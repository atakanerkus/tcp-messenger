using System.Collections.Concurrent;
using TcpMessenger.App.Abstractions;

namespace TcpMessenger.App.Services;

/// <summary>
/// Collects items raised on any thread and hands them to the UI thread in batches. At most one
/// flush is queued at a time, at a priority below input and rendering (K14).
/// </summary>
/// <remarks>
/// A peer can produce thousands of messages per second: the auto-reply ping-pong (K4), 10 ms
/// periodic sends, a flood of undefined bytes. Posting every event separately queues work faster
/// than the UI thread can run it, and the window stops responding.
/// </remarks>
public sealed class UiBatcher<T>
{
    private readonly ConcurrentQueue<T> _pending = new();
    private readonly IUiDispatcher _dispatcher;
    private readonly Action<IReadOnlyList<T>> _flush;
    private int _flushScheduled;

    public UiBatcher(IUiDispatcher dispatcher, Action<IReadOnlyList<T>> flush)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(flush);

        _dispatcher = dispatcher;
        _flush = flush;
    }

    /// <summary>Thread-safe. The item reaches the UI thread with the next flush.</summary>
    public void Add(T item)
    {
        _pending.Enqueue(item);

        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
        {
            _dispatcher.PostBackground(Flush);
        }
    }

    private void Flush()
    {
        // Reset before draining: an item added meanwhile schedules the next flush instead of being stranded.
        Interlocked.Exchange(ref _flushScheduled, 0);

        var batch = new List<T>();
        while (_pending.TryDequeue(out var item))
        {
            batch.Add(item);
        }

        if (batch.Count > 0)
        {
            _flush(batch);
        }
    }
}
