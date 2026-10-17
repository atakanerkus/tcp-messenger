using CommunityToolkit.Mvvm.ComponentModel;
using TcpMessenger.App.Abstractions;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.App.ViewModels;

/// <summary>Editor for Mesaj 1 (personnel identity).</summary>
public sealed partial class Message1EditorViewModel : MessageEditorViewModelBase
{
    public Message1EditorViewModel(IMessengerSession session, IPeriodicSender periodicSender, IMessageValidator validator, IUiDispatcher dispatcher)
        : base(session, periodicSender, validator, dispatcher, defaultIntervalMs: 100)
    {
        UnitReferenceNumber = 1;
        FirstName = "Ahmet";
        UnitNo = 1;
        LastName = "Yılmaz";
        Rank = Rank.Lieutenant;
        Revalidate();
    }

    /// <inheritdoc />
    public override int MessageId => Protocol.Messages.MessageId.Message1;

    /// <inheritdoc />
    public override string Title => Protocol.Messages.MessageId.GetName(MessageId);

    /// <summary>Choices for the Rütbe combo box.</summary>
    public IReadOnlyList<RankOption> RankOptions => RankOption.All;

    [ObservableProperty]
    public partial int UnitReferenceNumber { get; set; }

    [ObservableProperty]
    public partial string FirstName { get; set; }

    [ObservableProperty]
    public partial uint UnitNo { get; set; }

    [ObservableProperty]
    public partial string LastName { get; set; }

    [ObservableProperty]
    public partial Rank Rank { get; set; }

    /// <inheritdoc />
    protected override IMessage BuildMessage() => new Message1(UnitReferenceNumber, FirstName ?? string.Empty, UnitNo, LastName ?? string.Empty, Rank);

    partial void OnUnitReferenceNumberChanged(int value) => Revalidate();

    partial void OnFirstNameChanged(string value) => Revalidate();

    partial void OnUnitNoChanged(uint value) => Revalidate();

    partial void OnLastNameChanged(string value) => Revalidate();

    partial void OnRankChanged(Rank value) => Revalidate();
}
