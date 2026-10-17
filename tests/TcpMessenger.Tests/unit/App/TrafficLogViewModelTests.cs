using TcpMessenger.App.ViewModels;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Tests.TestUtilities;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class TrafficLogViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(3));
    private readonly ImmediateUiDispatcher _dispatcher = new();

    [Fact]
    public void LogEntries_AppearAsRowsThroughTheDispatcher()
    {
        var log = new TrafficLog(capacity: 10);
        var vm = new TrafficLogViewModel(log, _dispatcher);

        log.Add(TrafficEntry.Transmitted(T0, TestMessages.ValidMessage1(), new byte[64], []));
        log.Add(TrafficEntry.Received(T0, TestMessages.ValidMessage2(), new byte[29], []));
        log.Add(TrafficEntry.Undefined(T0, 9, new byte[] { 0, 0, 0, 9 }));

        Assert.Equal(["Transmit Mesaj 1", "Receive Mesaj 2", "Receive Tanımsız (9)"], vm.Entries.Select(e => e.Summary));
        Assert.Equal(1, vm.TransmitCount);
        Assert.Equal(1, vm.ReceiveCount);
        Assert.Equal(1, vm.UndefinedCount);
        Assert.Equal(3, _dispatcher.PostCount);
    }

    [Fact]
    public void ExistingEntries_AreLoadedOnConstruction()
    {
        var log = new TrafficLog(capacity: 10);
        log.Add(TrafficEntry.Transmitted(T0, TestMessages.ValidMessage1(), new byte[64], []));

        var vm = new TrafficLogViewModel(log, _dispatcher);

        Assert.Single(vm.Entries);
        Assert.Equal(1, vm.TransmitCount);
    }

    [Fact]
    public void Rows_AreTrimmedToTheLogCapacity()
    {
        var log = new TrafficLog(capacity: 3);
        var vm = new TrafficLogViewModel(log, _dispatcher);

        for (int i = 0; i < 5; i++)
        {
            log.Add(TrafficEntry.Transmitted(T0.AddSeconds(i), TestMessages.ValidMessage1(), new byte[64], []));
        }

        Assert.Equal(3, vm.Entries.Count);
        Assert.Equal("12:00:02.000", vm.Entries[0].Time);
        Assert.Equal(5, vm.TransmitCount); // counters count everything that happened, not what is still shown
    }

    [Fact]
    public void Clear_EmptiesRowsLogCountersAndSelection()
    {
        var log = new TrafficLog(capacity: 10);
        var vm = new TrafficLogViewModel(log, _dispatcher);
        log.Add(TrafficEntry.Transmitted(T0, TestMessages.ValidMessage1(), new byte[64], []));
        vm.SelectedEntry = vm.Entries[0];

        vm.ClearCommand.Execute(null);

        Assert.Empty(vm.Entries);
        Assert.Equal(0, log.Count);
        Assert.Null(vm.SelectedEntry);
        Assert.Equal(0, vm.TransmitCount);
    }

    [Fact]
    public void Row_ExposesDisplayValues()
    {
        var entry = TrafficEntry.Received(T0, TestMessages.ValidMessage2() with { Altitude = -1 }, new byte[29], [new("Altitude", "x")]);

        var row = new TrafficEntryViewModel(entry);

        Assert.Equal("12:00:00.000", row.Time);
        Assert.Equal("Receive", row.Direction);
        Assert.Equal("Mesaj 2", row.MessageName);
        Assert.Equal(29, row.Length);
        Assert.True(row.HasFieldErrors);
        Assert.False(row.IsUndefined);
        Assert.False(row.IsTransmit);
    }
}
