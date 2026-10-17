using System.Net;

namespace TcpMessenger.Transport.Abstractions;

/// <summary>
/// One established, bidirectional byte stream to a remote peer. The transport layer knows nothing
/// about messages; it only moves bytes and reports lifecycle changes.
/// </summary>
/// <remarks>
/// Subscribe to <see cref="DataReceived"/> and <see cref="Closed"/> <b>before</b> calling
/// <see cref="Start"/>, otherwise bytes that arrive early are lost.
/// Events are raised on a thread-pool thread, never on the caller's thread.
/// </remarks>
public interface IConnection : IAsyncDisposable
{
    /// <summary>Address of the remote peer, when known.</summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>False once the connection has been closed by either side or by an error.</summary>
    bool IsOpen { get; }

    /// <summary>Raised for every chunk read from the stream. Chunk boundaries are arbitrary.</summary>
    event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    /// <summary>Raised exactly once when the connection ends. The argument is null for a clean close.</summary>
    event EventHandler<Exception?>? Closed;

    /// <summary>Begins the background receive loop. Calling it more than once has no effect.</summary>
    void Start();

    /// <summary>Writes the bytes to the peer. Concurrent calls are serialized, each payload stays contiguous.</summary>
    /// <exception cref="InvalidOperationException">The connection is closed.</exception>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>Closes the connection and raises <see cref="Closed"/> if it was still open.</summary>
    Task CloseAsync();
}
