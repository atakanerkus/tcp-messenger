using System.Windows.Threading;
using TcpMessenger.App.Abstractions;

namespace TcpMessenger.App.Services;

/// <summary><see cref="IUiDispatcher"/> over a WPF <see cref="Dispatcher"/>.</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfUiDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
        }
    }
}
