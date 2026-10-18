using TcpMessenger.App.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>
/// <see cref="IUiDispatcher"/> that only queues work until the test runs it, standing in for a
/// UI thread that is busy while events keep arriving from the network.
/// </summary>
internal sealed class ManualUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _queued = new();

    public int QueuedCount => _queued.Count;

    public void Post(Action action) => _queued.Enqueue(action);

    public void PostBackground(Action action) => _queued.Enqueue(action);

    /// <summary>Runs queued work, including anything that work queues in turn.</summary>
    public void RunAll()
    {
        while (_queued.TryDequeue(out var action))
        {
            action();
        }
    }
}
