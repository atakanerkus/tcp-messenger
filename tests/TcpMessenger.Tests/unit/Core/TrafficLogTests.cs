using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities;

namespace TcpMessenger.Tests.Unit.Core;

[Trait("Category", "Unit")]
public sealed class TrafficLogTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Add_RaisesEntryAddedWithTheEntry()
    {
        var log = new TrafficLog(capacity: 10);
        TrafficEntry? raised = null;
        log.EntryAdded += (_, e) => raised = e;
        var entry = Transmit(0);

        log.Add(entry);

        Assert.Same(entry, raised);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void Add_BeyondCapacity_DropsOldestFirst()
    {
        var log = new TrafficLog(capacity: 3);

        for (int i = 0; i < 5; i++)
        {
            log.Add(Transmit(i));
        }

        Assert.Equal(3, log.Count);
        Assert.Equal([2, 3, 4], log.Snapshot().Select(e => e.Timestamp.Second));
    }

    [Fact]
    public void Snapshot_IsOldestFirstAndIndependentOfLaterAdds()
    {
        var log = new TrafficLog(capacity: 10);
        log.Add(Transmit(0));
        log.Add(Transmit(1));

        var snapshot = log.Snapshot();
        log.Add(Transmit(2));

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var log = new TrafficLog(capacity: 10);
        log.Add(Transmit(0));

        log.Clear();

        Assert.Equal(0, log.Count);
        Assert.Empty(log.Snapshot());
    }

    [Fact]
    public void DefaultCapacity_Is5000()
    {
        Assert.Equal(5000, new TrafficLog().Capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrafficLog(capacity));
    }

    [Fact]
    public async Task Add_FromManyThreads_NeverExceedsCapacity()
    {
        var log = new TrafficLog(capacity: 100);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                log.Add(Transmit(i % 60));
            }
        })));

        Assert.Equal(100, log.Count);
    }

    [Fact]
    public void Entry_SummaryAndLabels_FollowTaskWording()
    {
        var transmit = TrafficEntry.Transmitted(T0, TestMessages.ValidMessage1(), new byte[64], []);
        var receive = TrafficEntry.Received(T0, TestMessages.ValidMessage2(), new byte[29], []);
        var undefined = TrafficEntry.Undefined(T0, 7, new byte[] { 0, 0, 0, 7 });

        Assert.Equal("Transmit Mesaj 1", transmit.Summary);
        Assert.Equal("Receive Mesaj 2", receive.Summary);
        Assert.Equal("Receive Tanımsız (7)", undefined.Summary);
        Assert.True(transmit.IsValid);
        Assert.False(undefined.IsValid);
    }

    [Fact]
    public void Entry_WithFieldErrors_IsNotValidButStillAMessage()
    {
        var entry = TrafficEntry.Received(T0, TestMessages.ValidMessage2(), new byte[29], [new FieldError("Altitude", "x")]);

        Assert.Equal(TrafficEntryKind.Message, entry.Kind);
        Assert.False(entry.IsValid);
    }

    private static TrafficEntry Transmit(int second) =>
        TrafficEntry.Transmitted(T0.AddSeconds(second), TestMessages.ValidMessage1(), new byte[64], []);
}
