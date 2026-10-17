using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcpMessenger.App.Abstractions;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Session;

namespace TcpMessenger.App.ViewModels;

/// <summary>Server/client selection, address, Başlat/Durdur and the status line.</summary>
public sealed partial class ConnectionViewModel : ObservableObject
{
    private readonly IMessengerSession _session;
    private readonly IUiDispatcher _dispatcher;

    public ConnectionViewModel(IMessengerSession session, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _session = session;
        _dispatcher = dispatcher;
        IsServerMode = true;
        Host = "127.0.0.1";
        Port = 5000;
        State = session.State;
        StatusText = DescribeState(session.State);

        _session.StateChanged += (_, state) => _dispatcher.Post(() =>
        {
            State = state;
            StatusText = DescribeState(state);
        });
    }

    /// <summary>True for server mode, false for client mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeLabel))]
    [NotifyPropertyChangedFor(nameof(IsClientMode))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsServerMode { get; set; }

    /// <summary>Server address to connect to (client mode only).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial string Host { get; set; }

    /// <summary>Port to listen on or connect to.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial int Port { get; set; }

    /// <summary>Mirror of the session state, updated on the UI thread.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial SessionState State { get; set; }

    /// <summary>Human readable status line.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>Why the last Başlat failed, null otherwise.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    public bool IsClientMode => !IsServerMode;

    public string ModeLabel => IsServerMode ? "Server" : "Client";

    public bool CanEditSettings => State == SessionState.Idle;

    public bool IsConnected => State == SessionState.Connected;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        ErrorText = null;
        try
        {
            if (IsServerMode)
            {
                await _session.StartServerAsync(Port);
            }
            else
            {
                await _session.ConnectAsync(Host.Trim(), Port);
            }
        }
        catch (Exception ex)
        {
            // UI boundary: surface the reason (port in use, connection refused, timeout, ...).
            ErrorText = ex.Message;
        }
    }

    private bool CanStart() =>
        State == SessionState.Idle
        && Port is >= 1 and <= 65535
        && (IsServerMode || !string.IsNullOrWhiteSpace(Host));

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => _session.StopAsync();

    private bool CanStop() => State != SessionState.Idle;

    private string DescribeState(SessionState state) => state switch
    {
        SessionState.Idle => "Bağlı değil",
        SessionState.Listening => $"Dinleniyor: port {_session.LocalPort}, istemci bekleniyor…",
        SessionState.Connecting => $"Bağlanıyor: {Host}:{Port}…",
        SessionState.Connected => $"Bağlı: {_session.RemoteEndPoint}",
        _ => state.ToString(),
    };
}
