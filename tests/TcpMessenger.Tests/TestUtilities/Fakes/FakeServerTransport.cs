using System.Net;
using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>In-memory <see cref="IServerTransport"/>; tests raise accept/reject events explicitly.</summary>
internal sealed class FakeServerTransport : IServerTransport
{
    public bool IsListening { get; private set; }

    public IPEndPoint? LocalEndPoint { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    /// <summary>When set, <see cref="StartAsync"/> throws this.</summary>
    public Exception? StartException { get; set; }

    public event EventHandler<IConnection>? ClientConnected;

    public event EventHandler<EndPoint?>? ClientRejected;

    public Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        if (StartException is not null)
        {
            throw StartException;
        }

        if (IsListening)
        {
            throw new InvalidOperationException("Already listening.");
        }

        StartCount++;
        IsListening = true;
        LocalEndPoint = new IPEndPoint(IPAddress.Any, port == 0 ? 50_000 : port);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCount++;
        IsListening = false;
        LocalEndPoint = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    public void SimulateClientConnected(IConnection connection) => ClientConnected?.Invoke(this, connection);

    public void SimulateClientRejected(EndPoint? remote) => ClientRejected?.Invoke(this, remote);
}
