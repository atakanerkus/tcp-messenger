using System.Globalization;
using TcpMessenger.Core.Traffic;

namespace TcpMessenger.App.ViewModels;

/// <summary>Read-only row of the traffic list.</summary>
public sealed class TrafficEntryViewModel
{
    public TrafficEntryViewModel(TrafficEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
    }

    /// <summary>The underlying log entry.</summary>
    public TrafficEntry Entry { get; }

    /// <summary>Local time with milliseconds.</summary>
    public string Time => Entry.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>"Transmit" or "Receive".</summary>
    public string Direction => Entry.DirectionLabel;

    /// <summary>"Mesaj 1", "Mesaj 2" or "Tanımsız (…)".</summary>
    public string MessageName => Entry.MessageName;

    /// <summary>"Transmit Mesaj 1" — also exposed as the row's automation name.</summary>
    public string Summary => Entry.Summary;

    /// <summary>Number of bytes on the wire.</summary>
    public int Length => Entry.RawBytes.Length;

    public bool IsUndefined => Entry.Kind == TrafficEntryKind.Undefined;

    public bool HasFieldErrors => Entry.Kind == TrafficEntryKind.Message && Entry.Errors.Count > 0;

    public bool IsTransmit => Entry.Direction == TrafficDirection.Transmit;
}
