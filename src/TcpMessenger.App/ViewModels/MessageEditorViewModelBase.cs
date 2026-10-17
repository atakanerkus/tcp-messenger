using System.Collections;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcpMessenger.App.Abstractions;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Session;
using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.App.ViewModels;

/// <summary>
/// Shared behaviour of the two message editors: field validation through the protocol's
/// <see cref="IMessageValidator"/> (so the UI can never disagree with the wire rules), the Gönder
/// command and the periodic-send toggle. Derived classes only declare their fields and build the record.
/// </summary>
public abstract partial class MessageEditorViewModelBase : ObservableObject, INotifyDataErrorInfo
{
    private readonly IMessengerSession _session;
    private readonly IPeriodicSender _periodicSender;
    private readonly IMessageValidator _validator;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, List<string>> _errors = [];

    protected MessageEditorViewModelBase(
        IMessengerSession session,
        IPeriodicSender periodicSender,
        IMessageValidator validator,
        IUiDispatcher dispatcher,
        int defaultIntervalMs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(periodicSender);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _session = session;
        _periodicSender = periodicSender;
        _validator = validator;
        _dispatcher = dispatcher;
        IntervalMs = defaultIntervalMs;

        _session.StateChanged += (_, _) => _dispatcher.Post(() =>
        {
            SendCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsConnected));
        });
    }

    /// <summary>Wire identifier of the message this editor produces.</summary>
    public abstract int MessageId { get; }

    /// <summary>Tab title, e.g. "Mesaj 1".</summary>
    public abstract string Title { get; }

    /// <summary>True while a peer is connected; sending is only possible then.</summary>
    public bool IsConnected => _session.State == SessionState.Connected;

    /// <summary>The message built from the current fields, or null while any field is out of range.</summary>
    public IMessage? CurrentMessage => HasErrors ? null : BuildMessage();

    /// <summary>Error text shown under the Gönder button after a failed send; cleared on success.</summary>
    [ObservableProperty]
    public partial string? SendError { get; set; }

    /// <summary>Periodic sending on/off. Turning it on starts the schedule immediately.</summary>
    [ObservableProperty]
    public partial bool IsPeriodic { get; set; }

    /// <summary>Period in milliseconds; at least <see cref="IPeriodicSender.MinimumInterval"/>.</summary>
    [ObservableProperty]
    public partial int IntervalMs { get; set; }

    /// <summary>Validation text for <see cref="IntervalMs"/>, null when acceptable.</summary>
    [ObservableProperty]
    public partial string? IntervalError { get; set; }

    /// <inheritdoc />
    public bool HasErrors => _errors.Count > 0;

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <inheritdoc />
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return _errors.Values.SelectMany(e => e).ToList();
        }

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Array.Empty<string>();
    }

    /// <summary>Builds the record from the current field values without validating.</summary>
    protected abstract IMessage BuildMessage();

    /// <summary>Re-runs the protocol validator and republishes per-field errors. Call from every field setter.</summary>
    protected void Revalidate()
    {
        var fresh = _validator.Validate(BuildMessage())
            .GroupBy(e => e.FieldName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToList());

        var touched = new HashSet<string>(_errors.Keys);
        touched.UnionWith(fresh.Keys);

        _errors.Clear();
        foreach (var (field, messages) in fresh)
        {
            _errors[field] = messages;
        }

        foreach (string field in touched)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
        }

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(CurrentMessage));
        SendCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        SendError = null;
        try
        {
            await _session.SendAsync(BuildMessage());
        }
        catch (Exception ex)
        {
            // UI boundary: show the reason instead of crashing the window.
            SendError = ex.Message;
        }
    }

    private bool CanSend() => !HasErrors && IsConnected;

    partial void OnIsPeriodicChanged(bool value)
    {
        if (value)
        {
            StartPeriodic();
        }
        else
        {
            _periodicSender.Stop(MessageId);
        }
    }

    partial void OnIntervalMsChanged(int value)
    {
        IntervalError = ValidateInterval(value);
        if (IsPeriodic && IntervalError is null)
        {
            StartPeriodic();
        }
    }

    private void StartPeriodic()
    {
        IntervalError = ValidateInterval(IntervalMs);
        if (IntervalError is not null)
        {
            IsPeriodic = false;
            return;
        }

        _periodicSender.Start(MessageId, TimeSpan.FromMilliseconds(IntervalMs));
    }

    private string? ValidateInterval(int milliseconds) =>
        milliseconds < _periodicSender.MinimumInterval.TotalMilliseconds
            ? $"Aralık en az {_periodicSender.MinimumInterval.TotalMilliseconds:0} ms olmalı."
            : null;
}
