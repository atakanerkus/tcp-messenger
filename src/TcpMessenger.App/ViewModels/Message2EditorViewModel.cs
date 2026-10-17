using CommunityToolkit.Mvvm.ComponentModel;
using TcpMessenger.App.Abstractions;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.App.ViewModels;

/// <summary>Editor for Mesaj 2 (unit position).</summary>
public sealed partial class Message2EditorViewModel : MessageEditorViewModelBase
{
    public Message2EditorViewModel(IMessengerSession session, IPeriodicSender periodicSender, IMessageValidator validator, IUiDispatcher dispatcher)
        : base(session, periodicSender, validator, dispatcher, defaultIntervalMs: 500)
    {
        UnitReferenceNumber = 1;
        IsPositionValid = true;
        Latitude = 14_373_180;  // Ankara, 39.9255° in 1/360000°
        Longitude = 11_831_868; // Ankara, 32.8663° in 1/360000°
        Altitude = 938;
        Revalidate();
    }

    /// <inheritdoc />
    public override int MessageId => Protocol.Messages.MessageId.Message2;

    /// <inheritdoc />
    public override string Title => Protocol.Messages.MessageId.GetName(MessageId);

    [ObservableProperty]
    public partial int UnitReferenceNumber { get; set; }

    /// <summary>Bound to a check box; written to the wire as 1 or 0.</summary>
    [ObservableProperty]
    public partial bool IsPositionValid { get; set; }

    [ObservableProperty]
    public partial long Latitude { get; set; }

    [ObservableProperty]
    public partial long Longitude { get; set; }

    [ObservableProperty]
    public partial int Altitude { get; set; }

    /// <inheritdoc />
    protected override IMessage BuildMessage() => new Message2(
        UnitReferenceNumber,
        IsPositionValid ? Message2.PositionValid : Message2.PositionInvalid,
        Latitude,
        Longitude,
        Altitude);

    partial void OnUnitReferenceNumberChanged(int value) => Revalidate();

    partial void OnIsPositionValidChanged(bool value) => Revalidate();

    partial void OnLatitudeChanged(long value) => Revalidate();

    partial void OnLongitudeChanged(long value) => Revalidate();

    partial void OnAltitudeChanged(int value) => Revalidate();
}
