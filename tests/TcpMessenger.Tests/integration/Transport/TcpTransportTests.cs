using System.Net;
using System.Net.Sockets;
using TcpMessenger.Tests.TestUtilities;
using TcpMessenger.Transport.Abstractions;
using TcpMessenger.Transport.Tcp;

namespace TcpMessenger.Tests.Integration.Transport;

/// <summary>Real sockets on loopback. Each test starts its own server on an OS-assigned port.</summary>
[Trait("Category", "Integration")]
public sealed class TcpTransportTests : IAsyncLifetime
{
    private readonly TcpServerTransport _server = new();
    private readonly TcpClientTransport _clientTransport = new(TimeSpan.FromSeconds(5));
    private readonly List<IConnection> _connections = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await _server.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_PortZero_BindsToFreePortAndReportsIt()
    {
        await _server.StartAsync(0);

        Assert.True(_server.IsListening);
        Assert.NotNull(_server.LocalEndPoint);
        Assert.NotEqual(0, _server.LocalEndPoint!.Port);
    }

    [Fact]
    public async Task StartAsync_Twice_Throws()
    {
        await _server.StartAsync(0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _server.StartAsync(0));
    }

    [Fact]
    public async Task Connect_ServerAcceptsClient_BothEndsAreOpen()
    {
        var accepted = AsyncTestHelpers.NewSignal<IConnection>();
        _server.ClientConnected += (_, c) => accepted.TrySetResult(c);
        await _server.StartAsync(0);

        var client = await ConnectClientAsync();
        var serverSide = Track(await accepted.Task.WithinTimeout("server to accept the client"));

        Assert.True(client.IsOpen);
        Assert.True(serverSide.IsOpen);
        var remote = Assert.IsType<IPEndPoint>(client.RemoteEndPoint);
        Assert.Equal(_server.LocalEndPoint!.Port, remote.Port);
        Assert.Equal(IPAddress.Loopback, remote.Address); // plain IPv4, not "[::ffff:127.0.0.1]"
        Assert.Equal(IPAddress.Loopback, Assert.IsType<IPEndPoint>(serverSide.RemoteEndPoint).Address);
    }

    [Fact]
    public async Task Send_ClientToServer_ServerReceivesExactBytes()
    {
        var (client, serverSide) = await ConnectPairAsync();
        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var collector = new ReceivedBytesCollector(serverSide, payload.Length);
        serverSide.Start();
        client.Start();

        await client.SendAsync(payload);

        Assert.Equal(payload, await collector.Bytes.WithinTimeout("server to receive the payload"));
    }

    [Fact]
    public async Task Send_ServerToClient_ClientReceivesExactBytes()
    {
        var (client, serverSide) = await ConnectPairAsync();
        byte[] payload = "merhaba dünya"u8.ToArray();
        var collector = new ReceivedBytesCollector(client, payload.Length);
        serverSide.Start();
        client.Start();

        await serverSide.SendAsync(payload);

        Assert.Equal(payload, await collector.Bytes.WithinTimeout("client to receive the payload"));
    }

    [Fact]
    public async Task Send_LargePayload_ArrivesCompleteAndInOrder()
    {
        var (client, serverSide) = await ConnectPairAsync();
        var payload = new byte[1024 * 1024];
        new Random(Seed: 42).NextBytes(payload);
        var collector = new ReceivedBytesCollector(serverSide, payload.Length);
        serverSide.Start();
        client.Start();

        await client.SendAsync(payload);

        Assert.Equal(payload, await collector.Bytes.WithinTimeout("server to receive 1 MiB"));
    }

    [Fact]
    public async Task Send_ManyConcurrentWrites_EachPayloadStaysContiguous()
    {
        var (client, serverSide) = await ConnectPairAsync();
        const int payloadCount = 200;
        const int payloadSize = 64;
        var collector = new ReceivedBytesCollector(serverSide, payloadCount * payloadSize);
        serverSide.Start();
        client.Start();

        var sends = Enumerable.Range(0, payloadCount)
            .Select(i => client.SendAsync(Enumerable.Repeat((byte)i, payloadSize).ToArray()));
        await Task.WhenAll(sends);

        byte[] received = await collector.Bytes.WithinTimeout("server to receive all payloads");
        for (int offset = 0; offset < received.Length; offset += payloadSize)
        {
            var block = received.AsSpan(offset, payloadSize);
            byte marker = block[0];
            Assert.True(block.IndexOfAnyExcept(marker) < 0, $"Payload at offset {offset} was interleaved with another write.");
        }
    }

