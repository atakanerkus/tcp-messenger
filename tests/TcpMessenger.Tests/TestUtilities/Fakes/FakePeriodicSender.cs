using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Scheduling;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>Records Start/Stop calls of the <see cref="IPeriodicSender"/> contract.</summary>
internal sealed class FakePeriodicSender : IPeriodicSender
{
    private readonly Dictionary<int, TimeSpan> _running = [];

    public TimeSpan MinimumInterval { get; } = TimeSpan.FromMilliseconds(10);

    public List<(int MessageId, TimeSpan Interval)> StartCalls { get; } = [];

    public List<int> StopCalls { get; } = [];

    public event EventHandler<PeriodicSendFailure>? SendFailed;

    public bool IsRunning(int messageId) => _running.ContainsKey(messageId);

    public TimeSpan? IntervalOf(int messageId) => _running.TryGetValue(messageId, out var interval) ? interval : null;

    public void Start(int messageId, TimeSpan interval)
    {
        if (interval < MinimumInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        StartCalls.Add((messageId, interval));
        _running[messageId] = interval;
    }

    public void Stop(int messageId)
    {
        StopCalls.Add(messageId);
        _running.Remove(messageId);
    }

    public void StopAll() => _running.Clear();

    public ValueTask DisposeAsync()
    {
        StopAll();
        return ValueTask.CompletedTask;
    }

    /// <summary>Raises <see cref="SendFailed"/> as the real sender does after a failed tick.</summary>
    public void RaiseSendFailed(int messageId, Exception error)
    {
        _running.Remove(messageId);
        SendFailed?.Invoke(this, new PeriodicSendFailure(messageId, error));
    }
}
