using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcpMessenger.App.Abstractions;
using TcpMessenger.App.Services;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Scheduling;
using TcpMessenger.Core.Session;
using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.App.ViewModels;

/// <summary>Root view model: composes the panels and owns the warning banner.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IMessengerSession _session;
    private readonly IUiDispatcher _dispatcher;

    public MainViewModel(
        IMessengerSession session,
        IPeriodicSender periodicSender,
        IUiDispatcher dispatcher,
        ConnectionViewModel connection,
        Message1EditorViewModel message1,
        Message2EditorViewModel message2,
        TrafficLogViewModel trafficLog,
        MessageDetailViewModel detail)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(periodicSender);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(message1);
        ArgumentNullException.ThrowIfNull(message2);
        ArgumentNullException.ThrowIfNull(trafficLog);
        ArgumentNullException.ThrowIfNull(detail);

        _session = session;
        _dispatcher = dispatcher;
        Connection = connection;
        Message1 = message1;
        Message2 = message2;
        TrafficLog = trafficLog;
        Detail = detail;
        AutoReplyEnabled = session.AutoReplyEnabled;

        // A flood of undefined bytes raises a warning per received chunk: batch them like the log (K14).
        var warnings = new UiBatcher<SessionWarning>(dispatcher, batch => ShowWarnings(batch.Select(w => w.Message).ToList()));
        _session.WarningRaised += (_, warning) => warnings.Add(warning);
        periodicSender.SendFailed += (_, failure) => _dispatcher.Post(() => OnPeriodicSendFailed(failure));
        TrafficLog.PropertyChanged += OnTrafficLogPropertyChanged;
        Connection.PropertyChanged += OnConnectionPropertyChanged;
    }

    public ConnectionViewModel Connection { get; }

    public Message1EditorViewModel Message1 { get; }

    public Message2EditorViewModel Message2 { get; }

    public TrafficLogViewModel TrafficLog { get; }

    public MessageDetailViewModel Detail { get; }

    /// <summary>Window title reflecting the chosen mode, so two instances are told apart.</summary>
    public string WindowTitle => $"TCP Messenger — {Connection.ModeLabel}";

    /// <summary>Mirror of <see cref="IMessengerSession.AutoReplyEnabled"/> (K4).</summary>
    [ObservableProperty]
    public partial bool AutoReplyEnabled { get; set; }

    /// <summary>Text in the warning banner; null hides the banner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    public partial string? WarningText { get; set; }

    /// <summary>How many warnings arrived since the banner was last dismissed.</summary>
    [ObservableProperty]
    public partial int WarningCount { get; set; }

    public bool HasWarning => WarningText is not null;

    [RelayCommand]
    private void DismissWarning()
    {
        WarningText = null;
        WarningCount = 0;
    }

    partial void OnAutoReplyEnabledChanged(bool value) => _session.AutoReplyEnabled = value;

    /// <summary>Shows the newest warning and counts the rest, so the banner reads the same however they arrived.</summary>
    private void ShowWarnings(IReadOnlyList<string> texts)
    {
        WarningCount += texts.Count;
        string text = texts[^1];
        WarningText = WarningCount > 1 ? $"{text}  (+{WarningCount - 1} önceki uyarı)" : text;
    }

    private void OnPeriodicSendFailed(PeriodicSendFailure failure)
    {
        var editor = failure.MessageId == MessageId.Message1 ? (MessageEditorViewModelBase)Message1 : Message2;
        editor.IsPeriodic = false;
        ShowWarnings([$"{MessageId.GetName(failure.MessageId)} periyodik gönderimi durduruldu: {failure.Error.Message}"]);
    }

    private void OnTrafficLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrafficLogViewModel.SelectedEntry))
        {
            Detail.Show(TrafficLog.SelectedEntry?.Entry);
        }
    }

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionViewModel.ModeLabel))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
