using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Time.Testing;
using TcpMessenger.Core.Session;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.Core;

/// <summary>
/// Session logic with faked transports and the real protocol components. Fakes raise events
/// synchronously, so every assertion can follow the stimulus directly.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessengerSessionTests : IAsyncDisposable
{
    private readonly FakeServerTransport _server = new();
    private readonly FakeClientTransport _client = new();
    private readonly StubOutgoingMessageProvider _outgoing = StubOutgoingMessageProvider.WithValidMessages();
    private readonly TrafficLog _log = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.FromHours(3)));
    private readonly BinaryMessageCodec _codec = new();
    private readonly List<SessionState> _states = [];
    private readonly List<SessionWarning> _warnings = [];
    private readonly MessengerSession _session;

    public MessengerSessionTests()
    {
        _session = new MessengerSession(_server, _client, _codec, new FrameParser(), new MessageValidator(), _outgoing, _log, _clock);
        _session.StateChanged += (_, s) => _states.Add(s);
        _session.WarningRaised += (_, w) => _warnings.Add(w);
    }

    public ValueTask DisposeAsync() => _session.DisposeAsync();

    // ---- lifecycle: server -------------------------------------------------------------------

    [Fact]
    public async Task StartServerAsync_StartsTransportAndEntersListening()
    {
        await _session.StartServerAsync(5000);

        Assert.Equal(SessionState.Listening, _session.State);
        Assert.Equal(SessionMode.Server, _session.Mode);
        Assert.Equal(5000, _session.LocalPort);
        Assert.Equal(1, _server.StartCount);
    }

    [Fact]
    public async Task StartServerAsync_WhenTransportFails_StaysIdleAndRethrows()
    {
        _server.StartException = new SocketException((int)SocketError.AddressAlreadyInUse);

        await Assert.ThrowsAsync<SocketException>(() => _session.StartServerAsync(5000));

        Assert.Equal(SessionState.Idle, _session.State);
        Assert.Null(_session.Mode);
    }

    [Fact]
    public async Task ClientConnected_AttachesStartsAndEntersConnected()
    {
        await _session.StartServerAsync(5000);
        var connection = new FakeConnection();

        _server.SimulateClientConnected(connection);

        Assert.Equal(SessionState.Connected, _session.State);
        Assert.Equal(1, connection.StartCount);
        Assert.Equal(connection.RemoteEndPoint, _session.RemoteEndPoint);
        Assert.Equal([SessionState.Listening, SessionState.Connected], _states);
    }

    [Fact]
    public async Task ClientRejected_RaisesWarning()
    {
        await _session.StartServerAsync(5000);

        _server.SimulateClientRejected(new IPEndPoint(IPAddress.Loopback, 60000));

        var warning = Assert.Single(_warnings);
        Assert.Equal(SessionWarningKind.ClientRejected, warning.Kind);
        Assert.Contains("127.0.0.1:60000", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PeerCloses_InServerMode_ReturnsToListeningWithWarning()
    {
        var connection = await ConnectAsServerAsync();

        connection.SimulateRemoteClose(error: null);

        Assert.Equal(SessionState.Listening, _session.State);
        Assert.Equal(SessionMode.Server, _session.Mode);
        Assert.Null(_session.RemoteEndPoint);
        var warning = Assert.Single(_warnings);
        Assert.Equal(SessionWarningKind.ConnectionClosed, warning.Kind);
    }

    [Fact]
    public async Task PeerDrops_WithError_WarningMentionsTheError()
    {
        var connection = await ConnectAsServerAsync();

        connection.SimulateRemoteClose(new IOException("connection reset"));

        var warning = Assert.Single(_warnings);
        Assert.Equal(SessionWarningKind.ConnectionClosed, warning.Kind);
        Assert.Contains("connection reset", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterPeerLeaves_NextClientIsAccepted()
    {
        var first = await ConnectAsServerAsync();
        first.SimulateRemoteClose(error: null);
        var second = new FakeConnection();

        _server.SimulateClientConnected(second);

        Assert.Equal(SessionState.Connected, _session.State);
        Assert.Equal(1, second.StartCount);
    }

    // ---- lifecycle: client -------------------------------------------------------------------

    [Fact]
    public async Task ConnectAsync_GoesThroughConnectingToConnected()
    {
        var connection = new FakeConnection();
        _client.NextConnection = connection;

        await _session.ConnectAsync("10.0.0.5", 7000);

        Assert.Equal(("10.0.0.5", 7000), _client.LastTarget);
        Assert.Equal([SessionState.Connecting, SessionState.Connected], _states);
        Assert.Equal(SessionMode.Client, _session.Mode);
        Assert.Equal(1, connection.StartCount);
    }

    [Fact]
    public async Task ConnectAsync_WhenRefused_ReturnsToIdleAndRethrows()
    {
        _client.ConnectException = new SocketException((int)SocketError.ConnectionRefused);

        await Assert.ThrowsAsync<SocketException>(() => _session.ConnectAsync("127.0.0.1", 7000));

        Assert.Equal([SessionState.Connecting, SessionState.Idle], _states);
        Assert.Null(_session.Mode);
    }

    [Fact]
    public async Task PeerCloses_InClientMode_ReturnsToIdle()
    {
        var connection = await ConnectAsClientAsync();

        connection.SimulateRemoteClose(error: null);

        Assert.Equal(SessionState.Idle, _session.State);
        Assert.Null(_session.Mode);
        Assert.Equal(SessionWarningKind.ConnectionClosed, Assert.Single(_warnings).Kind);
    }

    // ---- lifecycle: common -------------------------------------------------------------------

    [Fact]
    public async Task StartServerAsync_WhileActive_Throws()
    {
        await _session.StartServerAsync(5000);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.StartServerAsync(5001));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.ConnectAsync("127.0.0.1", 5001));
    }

    [Fact]
    public async Task StopAsync_ClosesConnectionStopsServerAndIsSilent()
    {
        var connection = await ConnectAsServerAsync();

        await _session.StopAsync();

        Assert.False(connection.IsOpen);
        Assert.Equal(1, _server.StopCount);
        Assert.Equal(SessionState.Idle, _session.State);
        Assert.Null(_session.Mode);
        Assert.Empty(_warnings); // a user-initiated stop is not a "connection lost" warning
    }

    [Fact]
    public async Task StopAsync_WhenIdle_IsANoOp()
    {
        await _session.StopAsync();

        Assert.Equal(SessionState.Idle, _session.State);
        Assert.Empty(_states);
    }

    [Fact]
    public async Task StopAsync_ThenStartAgain_Works()
    {
        await _session.StartServerAsync(5000);
        await _session.StopAsync();

        await _session.ConnectAsync("127.0.0.1", 5000);

        Assert.Equal(SessionState.Connected, _session.State);
    }

    // ---- sending -----------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_EncodesWritesAndLogsTransmit()
    {
        var connection = await ConnectAsClientAsync();
        var message = TestMessages.ValidMessage1();

        await _session.SendAsync(message);

        Assert.Equal(_codec.Encode(message), Assert.Single(connection.Sent));
        var entry = Assert.Single(_log.Snapshot());
        Assert.Equal(TrafficDirection.Transmit, entry.Direction);
        Assert.Equal("Mesaj 1", entry.MessageName);
        Assert.Same(message, entry.Message);
        Assert.Equal(_clock.GetLocalNow(), entry.Timestamp);
        Assert.True(entry.IsValid);
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(TestMessages.ValidMessage1()));

        await _session.StartServerAsync(5000);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(TestMessages.ValidMessage1()));
    }

    [Fact]
    public async Task SendAsync_UnencodableMessage_ThrowsAndLogsNothing()
    {
        await ConnectAsClientAsync();
        var tooLong = TestMessages.ValidMessage1() with { FirstName = new string('x', 26) };

        await Assert.ThrowsAsync<MessageFormatException>(() => _session.SendAsync(tooLong));

        Assert.Equal(0, _log.Count);
    }

    // ---- receiving ---------------------------------------------------------------------------

    [Fact]
    public async Task Receive_DefinedMessage_LogsReceiveEntryWithDecodedMessage()
    {
        var connection = await ConnectAsClientAsync();
        _session.AutoReplyEnabled = false;
        var incoming = TestMessages.ValidMessage2();

        connection.SimulateReceive(_codec.Encode(incoming));

        var entry = Assert.Single(_log.Snapshot());
        Assert.Equal(TrafficDirection.Receive, entry.Direction);
        Assert.Equal(TrafficEntryKind.Message, entry.Kind);
        Assert.Equal("Mesaj 2", entry.MessageName);
        Assert.Equal(incoming, entry.Message);
        Assert.True(entry.IsValid);
        Assert.Empty(_warnings);
    }

    [Fact]
    public async Task Receive_SplitAcrossChunks_ProducesOneEntry()
    {
        var connection = await ConnectAsClientAsync();
        _session.AutoReplyEnabled = false;
        byte[] frame = _codec.Encode(TestMessages.ValidMessage1());

        connection.SimulateReceive(frame[..10]);
        Assert.Equal(0, _log.Count);
        connection.SimulateReceive(frame[10..]);

        Assert.Equal(1, _log.Count);
    }

    [Fact]
    public async Task Receive_OutOfRangeFields_IsLoggedAsMessageWithErrors()
    {
        var connection = await ConnectAsClientAsync();
        _session.AutoReplyEnabled = false;
        var invalid = TestMessages.ValidMessage2() with { Altitude = 99_999, UnitReferenceNumber = 0 };

        connection.SimulateReceive(_codec.Encode(invalid));

        var entry = Assert.Single(_log.Snapshot());
        Assert.Equal(TrafficEntryKind.Message, entry.Kind);
        Assert.False(entry.IsValid);
        Assert.Equal(2, entry.Errors.Count);
        Assert.Empty(_warnings); // K6: out-of-range is not "undefined"
    }

    [Fact]
    public async Task Receive_UndefinedId_LogsUndefinedEntryAndWarns()
    {
        var connection = await ConnectAsClientAsync();
        byte[] garbage = [0x00, 0x00, 0x00, 0x09, 0xAA, 0xBB];

        connection.SimulateReceive(garbage);

        var entry = Assert.Single(_log.Snapshot());
        Assert.Equal(TrafficEntryKind.Undefined, entry.Kind);
        Assert.Equal(TrafficDirection.Receive, entry.Direction);
        Assert.Equal("Tanımsız (9)", entry.MessageName);
        Assert.Equal(garbage, entry.RawBytes.ToArray());
        var warning = Assert.Single(_warnings);
        Assert.Equal(SessionWarningKind.UndefinedMessageReceived, warning.Kind);
        Assert.Contains("ID 9", warning.Message, StringComparison.Ordinal);
        Assert.Empty(connection.Sent); // no auto-reply for undefined messages
    }

    // ---- auto reply --------------------------------------------------------------------------

    [Fact]
    public async Task Receive_Message1_WithAutoReply_SendsMessage2FromProvider()
    {
        var connection = await ConnectAsClientAsync();
        var expectedReply = TestMessages.ValidMessage2() with { Altitude = 1234 };
        _outgoing.Messages[MessageId.Message2] = expectedReply;

        connection.SimulateReceive(_codec.Encode(TestMessages.ValidMessage1()));

        Assert.Equal(_codec.Encode(expectedReply), Assert.Single(connection.Sent));
        Assert.Equal(["Receive Mesaj 1", "Transmit Mesaj 2"], _log.Snapshot().Select(e => e.Summary));
    }

    [Fact]
    public async Task Receive_Message2_WithAutoReply_SendsMessage1()
    {
        var connection = await ConnectAsClientAsync();

        connection.SimulateReceive(_codec.Encode(TestMessages.ValidMessage2()));

        Assert.Equal(_codec.Encode(TestMessages.ValidMessage1()), Assert.Single(connection.Sent));
        Assert.Equal(["Receive Mesaj 2", "Transmit Mesaj 1"], _log.Snapshot().Select(e => e.Summary));
    }

    [Fact]
    public async Task Receive_WithAutoReplyDisabled_SendsNothing()
    {
        var connection = await ConnectAsClientAsync();
        _session.AutoReplyEnabled = false;

        connection.SimulateReceive(_codec.Encode(TestMessages.ValidMessage1()));

        Assert.Empty(connection.Sent);
        Assert.Single(_log.Snapshot());
    }

    [Fact]
    public async Task Receive_OutOfRangeMessage_StillTriggersAutoReply()
    {
        var connection = await ConnectAsClientAsync();
        var invalid = TestMessages.ValidMessage1() with { UnitReferenceNumber = -5000 };

        connection.SimulateReceive(_codec.Encode(invalid));

        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AutoReply_WhenProviderHasNoValidMessage_WarnsInsteadOfSending()
    {
        var connection = await ConnectAsClientAsync();
        _outgoing.Messages[MessageId.Message2] = null;

        connection.SimulateReceive(_codec.Encode(TestMessages.ValidMessage1()));

        Assert.Empty(connection.Sent);
        var warning = Assert.Single(_warnings);
        Assert.Equal(SessionWarningKind.AutoReplyFailed, warning.Kind);
        Assert.Contains("Mesaj 2", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AutoReply_WhenSendFails_WarnsAndDoesNotThrow()
    {
        var connection = await ConnectAsClientAsync();
        connection.SendException = new IOException("broken pipe");

        connection.SimulateReceive(_codec.Encode(TestMessages.ValidMessage1()));

        var warning = Assert.Single(_warnings);
        Assert.Equal(SessionWarningKind.AutoReplyFailed, warning.Kind);
        Assert.Contains("broken pipe", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoReplyEnabled_DefaultsToTrue()
    {
        Assert.True(_session.AutoReplyEnabled);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private async Task<FakeConnection> ConnectAsServerAsync()
    {
        await _session.StartServerAsync(5000);
        var connection = new FakeConnection();
        _server.SimulateClientConnected(connection);
        _states.Clear();
        return connection;
    }

    private async Task<FakeConnection> ConnectAsClientAsync()
    {
        var connection = new FakeConnection();
        _client.NextConnection = connection;
        await _session.ConnectAsync("127.0.0.1", 5000);
        _states.Clear();
        return connection;
    }
}
