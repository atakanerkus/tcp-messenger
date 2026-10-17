using System.Globalization;
using System.Net.Sockets;
using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Transport.Tcp;

/// <summary><see cref="IClientTransport"/> over <see cref="TcpClient"/> with a connect timeout.</summary>
public sealed class TcpClientTransport : IClientTransport
{
    /// <summary>Default connect timeout.</summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _connectTimeout;

    public TcpClientTransport()
        : this(DefaultConnectTimeout)
    {
    }

    public TcpClientTransport(TimeSpan connectTimeout)
    {
        if (connectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectTimeout), connectTimeout, "Timeout must be positive.");
        }

        _connectTimeout = connectTimeout;
    }

    /// <inheritdoc />
    public async Task<IConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_connectTimeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            throw new TimeoutException(string.Format(
                CultureInfo.InvariantCulture,
                "Connecting to {0}:{1} timed out after {2:0.#} s.",
                host,
                port,
                _connectTimeout.TotalSeconds));
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new TcpConnection(client);
    }
}
