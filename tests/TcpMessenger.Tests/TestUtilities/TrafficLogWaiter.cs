using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Traffic;

namespace TcpMessenger.Tests.TestUtilities;

/// <summary>
/// Completes when an entry matching a predicate is added to a log. Create it <b>before</b> the
/// action under test so that nothing is missed.
/// </summary>
internal sealed class TrafficLogWaiter : IDisposable
{
    private readonly ITrafficLog _log;
    private readonly Func<TrafficEntry, bool> _predicate;
    private readonly TaskCompletionSource<TrafficEntry> _matched = AsyncTestHelpers.NewSignal<TrafficEntry>();

    public TrafficLogWaiter(ITrafficLog log, Func<TrafficEntry, bool> predicate)
    {
        _log = log;
        _predicate = predicate;
        _log.EntryAdded += OnEntryAdded;
    }

    public Task<TrafficEntry> Entry => _matched.Task;

    public void Dispose() => _log.EntryAdded -= OnEntryAdded;

    private void OnEntryAdded(object? sender, TrafficEntry entry)
    {
        if (_predicate(entry))
        {
            _matched.TrySetResult(entry);
        }
    }
}