    [Fact]
    public async Task ClientClose_ServerSideRaisesClosedWithoutError()
    {
        var (client, serverSide) = await ConnectPairAsync();
        var closed = AsyncTestHelpers.NewSignal<Exception?>();
        serverSide.Closed += (_, error) => closed.TrySetResult(error);
        serverSide.Start();
        client.Start();

        await client.CloseAsync();

        Assert.Null(await closed.Task.WithinTimeout("server side to observe the close"));
        Assert.False(serverSide.IsOpen);
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task ServerStop_ClientSideRaisesClosed()
    {
        var (client, serverSide) = await ConnectPairAsync();
        var closed = AsyncTestHelpers.NewSignal<Exception?>();
        client.Closed += (_, error) => closed.TrySetResult(error);
        serverSide.Start();
        client.Start();

        await _server.StopAsync();

        await closed.Task.WithinTimeout("client to observe the server stopping");
        Assert.False(client.IsOpen);
        Assert.False(_server.IsListening);
    }

    [Fact]
    public async Task Closed_IsRaisedExactlyOnce_EvenWhenBothSidesClose()
    {
        var (client, serverSide) = await ConnectPairAsync();
        int closedCount = 0;
        var closed = AsyncTestHelpers.NewSignal<bool>();
        client.Closed += (_, _) =>
        {
            Interlocked.Increment(ref closedCount);
            closed.TrySetResult(true);
        };
        serverSide.Start();
        client.Start();

        await serverSide.CloseAsync();
        await closed.Task.WithinTimeout("client to observe the close");
        await client.CloseAsync();
        await client.DisposeAsync();

        Assert.Equal(1, closedCount);
    }

    [Fact]
    public async Task SendAsync_AfterClose_ThrowsInvalidOperation()
    {
        var (client, _) = await ConnectPairAsync();
        await client.CloseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(new byte[] { 1 }));
    }

    [Fact]
    public async Task SecondClient_WhileFirstIsConnected_IsRejected()
    {
        var rejected = AsyncTestHelpers.NewSignal<EndPoint?>();
        _server.ClientRejected += (_, remote) => rejected.TrySetResult(remote);
        var (first, _) = await ConnectPairAsync();
        first.Start();

        var second = await ConnectClientAsync();
        var secondClosed = AsyncTestHelpers.NewSignal<bool>();
        second.Closed += (_, _) => secondClosed.TrySetResult(true);
        second.Start();

        Assert.NotNull(await rejected.Task.WithinTimeout("server to report the rejection"));
        await secondClosed.Task.WithinTimeout("rejected client to see its connection closed");
        Assert.True(first.IsOpen);
    }

    [Fact]
    public async Task AfterActiveClientDisconnects_NextClientIsAccepted()
    {
        var accepted = new List<IConnection>();
        var secondAccepted = AsyncTestHelpers.NewSignal<IConnection>();
        _server.ClientConnected += (_, c) =>
        {
            lock (accepted)
            {
                accepted.Add(Track(c));
                if (accepted.Count == 2)
                {
                    secondAccepted.TrySetResult(c);
                }
            }
        };
        await _server.StartAsync(0);

        var first = await ConnectClientAsync();
        var firstServerSide = await WaitForAcceptedAsync(accepted, 1);
        var firstServerClosed = AsyncTestHelpers.NewSignal<bool>();
        firstServerSide.Closed += (_, _) => firstServerClosed.TrySetResult(true);
        firstServerSide.Start();
        first.Start();

        await first.CloseAsync();
        await firstServerClosed.Task.WithinTimeout("server to notice the first client leaving");

        var second = await ConnectClientAsync();
        second.Start();

        var secondServerSide = await secondAccepted.Task.WithinTimeout("server to accept the second client");
        Assert.True(secondServerSide.IsOpen);
        Assert.True(second.IsOpen);
    }

    [Fact]
    public async Task StopAsync_ThenStartAsync_ListensAgainOnTheSamePort()
    {
        await _server.StartAsync(0);
        int port = _server.LocalEndPoint!.Port;

        await _server.StopAsync();
        Assert.False(_server.IsListening);
        await _server.StartAsync(port);

        Assert.True(_server.IsListening);
        var client = await ConnectClientAsync();
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task StopAsync_WhenNotListening_IsANoOp()
    {
        await _server.StopAsync();
        await _server.StopAsync();

        Assert.False(_server.IsListening);
    }

    [Fact]
    public async Task ConnectAsync_NoServerListening_ThrowsSocketException()
    {
        // Bind and immediately release a port so that nothing listens on it.
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        await Assert.ThrowsAsync<SocketException>(() => _clientTransport.ConnectAsync("127.0.0.1", port));
    }

    [Fact]
    public void Constructor_NonPositiveTimeout_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TcpClientTransport(TimeSpan.Zero));
    }

    // ---- helpers ---------------------------------------------------------------------------

    private async Task<(IConnection Client, IConnection ServerSide)> ConnectPairAsync()
    {
        var accepted = AsyncTestHelpers.NewSignal<IConnection>();
        _server.ClientConnected += (_, c) => accepted.TrySetResult(c);
        if (!_server.IsListening)
        {
            await _server.StartAsync(0);
        }

        var client = await ConnectClientAsync();
        var serverSide = Track(await accepted.Task.WithinTimeout("server to accept the client"));
        return (client, serverSide);
    }

    private async Task<IConnection> ConnectClientAsync()
    {
        var connection = await _clientTransport
            .ConnectAsync("127.0.0.1", _server.LocalEndPoint!.Port)
            .WithinTimeout("client to connect");
        return Track(connection);
    }

    private IConnection Track(IConnection connection)
    {
        lock (_connections)
        {
            _connections.Add(connection);
        }

        return connection;
    }

    private static async Task<IConnection> WaitForAcceptedAsync(List<IConnection> accepted, int count)
    {
        var deadline = DateTime.UtcNow + AsyncTestHelpers.Timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (accepted)
            {
                if (accepted.Count >= count)
                {
                    return accepted[count - 1];
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"Server did not accept {count} client(s) in time.");
    }
}
