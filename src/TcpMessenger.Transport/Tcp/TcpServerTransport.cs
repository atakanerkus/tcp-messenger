using System.Net;
using System.Net.Sockets;
using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Transport.Tcp;

/// <summary>
/// <see cref="IServerTransport"/> over <see cref="TcpListener"/>. Accepts in the background and
/// enforces the single-active-connection policy.
/// </summary>
public sealed class TcpServerTransport : IServerTransport
{
    private readonly Lock _gate = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptLoop;
    private TcpConnection? _active;

    /// <inheritdoc />
    public bool IsListening
    {
        get
        {
            lock (_gate)
            {
                return _listener is not null;
            }
        }
    }

    /// <inheritdoc />
    public IPEndPoint? LocalEndPoint
    {
        get
        {
            lock (_gate)
            {
                return _listener?.LocalEndpoint as IPEndPoint;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<IConnection>? ClientConnected;

    /// <inheritdoc />
    public event EventHandler<EndPoint?>? ClientRejected;

    /// <inheritdoc />
    public Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_listener is not null)
            {
                throw new InvalidOperationException("The server is already listening.");
            }

            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            _listener = listener;
            _acceptCts = new CancellationTokenSource();
            var token = _acceptCts.Token;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        TcpListener? listener;
        CancellationTokenSource? cts;
        Task? loop;
        TcpConnection? active;

        lock (_gate)
        {
            listener = _listener;
            cts = _acceptCts;
            loop = _acceptLoop;
            active = _active;
            _listener = null;
            _acceptCts = null;
            _acceptLoop = null;
            _active = null;
        }

        if (listener is null)
        {
            return;
        }

        cts?.Cancel();
        listener.Stop();

        if (loop is not null)
        {
            await loop.ConfigureAwait(false);
        }

        cts?.Dispose();

        if (active is not null)
        {
            await active.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            HandleAccepted(client);
        }
    }

    private void HandleAccepted(TcpClient client)
    {
        TcpConnection connection;
        lock (_gate)
        {
            if (_active is { IsOpen: true })
            {
                connection = null!;
            }
            else
            {
                connection = new TcpConnection(client);
                _active = connection;
            }
        }

        if (connection is null)
        {
            // Policy K5: one client at a time. Turn the newcomer away outside the lock.
            var remote = client.Client.RemoteEndPoint;
            client.Dispose();
            ClientRejected?.Invoke(this, remote);
            return;
        }

        connection.Closed += (_, _) =>
        {
            lock (_gate)
            {
                if (ReferenceEquals(_active, connection))
                {
                    _active = null;
                }
            }
        };

        ClientConnected?.Invoke(this, connection);
    }
}
