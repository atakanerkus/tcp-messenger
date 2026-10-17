using System.Net;

namespace TcpMessenger.Transport.Abstractions;

/// <summary>
/// Listens for incoming connections. Only one connection is active at a time (see docs/decisions.md, K5):
/// further clients are accepted and immediately closed, reported through <see cref="ClientRejected"/>.
/// </summary>
public interface IServerTransport : IAsyncDisposable
{
    /// <summary>True between <see cref="StartAsync"/> and <see cref="StopAsync"/>.</summary>
    bool IsListening { get; }

    /// <summary>Actual bound endpoint; useful when port 0 was requested.</summary>
    IPEndPoint? LocalEndPoint { get; }

    /// <summary>
    /// Raised with a connection that is <b>not yet started</b>: subscribe to its events, then call
    /// <see cref="IConnection.Start"/>.
    /// </summary>
    event EventHandler<IConnection>? ClientConnected;

    /// <summary>Raised when a client was turned away because another one is already connected.</summary>
    event EventHandler<EndPoint?>? ClientRejected;

    /// <summary>Starts listening on all IPv4 interfaces. Port 0 picks a free port.</summary>
    /// <exception cref="InvalidOperationException">Already listening.</exception>
    Task StartAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>Stops listening and closes the active connection, if any. Safe to call when not listening.</summary>
    Task StopAsync();
}
