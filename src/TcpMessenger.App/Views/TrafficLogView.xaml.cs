using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TcpMessenger.App.ViewModels;

namespace TcpMessenger.App.Views;

/// <summary>Traffic list. The only code-behind is auto-scrolling, which is pure view behaviour.</summary>
public partial class TrafficLogView : UserControl
{
    private TrafficLogViewModel? _viewModel;
    private bool _scrollPending;

    public TrafficLogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
        }

        _viewModel = e.NewValue as TrafficLogViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is not { AutoScroll: true } || e.Action != NotifyCollectionChangedAction.Add || _scrollPending)
        {
            return;
        }

        // One scroll after a whole batch of rows, not a layout pass per row (K14).
        _scrollPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, ScrollToNewest);
    }

    private void ScrollToNewest()
    {
        _scrollPending = false;
        if (_viewModel is { AutoScroll: true, Entries.Count: > 0 } viewModel)
        {
            EntriesList.ScrollIntoView(viewModel.Entries[^1]);
        }
    }
}
