using System.Net;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Session;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>
/// Scriptable <see cref="IMessengerSession"/>: records calls, lets tests drive the state and raise
/// warnings. Used by the periodic sender and view model tests.
/// </summary>
internal sealed class FakeMessengerSession : IMessengerSession
{
    private readonly List<IMessage> _sent = [];
    private SessionState _state = SessionState.Connected;

    public SessionMode? Mode { get; set; } = SessionMode.Client;

    /// <summary>Setting the state raises <see cref="StateChanged"/>, as the real session does.</summary>
    public SessionState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public int? LocalPort { get; set; }

    public EndPoint? RemoteEndPoint { get; set; }

    public bool AutoReplyEnabled { get; set; } = true;

    public ITrafficLog Log { get; } = new TrafficLog();

    /// <summary>When set, <see cref="SendAsync"/> throws this.</summary>
    public Exception? SendException { get; set; }

    /// <summary>When set, <see cref="StartServerAsync"/> and <see cref="ConnectAsync"/> throw this.</summary>
    public Exception? StartException { get; set; }

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

    public List<int> StartServerCalls { get; } = [];

    public List<(string Host, int Port)> ConnectCalls { get; } = [];

    public int StopCalls { get; private set; }

    public event EventHandler<SessionState>? StateChanged;

    public event EventHandler<SessionWarning>? WarningRaised;

    public Task StartServerAsync(int port, CancellationToken cancellationToken = default)
    {
        StartServerCalls.Add(port);
        if (StartException is not null)
        {
            throw StartException;
        }

        Mode = SessionMode.Server;
        LocalPort = port;
        State = SessionState.Listening;
        return Task.CompletedTask;
    }

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ConnectCalls.Add((host, port));
        if (StartException is not null)
        {
            throw StartException;
        }

        Mode = SessionMode.Client;
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
        State = SessionState.Connected;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCalls++;
        Mode = null;
        State = SessionState.Idle;
        return Task.CompletedTask;
    }

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

    /// <summary>Raises <see cref="WarningRaised"/>.</summary>
    public void RaiseWarning(SessionWarningKind kind, string message) =>
        WarningRaised?.Invoke(this, new SessionWarning(DateTimeOffset.Now, kind, message));
}
