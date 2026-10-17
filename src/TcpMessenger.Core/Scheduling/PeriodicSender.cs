using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Session;

namespace TcpMessenger.Core.Scheduling;

/// <summary>
/// <see cref="IPeriodicSender"/> on top of <see cref="TimeProvider"/> timers, so that schedules can be
/// unit-tested with a fake clock. Ticks that arrive while the previous send is still in flight are
/// skipped rather than queued, which keeps a slow link from building a backlog.
/// </summary>
public sealed class PeriodicSender : IPeriodicSender
{
    private readonly IMessengerSession _session;
    private readonly IOutgoingMessageProvider _messages;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Schedule> _schedules = [];
    private bool _disposed;

    public PeriodicSender(IMessengerSession session, IOutgoingMessageProvider messages, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(messages);

        _session = session;
        _messages = messages;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public TimeSpan MinimumInterval { get; } = TimeSpan.FromMilliseconds(10);

    /// <inheritdoc />
    public event EventHandler<PeriodicSendFailure>? SendFailed;

    /// <inheritdoc />
    public bool IsRunning(int messageId)
    {
        lock (_gate)
        {
            return _schedules.ContainsKey(messageId);
        }
    }

    /// <inheritdoc />
    public void Start(int messageId, TimeSpan interval)
    {
        if (interval < MinimumInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, $"Interval must be at least {MinimumInterval.TotalMilliseconds} ms.");
        }

        Schedule? previous;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _schedules.Remove(messageId, out previous);
            var schedule = new Schedule(messageId);
            _schedules[messageId] = schedule;
            schedule.Timer = _timeProvider.CreateTimer(OnTick, schedule, interval, interval);
        }

        previous?.Dispose();
    }

    /// <inheritdoc />
    public void Stop(int messageId)
    {
        Schedule? schedule;
        lock (_gate)
        {
            _schedules.Remove(messageId, out schedule);
        }

        schedule?.Dispose();
    }

    /// <inheritdoc />
    public void StopAll()
    {
        Schedule[] schedules;
        lock (_gate)
        {
            schedules = [.. _schedules.Values];
            _schedules.Clear();
        }

        foreach (var schedule in schedules)
        {
            schedule.Dispose();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        StopAll();
        return ValueTask.CompletedTask;
    }

    private void OnTick(object? state)
    {
        var schedule = (Schedule)state!;
        if (!schedule.TryEnter())
        {
            return; // previous send still running: skip this tick
        }

        _ = SendOnceAsync(schedule);
    }

    private async Task SendOnceAsync(Schedule schedule)
    {
        try
        {
            if (_session.State != SessionState.Connected)
            {
                return;
            }

            var message = _messages.GetOutgoingMessage(schedule.MessageId);
            if (message is null)
            {
                return; // form currently invalid: nothing to send this tick
            }

            await _session.SendAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Fire-and-forget boundary: report through the event instead of an unobserved task exception.
            Stop(schedule.MessageId);
            SendFailed?.Invoke(this, new PeriodicSendFailure(schedule.MessageId, ex));
        }
        finally
        {
            schedule.Exit();
        }
    }

    private sealed class Schedule(int messageId) : IDisposable
    {
        private int _busy;

        public int MessageId { get; } = messageId;

        public ITimer? Timer { get; set; }

        public bool TryEnter() => Interlocked.CompareExchange(ref _busy, 1, 0) == 0;

        public void Exit() => Volatile.Write(ref _busy, 0);

        public void Dispose() => Timer?.Dispose();
    }
}
