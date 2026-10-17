using TcpMessenger.Core.Traffic;

namespace TcpMessenger.Core.Abstractions;

/// <summary>
/// Bounded, thread-safe list of everything that went over the wire, oldest first.
/// The UI mirrors it through <see cref="EntryAdded"/>.
/// </summary>
public interface ITrafficLog
{
    /// <summary>Maximum number of entries kept; the oldest entry is dropped when exceeded (K8).</summary>
    int Capacity { get; }

    /// <summary>Current number of entries.</summary>
    int Count { get; }

    /// <summary>Snapshot of the entries, oldest first.</summary>
    IReadOnlyList<TrafficEntry> Snapshot();

    /// <summary>Raised after an entry was appended. May be raised on any thread.</summary>
    event EventHandler<TrafficEntry>? EntryAdded;

    /// <summary>Appends an entry, dropping the oldest one when the log is full.</summary>
    void Add(TrafficEntry entry);

    /// <summary>Removes every entry.</summary>
    void Clear();
}
