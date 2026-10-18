using TcpMessenger.App.Services;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class UiBatcherTests
{
    private readonly ManualUiDispatcher _dispatcher = new();
    private readonly List<IReadOnlyList<int>> _flushes = [];
    private readonly UiBatcher<int> _batcher;

    public UiBatcherTests()
    {
        _batcher = new UiBatcher<int>(_dispatcher, batch => _flushes.Add(batch));
    }

    [Fact]
    public void ManyItemsWhileTheUiIsBusy_QueueASingleFlush()
    {
        for (int i = 0; i < 10_000; i++)
        {
            _batcher.Add(i);
        }

        Assert.Equal(1, _dispatcher.QueuedCount);

        _dispatcher.RunAll();

        var batch = Assert.Single(_flushes);
        Assert.Equal(Enumerable.Range(0, 10_000), batch);
    }

    [Fact]
    public void ItemAddedAfterAFlush_SchedulesTheNextOne()
    {
        _batcher.Add(1);
        _dispatcher.RunAll();

        _batcher.Add(2);
        _batcher.Add(3);

        Assert.Equal(1, _dispatcher.QueuedCount);
        _dispatcher.RunAll();
        Assert.Equal([[1], [2, 3]], _flushes);
    }

    [Fact]
    public void ItemAddedDuringAFlush_IsNotLost()
    {
        var flushes = new List<IReadOnlyList<int>>();
        UiBatcher<int>? batcher = null;
        batcher = new UiBatcher<int>(_dispatcher, batch =>
        {
            flushes.Add(batch);
            if (batch.Contains(1))
            {
                batcher!.Add(2); // the network thread delivering while the UI thread is mid-flush
            }
        });

        batcher.Add(1);
        _dispatcher.RunAll();

        Assert.Equal([[1], [2]], flushes);
    }

    [Fact]
    public async Task ConcurrentProducers_EveryItemArrivesExactlyOnce()
    {
        const int producers = 4;
        const int perProducer = 5_000;
        var dispatcher = new ImmediateUiDispatcher();
        var received = new List<int>();
        var batcher = new UiBatcher<int>(dispatcher, batch =>
        {
            lock (received)
            {
                received.AddRange(batch);
            }
        });

        await Task.WhenAll(Enumerable.Range(0, producers).Select(p => Task.Run(() =>
        {
            for (int i = 0; i < perProducer; i++)
            {
                batcher.Add((p * perProducer) + i);
            }
        })));

        Assert.Equal(Enumerable.Range(0, producers * perProducer), received.Order());
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new UiBatcher<int>(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new UiBatcher<int>(_dispatcher, null!));
    }
}
