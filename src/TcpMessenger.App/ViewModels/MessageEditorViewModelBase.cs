using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TcpMessenger.App.Abstractions;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.Session;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Validation;

namespace TcpMessenger.App.ViewModels;

/// <summary>
/// Shared behaviour of the two message editors: field validation through the protocol's
/// <see cref="IMessageValidator"/> (so the UI can never disagree with the wire rules), the Gönder
/// command and the periodic-send toggle. Derived classes only declare their fields and build the record.
/// </summary>
/// <remarks>
/// Numeric fields hold the text exactly as typed and are parsed here, not by the WPF binding: a binding
/// that cannot convert "" or "abc" leaves the view model untouched, so the old value would still be sent
/// while the box looks empty (K15).
/// </remarks>
public abstract partial class MessageEditorViewModelBase : ObservableObject, INotifyDataErrorInfo
{
    private readonly IMessengerSession _session;
    private readonly IPeriodicSender _periodicSender;
    private readonly IMessageValidator _validator;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, List<string>> _errors = [];
    private IMessage? _currentMessage;

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
        IntervalMs = defaultIntervalMs.ToString(CultureInfo.InvariantCulture);

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

    /// <summary>
    /// The message built from the current fields, or null while any field is empty, not a number or out
    /// of range. Read from thread-pool threads by the automatic reply and the periodic sender.
    /// </summary>
    public IMessage? CurrentMessage => Volatile.Read(ref _currentMessage);

    /// <summary>Error text shown under the Gönder button after a failed send; cleared on success.</summary>
    [ObservableProperty]
    public partial string? SendError { get; set; }

    /// <summary>Periodic sending on/off. Turning it on starts the schedule immediately.</summary>
    [ObservableProperty]
    public partial bool IsPeriodic { get; set; }

    /// <summary>Period in milliseconds as typed; at least <see cref="IPeriodicSender.MinimumInterval"/>.</summary>
    [ObservableProperty]
    public partial string IntervalMs { get; set; }

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

    /// <summary>
    /// Builds the record from the current field values without range checks. Adds an error for every
    /// numeric field whose text cannot be parsed and returns null in that case.
    /// </summary>
    protected abstract IMessage? BuildMessage(List<FieldError> errors);

    /// <summary>
    /// Parses a numeric field. Empty text and text that is not an integer of type
    /// <typeparamref name="T"/> become an error on that field; null is returned then.
    /// </summary>
    protected static T? ParseInteger<T>(string? text, string fieldName, List<FieldError> errors)
        where T : struct, IBinaryInteger<T>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add(new FieldError(fieldName, "Değer boş olamaz."));
            return null;
        }

        if (T.TryParse(text, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new FieldError(fieldName, "Geçerli bir tam sayı değil ya da alanın tipine sığmıyor."));
        return null;
    }

    /// <summary>Re-parses and re-validates every field and republishes per-field errors. Call from every field setter.</summary>
    protected void Revalidate()
    {
        var errors = new List<FieldError>();
        var message = BuildMessage(errors);
        if (message is not null)
        {
            errors.AddRange(_validator.Validate(message));
        }

        Volatile.Write(ref _currentMessage, errors.Count == 0 ? message : null);

        var fresh = errors
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
        var message = CurrentMessage;
        if (message is null)
        {
            return; // CanSend keeps the button disabled; guards a click racing a keystroke
        }

        try
        {
            await _session.SendAsync(message);
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

    partial void OnIntervalMsChanged(string value)
    {
        IntervalError = ValidateInterval(value, out _);
        if (IsPeriodic && IntervalError is null)
        {
            StartPeriodic();
        }
    }

    private void StartPeriodic()
    {
        IntervalError = ValidateInterval(IntervalMs, out int milliseconds);
        if (IntervalError is not null)
        {
            IsPeriodic = false;
            return;
        }

        _periodicSender.Start(MessageId, TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>
    /// Null when the text is a whole number of milliseconds at or above the minimum. An invalid value never
    /// starts a schedule; a schedule that is already running keeps its last valid period (K15).
    /// </summary>
    private string? ValidateInterval(string? text, out int milliseconds)
    {
        var errors = new List<FieldError>();
        milliseconds = ParseInteger<int>(text, nameof(IntervalMs), errors) ?? 0;
        if (errors.Count > 0)
        {
            return errors[0].Message;
        }

        return milliseconds < _periodicSender.MinimumInterval.TotalMilliseconds
            ? $"Aralık en az {_periodicSender.MinimumInterval.TotalMilliseconds:0} ms olmalı."
            : null;
    }
}
