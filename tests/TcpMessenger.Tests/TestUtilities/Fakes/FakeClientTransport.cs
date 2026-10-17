using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>In-memory <see cref="IClientTransport"/> that hands out a prepared <see cref="FakeConnection"/>.</summary>
internal sealed class FakeClientTransport : IClientTransport
{
    /// <summary>Connection returned by the next <see cref="ConnectAsync"/>; a fresh one when null.</summary>
    public FakeConnection? NextConnection { get; set; }

    /// <summary>When set, <see cref="ConnectAsync"/> throws this.</summary>
    public Exception? ConnectException { get; set; }

    /// <summary>Host and port of the last connect attempt.</summary>
    public (string Host, int Port)? LastTarget { get; private set; }

    public Task<IConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        LastTarget = (host, port);

        if (ConnectException is not null)
        {
            throw ConnectException;
        }

        IConnection connection = NextConnection ?? new FakeConnection();
        return Task.FromResult(connection);
    }
}
