using TcpMessenger.Core.Abstractions;

namespace TcpMessenger.Core.Traffic;

/// <summary>Ring-buffer style <see cref="ITrafficLog"/> guarded by a lock.</summary>
public sealed class TrafficLog : ITrafficLog
{
    /// <summary>Default capacity (K8).</summary>
    public const int DefaultCapacity = 5000;

    private readonly Lock _gate = new();
    private readonly Queue<TrafficEntry> _entries;

    public TrafficLog()
        : this(DefaultCapacity)
    {
    }

    public TrafficLog(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _entries = new Queue<TrafficEntry>(capacity);
    }

    /// <inheritdoc />
    public int Capacity { get; }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<TrafficEntry>? EntryAdded;

    /// <inheritdoc />
    public IReadOnlyList<TrafficEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    /// <inheritdoc />
    public void Add(TrafficEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            if (_entries.Count == Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        EntryAdded?.Invoke(this, entry);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
