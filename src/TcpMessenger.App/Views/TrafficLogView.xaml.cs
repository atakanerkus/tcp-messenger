using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using TcpMessenger.App.ViewModels;

namespace TcpMessenger.App.Views;

/// <summary>Traffic list. The only code-behind is auto-scrolling, which is pure view behaviour.</summary>
public partial class TrafficLogView : UserControl
{
    private TrafficLogViewModel? _viewModel;

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
        if (_viewModel is { AutoScroll: true } && e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 })
        {
            EntriesList.ScrollIntoView(e.NewItems[^1]);
        }
    }
}
