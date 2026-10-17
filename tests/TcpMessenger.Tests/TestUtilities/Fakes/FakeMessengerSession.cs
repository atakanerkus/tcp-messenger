using System.Net;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Session;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>Minimal <see cref="IMessengerSession"/> that records what was sent. Used to test the periodic sender.</summary>
internal sealed class FakeMessengerSession : IMessengerSession
{
    private readonly List<IMessage> _sent = [];

    public SessionMode? Mode { get; set; } = SessionMode.Client;

    public SessionState State { get; set; } = SessionState.Connected;

    public int? LocalPort => null;

    public EndPoint? RemoteEndPoint => null;

    public bool AutoReplyEnabled { get; set; }

    public ITrafficLog Log { get; } = new TrafficLog();

    /// <summary>When set, <see cref="SendAsync"/> throws this.</summary>
    public Exception? SendException { get; set; }

    /// <summary>When set, <see cref="SendAsync"/> does not complete until this does.</summary>
    public TaskCompletionSource? SendGate { get; set; }

    public IReadOnlyList<IMessage> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

#pragma warning disable CS0067 // events are part of the interface but never raised by this fake
    public event EventHandler<SessionState>? StateChanged;

    public event EventHandler<SessionWarning>? WarningRaised;
#pragma warning restore CS0067

    public Task StartServerAsync(int port, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public Task SendAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        if (SendException is not null)
        {
            throw SendException;
        }

        lock (_sent)
        {
            _sent.Add(message);
        }

        return SendGate?.Task ?? Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
