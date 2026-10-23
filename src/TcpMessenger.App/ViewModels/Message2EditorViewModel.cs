using CommunityToolkit.Mvvm.ComponentModel;
using TcpMessenger.App.Abstractions;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Validation;

namespace TcpMessenger.App.ViewModels;

/// <summary>Editor for Mesaj 2 (unit position).</summary>
public sealed partial class Message2EditorViewModel : MessageEditorViewModelBase
{
    public Message2EditorViewModel(IMessengerSession session, IPeriodicSender periodicSender, IMessageValidator validator, IUiDispatcher dispatcher)
        : base(session, periodicSender, validator, dispatcher, defaultIntervalMs: 500)
    {
        UnitReferenceNumber = "1";
        IsPositionValid = true;
        Latitude = "14373180";  // Ankara, 39.9255° in 1/360000°
        Longitude = "11831868"; // Ankara, 32.8663° in 1/360000°
        Altitude = "938";
        Revalidate();
    }

    /// <inheritdoc />
    public override int MessageId => Protocol.Messages.MessageId.Message2;

    /// <inheritdoc />
    public override string Title => Protocol.Messages.MessageId.GetName(MessageId);

    /// <summary>Text as typed; parsed as <see cref="int"/>.</summary>
    [ObservableProperty]
    public partial string UnitReferenceNumber { get; set; }

    /// <summary>Bound to a check box; written to the wire as 1 or 0.</summary>
    [ObservableProperty]
    public partial bool IsPositionValid { get; set; }

    /// <summary>Text as typed; parsed as <see cref="long"/>.</summary>
    [ObservableProperty]
    public partial string Latitude { get; set; }

    /// <summary>Text as typed; parsed as <see cref="long"/>.</summary>
    [ObservableProperty]
    public partial string Longitude { get; set; }

    /// <summary>Text as typed; parsed as <see cref="int"/>.</summary>
    [ObservableProperty]
    public partial string Altitude { get; set; }

    /// <inheritdoc />
    protected override IMessage? BuildMessage(List<FieldError> errors)
    {
        int? unitReferenceNumber = ParseInteger<int>(UnitReferenceNumber, nameof(UnitReferenceNumber), errors);
        long? latitude = ParseInteger<long>(Latitude, nameof(Latitude), errors);
        long? longitude = ParseInteger<long>(Longitude, nameof(Longitude), errors);
        int? altitude = ParseInteger<int>(Altitude, nameof(Altitude), errors);

        if (unitReferenceNumber is null || latitude is null || longitude is null || altitude is null)
        {
            return null;
        }

        return new Message2(
            unitReferenceNumber.Value,
            IsPositionValid ? Message2.PositionValid : Message2.PositionInvalid,
            latitude.Value,
            longitude.Value,
            altitude.Value);
    }

    partial void OnUnitReferenceNumberChanged(string value) => Revalidate();

    partial void OnIsPositionValidChanged(bool value) => Revalidate();

    partial void OnLatitudeChanged(string value) => Revalidate();

    partial void OnLongitudeChanged(string value) => Revalidate();

    partial void OnAltitudeChanged(string value) => Revalidate();
}
