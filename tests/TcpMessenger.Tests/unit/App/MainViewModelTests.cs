using TcpMessenger.App.ViewModels;
using TcpMessenger.Core.Session;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class MainViewModelTests
{
    private readonly FakeMessengerSession _session = new();
    private readonly FakePeriodicSender _periodic = new();
    private readonly ImmediateUiDispatcher _dispatcher = new();
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        var validator = new MessageValidator();
        _vm = new MainViewModel(
            _session,
            _periodic,
            _dispatcher,
            new ConnectionViewModel(_session, _dispatcher),
            new Message1EditorViewModel(_session, _periodic, validator, _dispatcher),
            new Message2EditorViewModel(_session, _periodic, validator, _dispatcher),
            new TrafficLogViewModel(_session.Log, _dispatcher),
            new MessageDetailViewModel());
    }

    [Fact]
    public void AutoReply_MirrorsTheSessionBothWays()
    {
        Assert.True(_vm.AutoReplyEnabled);

        _vm.AutoReplyEnabled = false;

        Assert.False(_session.AutoReplyEnabled);
    }

    [Fact]
    public void SessionWarning_ShowsBannerAndCounts()
    {
        Assert.False(_vm.HasWarning);

        _session.RaiseWarning(SessionWarningKind.UndefinedMessageReceived, "Tanımsız mesaj alındı: ID 9");
        _session.RaiseWarning(SessionWarningKind.ClientRejected, "Yeni bağlantı reddedildi");

        Assert.True(_vm.HasWarning);
        Assert.Equal(2, _vm.WarningCount);
        Assert.StartsWith("Yeni bağlantı reddedildi", _vm.WarningText, StringComparison.Ordinal);
        Assert.Contains("+1", _vm.WarningText, StringComparison.Ordinal);
    }

    [Fact]
    public void WarningFlood_WhileTheUiIsBusy_ShowsTheNewestAndCountsAll()
    {
        var busyUi = new ManualUiDispatcher();
        var validator = new MessageValidator();
        var vm = new MainViewModel(
            _session,
            _periodic,
            busyUi,
            new ConnectionViewModel(_session, busyUi),
            new Message1EditorViewModel(_session, _periodic, validator, busyUi),
            new Message2EditorViewModel(_session, _periodic, validator, busyUi),
            new TrafficLogViewModel(_session.Log, busyUi),
            new MessageDetailViewModel());
        busyUi.RunAll();

        for (int i = 1; i <= 1000; i++)
        {
            _session.RaiseWarning(SessionWarningKind.UndefinedMessageReceived, $"Tanımsız mesaj alındı: ID {i}");
        }

        Assert.Equal(1, busyUi.QueuedCount);
        busyUi.RunAll();

        Assert.Equal(1000, vm.WarningCount);
        Assert.Equal("Tanımsız mesaj alındı: ID 1000  (+999 önceki uyarı)", vm.WarningText);
    }

    [Fact]
    public void DismissWarning_HidesBannerAndResetsCount()
    {
        _session.RaiseWarning(SessionWarningKind.ConnectionClosed, "Bağlantı koptu");

        _vm.DismissWarningCommand.Execute(null);

        Assert.False(_vm.HasWarning);
        Assert.Null(_vm.WarningText);
        Assert.Equal(0, _vm.WarningCount);
    }

    [Fact]
    public void PeriodicSendFailure_UnchecksTheEditorAndWarns()
    {
        _vm.Message2.IsPeriodic = true;

        _periodic.RaiseSendFailed(MessageId.Message2, new IOException("link down"));

        Assert.False(_vm.Message2.IsPeriodic);
        Assert.True(_vm.Message1.IsPeriodic == false);
        Assert.Contains("Mesaj 2", _vm.WarningText, StringComparison.Ordinal);
        Assert.Contains("link down", _vm.WarningText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectingARow_ShowsItInTheDetailPane()
    {
        _session.Log.Add(TrafficEntry.Received(DateTimeOffset.Now, TestMessages.ValidMessage1(), new byte[64], []));

        _vm.TrafficLog.SelectedEntry = _vm.TrafficLog.Entries[0];

        Assert.True(_vm.Detail.HasEntry);
        Assert.Equal(6, _vm.Detail.Fields.Count);

        _vm.TrafficLog.SelectedEntry = null;
        Assert.False(_vm.Detail.HasEntry);
    }

    [Fact]
    public void WindowTitle_FollowsTheMode()
    {
        var titles = new List<string>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle))
            {
                titles.Add(_vm.WindowTitle);
            }
        };

        _vm.Connection.IsServerMode = false;

        Assert.Equal("TCP Messenger — Client", Assert.Single(titles));
    }
}
