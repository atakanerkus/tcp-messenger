using System.Net;
using System.Net.Sockets;
using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Transport.Tcp;

/// <summary>
/// <see cref="IConnection"/> over a connected <see cref="TcpClient"/>. Owns the socket and
/// disposes it on close. Nagle is disabled so that small periodic messages are not coalesced.
/// </summary>
public sealed class TcpConnection : IConnection
{
    private const int ReceiveBufferSize = 8 * 1024;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _receiveCts = new();
    private Task? _receiveLoop;
    private int _closed;

    public TcpConnection(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        RemoteEndPoint = NormalizeEndPoint(client.Client.RemoteEndPoint);
    }

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint { get; }

    /// <inheritdoc />
    public bool IsOpen => Volatile.Read(ref _closed) == 0;

    /// <inheritdoc />
    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    /// <inheritdoc />
    public event EventHandler<Exception?>? Closed;

    /// <inheritdoc />
    public void Start()
    {
        if (_receiveLoop is not null || !IsOpen)
        {
            return;
        }

        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The connection is closed.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            await CloseCoreAsync(ex).ConfigureAwait(false);
            throw;
        }
        catch (ObjectDisposedException ex)
        {
            await CloseCoreAsync(ex).ConfigureAwait(false);
            throw new InvalidOperationException("The connection is closed.", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public Task CloseAsync() => CloseCoreAsync(error: null);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseCoreAsync(error: null).ConfigureAwait(false);
        _receiveCts.Dispose();
        _sendLock.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        Exception? error = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break; // remote peer closed gracefully
                }

                // Copy: the buffer is reused for the next read while handlers may hold on to the chunk.
                DataReceived?.Invoke(this, buffer.AsMemory(0, read).ToArray());
            }
        }
        catch (OperationCanceledException)
        {
            // local close requested
        }
        catch (IOException ex)
        {
            error = ex; // connection reset, network failure, ...
        }
        catch (ObjectDisposedException)
        {
            // socket disposed by a concurrent close
        }
        finally
        {
            await CloseCoreAsync(error).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A dual-stack client socket reports IPv4 peers as IPv4-mapped IPv6 addresses ("[::ffff:127.0.0.1]");
    /// show them the way users typed them.
    /// </summary>
    private static EndPoint? NormalizeEndPoint(EndPoint? endPoint) =>
        endPoint is IPEndPoint { Address.IsIPv4MappedToIPv6: true } ip
            ? new IPEndPoint(ip.Address.MapToIPv4(), ip.Port)
            : endPoint;

    private Task CloseCoreAsync(Exception? error)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _receiveCts.Cancel();

        try
        {
            _client.Client.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
            // already reset by the peer
        }
        catch (ObjectDisposedException)
        {
            // already disposed
        }

        _client.Dispose();
        Closed?.Invoke(this, error);
        return Task.CompletedTask;
    }
}
