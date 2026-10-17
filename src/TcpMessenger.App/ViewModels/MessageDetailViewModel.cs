using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TcpMessenger.App.Services;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Validation;

namespace TcpMessenger.App.ViewModels;

/// <summary>One row of the field table in the detail pane.</summary>
/// <param name="Name">Field name as written in the task specification.</param>
/// <param name="Value">Formatted value.</param>
/// <param name="Error">Validator message when the value is out of range, else null.</param>
public sealed record DetailField(string Name, string Value, string? Error)
{
    public bool HasError => Error is not null;
}

/// <summary>Shows the content of the traffic entry selected in the list.</summary>
public sealed partial class MessageDetailViewModel : ObservableObject
{
    public MessageDetailViewModel()
    {
        Header = "Listeden bir kayıt seçin";
        HexDump = string.Empty;
    }

    /// <summary>"Receive Mesaj 1 · 12:00:01.234 · 64 byte" or a hint when nothing is selected.</summary>
    [ObservableProperty]
    public partial string Header { get; set; }

    /// <summary>Hex dump of the raw bytes.</summary>
    [ObservableProperty]
    public partial string HexDump { get; set; }

    /// <summary>True while an entry is shown.</summary>
    [ObservableProperty]
    public partial bool HasEntry { get; set; }

    /// <summary>Field rows of the shown entry.</summary>
    public ObservableCollection<DetailField> Fields { get; } = [];

    /// <summary>Replaces the pane content; null clears it.</summary>
    public void Show(TrafficEntry? entry)
    {
        Fields.Clear();

        if (entry is null)
        {
            Header = "Listeden bir kayıt seçin";
            HexDump = string.Empty;
            HasEntry = false;
            return;
        }

        Header = string.Format(
            CultureInfo.InvariantCulture,
            "{0} · {1:HH:mm:ss.fff} · {2} byte",
            entry.Summary,
            entry.Timestamp,
            entry.RawBytes.Length);
        HexDump = Services.HexDump.Format(entry.RawBytes.Span);
        HasEntry = true;

        foreach (var field in BuildFields(entry))
        {
            Fields.Add(field);
        }
    }

    private static IEnumerable<DetailField> BuildFields(TrafficEntry entry)
    {
        var errors = entry.Errors.ToDictionary(e => e.FieldName, e => e.Message);

        switch (entry.Message)
        {
            case Message1 m:
                yield return new DetailField("Mesaj ID", Invariant(m.Id), null);
                yield return Field("Birlik Referans Numarası", Invariant(m.UnitReferenceNumber), nameof(Message1.UnitReferenceNumber), errors);
                yield return Field("Adı", m.FirstName, nameof(Message1.FirstName), errors);
                yield return Field("Unit No", Invariant(m.UnitNo), nameof(Message1.UnitNo), errors);
                yield return Field("Soyadı", m.LastName, nameof(Message1.LastName), errors);
                yield return Field("Rütbe", $"{Invariant((short)m.Rank)} – {m.Rank.ToDisplayName()}", nameof(Message1.Rank), errors);
                break;

            case Message2 m:
                yield return new DetailField("Mesaj ID", Invariant(m.Id), null);
                yield return Field("Birlik Referans Numarası", Invariant(m.UnitReferenceNumber), nameof(Message2.UnitReferenceNumber), errors);
                yield return Field("Birlik Konum Bilgisi Geçerliliği", DescribeValidity(m.PositionValidity), nameof(Message2.PositionValidity), errors);
                yield return Field("Enlem", Invariant(m.Latitude), nameof(Message2.Latitude), errors);
                yield return Field("Boylam", Invariant(m.Longitude), nameof(Message2.Longitude), errors);
                yield return Field("Yükseklik", Invariant(m.Altitude), nameof(Message2.Altitude), errors);
                break;

            default:
                int id = entry.RawBytes.Length >= 4
                    ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(entry.RawBytes.Span)
                    : 0;
                yield return new DetailField(
                    "Mesaj ID",
                    string.Format(CultureInfo.InvariantCulture, "{0} (0x{0:X8})", id),
                    "Tanımlı bir mesaj kimliği değil; tampondaki veri atıldı.");
                break;
        }
    }

    private static DetailField Field(string name, string value, string propertyName, Dictionary<string, string> errors) =>
        new(name, value, errors.GetValueOrDefault(propertyName));

    private static string DescribeValidity(byte value) => value switch
    {
        Message2.PositionValid => "1 – True",
        Message2.PositionInvalid => "0 – False",
        _ => Invariant(value) + " – geçersiz",
    };

    private static string Invariant<T>(T value)
        where T : IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);
}
