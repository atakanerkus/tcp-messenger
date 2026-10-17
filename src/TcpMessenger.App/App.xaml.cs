using System.Windows;
using System.Windows.Threading;
using TcpMessenger.App.Services;
using TcpMessenger.App.ViewModels;
using TcpMessenger.App.Views;
using TcpMessenger.Core.Scheduling;
using TcpMessenger.Core.Session;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Transport.Tcp;

namespace TcpMessenger.App;

/// <summary>Composition root: builds the object graph by hand (no container needed for this size) and shows the window.</summary>
public partial class App : Application
{
    private MessengerSession? _session;
    private PeriodicSender? _periodicSender;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var options = StartupOptions.Parse(e.Args);
        var dispatcher = new WpfUiDispatcher(Dispatcher);

        // Protocol + Core
        var validator = new MessageValidator();
        var formMessages = new FormMessageProvider();
        var log = new TrafficLog();
        _session = new MessengerSession(
            new TcpServerTransport(),
            new TcpClientTransport(),
            new BinaryMessageCodec(),
            new FrameParser(),
            validator,
            formMessages,
            log)
        {
            AutoReplyEnabled = options.AutoReply,
        };
        _periodicSender = new PeriodicSender(_session, formMessages);

        // View models
        var message1 = new Message1EditorViewModel(_session, _periodicSender, validator, dispatcher);
        var message2 = new Message2EditorViewModel(_session, _periodicSender, validator, dispatcher);
        formMessages.Register(message1);
        formMessages.Register(message2);

        var connection = new ConnectionViewModel(_session, dispatcher)
        {
            IsServerMode = options.Mode == SessionMode.Server,
            Host = options.Host,
            Port = options.Port,
        };

        var main = new MainViewModel(
            _session,
            _periodicSender,
            dispatcher,
            connection,
            message1,
            message2,
            new TrafficLogViewModel(log, dispatcher),
            new MessageDetailViewModel());

        var window = new MainWindow { DataContext = main };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Everything below uses ConfigureAwait(false), so blocking here cannot deadlock the dispatcher.
        _periodicSender?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Last line of defence for the live demo: show the error instead of tearing the window down.
        MessageBox.Show(e.Exception.ToString(), "Beklenmeyen hata", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
