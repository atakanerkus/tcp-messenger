using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcpMessenger.App.Abstractions;
using TcpMessenger.App.Services;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Traffic;

namespace TcpMessenger.App.ViewModels;

/// <summary>UI mirror of <see cref="ITrafficLog"/>, kept on the UI thread and bounded to the log's capacity.</summary>
public sealed partial class TrafficLogViewModel : ObservableObject
{
    private readonly ITrafficLog _log;

    public TrafficLogViewModel(ITrafficLog log, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _log = log;
        AutoScroll = true;

        AppendBatch(_log.Snapshot());

        // Batched, not one dispatcher post per entry: see UiBatcher and K14.
        var incoming = new UiBatcher<TrafficEntry>(dispatcher, AppendBatch);
        _log.EntryAdded += (_, entry) => incoming.Add(entry);
    }

    /// <summary>Rows, oldest first.</summary>
    public ObservableCollection<TrafficEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    public partial TrafficEntryViewModel? SelectedEntry { get; set; }

    /// <summary>Keep the newest row in view.</summary>
    [ObservableProperty]
    public partial bool AutoScroll { get; set; }

    [ObservableProperty]
    public partial int TransmitCount { get; set; }

    [ObservableProperty]
    public partial int ReceiveCount { get; set; }

    [ObservableProperty]
    public partial int UndefinedCount { get; set; }

    [RelayCommand]
    private void Clear()
    {
        _log.Clear();
        Entries.Clear();
        SelectedEntry = null;
        TransmitCount = 0;
        ReceiveCount = 0;
        UndefinedCount = 0;
    }

    private void AppendBatch(IReadOnlyList<TrafficEntry> batch)
    {
        int transmits = 0, receives = 0, undefined = 0;
        foreach (var entry in batch)
        {
            if (entry.Kind == TrafficEntryKind.Undefined)
            {
                undefined++;
            }
            else if (entry.Direction == TrafficDirection.Transmit)
            {
                transmits++;
            }
            else
            {
                receives++;
            }
        }

        TransmitCount += transmits;
        ReceiveCount += receives;
        UndefinedCount += undefined;

        // Only the newest Capacity entries can end up on screen: skip older ones instead of adding
        // and immediately removing them, so one flush touches at most twice Capacity rows.
        int firstShown = Math.Max(0, batch.Count - _log.Capacity);
        int overflow = Entries.Count + (batch.Count - firstShown) - _log.Capacity;
        if (overflow > 0 && overflow >= Entries.Count)
        {
            Entries.Clear();
        }
        else
        {
            for (int i = 0; i < overflow; i++)
            {
                Entries.RemoveAt(0);
            }
        }

        for (int i = firstShown; i < batch.Count; i++)
        {
            Entries.Add(new TrafficEntryViewModel(batch[i]));
        }
    }
}
