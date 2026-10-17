namespace TcpMessenger.Transport.Abstractions;

/// <summary>Opens outgoing connections.</summary>
public interface IClientTransport
{
    /// <summary>
    /// Connects to a server and returns a connection that is <b>not yet started</b>: subscribe to its
    /// events, then call <see cref="IConnection.Start"/>.
    /// </summary>
    /// <exception cref="System.Net.Sockets.SocketException">The server refused or is unreachable.</exception>
    /// <exception cref="TimeoutException">The connect attempt exceeded the configured timeout.</exception>
    Task<IConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
}
