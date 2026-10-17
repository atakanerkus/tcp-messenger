using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcpMessenger.App.Abstractions;
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

        foreach (var entry in _log.Snapshot())
        {
            Append(entry);
        }

        _log.EntryAdded += (_, entry) => dispatcher.Post(() => Append(entry));
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

    private void Append(TrafficEntry entry)
    {
        Entries.Add(new TrafficEntryViewModel(entry));
        while (Entries.Count > _log.Capacity)
        {
            Entries.RemoveAt(0);
        }

        if (entry.Kind == TrafficEntryKind.Undefined)
        {
            UndefinedCount++;
        }
        else if (entry.Direction == TrafficDirection.Transmit)
        {
            TransmitCount++;
        }
        else
        {
            ReceiveCount++;
        }
    }
}
