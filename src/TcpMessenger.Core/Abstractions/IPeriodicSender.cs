using TcpMessenger.Core.Scheduling;

namespace TcpMessenger.Core.Abstractions;

/// <summary>
/// Sends a message type repeatedly at a fixed interval, e.g. Mesaj 1 every 100 ms and Mesaj 2
/// every 500 ms at the same time. Each tick sends the current form values.
/// </summary>
public interface IPeriodicSender : IAsyncDisposable
{
    /// <summary>Smallest interval accepted by <see cref="Start"/>.</summary>
    TimeSpan MinimumInterval { get; }

    /// <summary>True while a schedule exists for the message type.</summary>
    bool IsRunning(int messageId);

    /// <summary>Raised when a scheduled send failed; the schedule for that message type has been stopped.</summary>
    event EventHandler<PeriodicSendFailure>? SendFailed;

    /// <summary>Starts (or restarts with a new interval) the schedule for a message type.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is below <see cref="MinimumInterval"/>.</exception>
    void Start(int messageId, TimeSpan interval);

    /// <summary>Stops the schedule for a message type. No-op when not running.</summary>
    void Stop(int messageId);

    /// <summary>Stops every schedule.</summary>
    void StopAll();
}
