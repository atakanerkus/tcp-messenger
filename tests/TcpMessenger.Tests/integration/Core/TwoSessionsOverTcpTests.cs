using System.Net.Sockets;
using TcpMessenger.Core.Scheduling;
using TcpMessenger.Core.Session;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities;
using TcpMessenger.Tests.TestUtilities.Fakes;
using TcpMessenger.Transport.Tcp;

namespace TcpMessenger.Tests.Integration.Core;

/// <summary>
/// Two complete Core stacks (session + real TCP transports + real protocol) talking over loopback,
/// exactly as the two application instances will in the demo, minus the UI.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TwoSessionsOverTcpTests : IAsyncLifetime
{
    private readonly BinaryMessageCodec _codec = new();
    private readonly StubOutgoingMessageProvider _serverForm = StubOutgoingMessageProvider.WithValidMessages();
    private readonly StubOutgoingMessageProvider _clientForm = StubOutgoingMessageProvider.WithValidMessages();
    private MessengerSession _server = null!;
    private MessengerSession _client = null!;

    public Task InitializeAsync()
    {
        _server = NewSession(_serverForm);
        _client = NewSession(_clientForm);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task ClientConnects_BothSessionsReportConnected()
    {
        await ConnectBothAsync();

        Assert.Equal(SessionState.Connected, _server.State);
        Assert.Equal(SessionState.Connected, _client.State);
        Assert.NotNull(_server.RemoteEndPoint);
        Assert.NotNull(_client.RemoteEndPoint);
    }

    [Fact]
    public async Task ClientSendsMessage1_ServerLogsReceiveAndAutoRepliesWithMessage2()
    {
        await ConnectBothAsync();
        _client.AutoReplyEnabled = false; // K4: only one side answers, otherwise ping-pong
        _server.AutoReplyEnabled = true;
        var outgoing = TestMessages.ValidMessage1() with { FirstName = "Demo", UnitNo = 7 };
        using var serverReceived = new TrafficLogWaiter(_server.Log, e => e.Direction == TrafficDirection.Receive);
        using var clientReceived = new TrafficLogWaiter(_client.Log, e => e.Direction == TrafficDirection.Receive);

        await _client.SendAsync(outgoing);

        var received = await serverReceived.Entry.WithinTimeout("server to log the received Mesaj 1");
        Assert.Equal("Receive Mesaj 1", received.Summary);
        Assert.Equal(outgoing, received.Message);

        var reply = await clientReceived.Entry.WithinTimeout("client to log the automatic Mesaj 2");
        Assert.Equal("Receive Mesaj 2", reply.Summary);
        Assert.Equal(_serverForm.Messages[MessageId.Message2], reply.Message);

        Assert.Equal(["Transmit Mesaj 1", "Receive Mesaj 2"], _client.Log.Snapshot().Select(e => e.Summary));
        Assert.Equal(["Receive Mesaj 1", "Transmit Mesaj 2"], _server.Log.Snapshot().Select(e => e.Summary));
    }

    [Fact]
    public async Task ServerSendsMessage2_ClientLogsReceiveAndAutoRepliesWithMessage1()
    {
        await ConnectBothAsync();
        _server.AutoReplyEnabled = false;
        _client.AutoReplyEnabled = true;
        using var clientReceived = new TrafficLogWaiter(_client.Log, e => e.Direction == TrafficDirection.Receive);
        using var serverReceived = new TrafficLogWaiter(_server.Log, e => e.Direction == TrafficDirection.Receive);

        await _server.SendAsync(TestMessages.ValidMessage2());

        Assert.Equal("Receive Mesaj 2", (await clientReceived.Entry.WithinTimeout("client to receive")).Summary);
        Assert.Equal("Receive Mesaj 1", (await serverReceived.Entry.WithinTimeout("server to receive the reply")).Summary);
    }

    [Fact]
    public async Task BothSidesAutoReply_ProducesPingPong_WhichIsWhyTheToggleExists()
    {
        await ConnectBothAsync();
        _server.AutoReplyEnabled = true;
        _client.AutoReplyEnabled = true;
        var fiveReplies = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int received = 0;
        _client.Log.EntryAdded += (_, e) =>
        {
            if (e.Direction == TrafficDirection.Receive && Interlocked.Increment(ref received) == 5)
            {
                fiveReplies.TrySetResult();
            }
        };

        await _client.SendAsync(TestMessages.ValidMessage1());
        await fiveReplies.Task.WithinTimeout("the exchange to ping-pong at least five times");

        _client.AutoReplyEnabled = false; // break the loop
        Assert.True(received >= 5);
    }

    [Fact]
    public async Task RawGarbage_IsReportedAsUndefinedMessage()
    {
        await _server.StartServerAsync(0);
        var warned = AsyncTestHelpers.NewSignal<SessionWarning>();
        _server.WarningRaised += (_, w) =>
        {
            if (w.Kind == SessionWarningKind.UndefinedMessageReceived)
            {
                warned.TrySetResult(w);
            }
        };
        using var logged = new TrafficLogWaiter(_server.Log, e => e.Kind == TrafficEntryKind.Undefined);
        using var raw = new TcpClient();
        await raw.ConnectAsync("127.0.0.1", _server.LocalPort!.Value);

        await raw.GetStream().WriteAsync("HELLO WORLD"u8.ToArray());

        var warning = await warned.Task.WithinTimeout("server to warn about the undefined message");
        Assert.Contains("Tanımsız mesaj", warning.Message, StringComparison.Ordinal);
        var entry = await logged.Entry.WithinTimeout("server to log the undefined entry");
        Assert.Equal("HELLO WORLD"u8.ToArray(), entry.RawBytes.ToArray());
    }

    [Fact]
    public async Task PeriodicSender_OverRealTcp_DeliversRepeatedMessages()
    {
        await ConnectBothAsync();
        _server.AutoReplyEnabled = false;
        var threeReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        _server.Log.EntryAdded += (_, e) =>
        {
            if (e.Summary == "Receive Mesaj 1" && Interlocked.Increment(ref count) == 3)
            {
                threeReceived.TrySetResult();
            }
        };
        await using var periodic = new PeriodicSender(_client, _clientForm);

        periodic.Start(MessageId.Message1, TimeSpan.FromMilliseconds(50));
        await threeReceived.Task.WithinTimeout("three periodic messages to arrive");
        periodic.Stop(MessageId.Message1);

        Assert.True(count >= 3);
        Assert.False(periodic.IsRunning(MessageId.Message1));
    }

    [Fact]
    public async Task ClientStops_ServerReturnsToListeningAndAcceptsAgain()
    {
        await ConnectBothAsync();
        var listening = AsyncTestHelpers.NewSignal<SessionState>();
        _server.StateChanged += (_, s) =>
        {
            if (s == SessionState.Listening)
            {
                listening.TrySetResult(s);
            }
        };

        await _client.StopAsync();
        await listening.Task.WithinTimeout("server to go back to listening");

        var reconnected = AsyncTestHelpers.NewSignal<SessionState>();
        _server.StateChanged += (_, s) =>
        {
            if (s == SessionState.Connected)
            {
                reconnected.TrySetResult(s);
            }
        };
        await _client.ConnectAsync("127.0.0.1", _server.LocalPort!.Value);
        await reconnected.Task.WithinTimeout("server to accept the reconnect");

        Assert.Equal(SessionState.Connected, _client.State);
    }

    [Fact]
    public async Task ServerStops_ClientReturnsToIdleWithWarning()
    {
        await ConnectBothAsync();
        var idle = AsyncTestHelpers.NewSignal<SessionState>();
        var warned = AsyncTestHelpers.NewSignal<SessionWarning>();
        _client.StateChanged += (_, s) =>
        {
            if (s == SessionState.Idle)
            {
                idle.TrySetResult(s);
            }
        };
        _client.WarningRaised += (_, w) => warned.TrySetResult(w);

        await _server.StopAsync();

        await idle.Task.WithinTimeout("client to notice the server stopping");
        Assert.Equal(SessionWarningKind.ConnectionClosed, (await warned.Task.WithinTimeout("client warning")).Kind);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static MessengerSession NewSession(StubOutgoingMessageProvider form) => new(
        new TcpServerTransport(),
        new TcpClientTransport(TimeSpan.FromSeconds(5)),
        new BinaryMessageCodec(),
        new FrameParser(),
        new MessageValidator(),
        form,
        new TrafficLog());

    private async Task ConnectBothAsync()
    {
        var serverConnected = AsyncTestHelpers.NewSignal<SessionState>();
        _server.StateChanged += (_, s) =>
        {
            if (s == SessionState.Connected)
            {
                serverConnected.TrySetResult(s);
            }
        };

        await _server.StartServerAsync(0);
        await _client.ConnectAsync("127.0.0.1", _server.LocalPort!.Value);
        await serverConnected.Task.WithinTimeout("server session to become connected");
    }
}
