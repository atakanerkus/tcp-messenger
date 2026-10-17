using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace TcpMessenger.Tests.E2E.Fixtures;

/// <summary>
/// Page object over the main window. Every control is reached by the AutomationId set in XAML,
/// so the tests read like the demo script rather than like a UI tree walk.
/// </summary>
internal sealed class MainWindowDriver
{
    public static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(10);

    private readonly Window _window;

    public MainWindowDriver(Window window)
    {
        _window = window;
    }

    public string Title => _window.Title;

    // ---- connection panel --------------------------------------------------------------------

    public void SelectServerMode() => Find("ModeServerRadio").AsRadioButton().IsChecked = true;

    public void SelectClientMode() => Find("ModeClientRadio").AsRadioButton().IsChecked = true;

    public void SetHost(string host) => SetText("HostTextBox", host);

    public void SetPort(int port) => SetText("PortTextBox", port.ToString(CultureInfo.InvariantCulture));

    public void ClickStart() => Find("StartButton").AsButton().Invoke();

    public void ClickStop() => Find("StopButton").AsButton().Invoke();

    public bool IsStartEnabled => Find("StartButton").IsEnabled;

    public bool IsStopEnabled => Find("StopButton").IsEnabled;

    public string StatusText => Find("StatusText").Name;

    public string? ConnectionError => TryFind("ConnectionErrorText")?.Name;

    public void WaitForStatusContaining(string fragment) =>
        WaitUntil(() => StatusText.Contains(fragment, StringComparison.Ordinal), $"status to contain '{fragment}' (was '{StatusText}')");

    // ---- auto reply & warnings ---------------------------------------------------------------

    public void SetAutoReply(bool enabled) => Find("AutoReplyCheckBox").AsCheckBox().IsChecked = enabled;

    public bool IsAutoReplyChecked => Find("AutoReplyCheckBox").AsCheckBox().IsChecked == true;

    /// <summary>
    /// The banner is a WPF Border, which has no automation peer; its text block is the element
    /// that appears in (visible) and disappears from (collapsed) the UI Automation tree.
    /// </summary>
    public bool IsWarningVisible => TryFind("WarningText") is { IsOffscreen: false };

    public string? WarningText => TryFind("WarningText")?.Name;

    public void WaitForWarningContaining(string fragment) =>
        WaitUntil(() => WarningText?.Contains(fragment, StringComparison.Ordinal) == true, $"a warning containing '{fragment}'");

    public void DismissWarning() => Find("DismissWarningButton").AsButton().Invoke();

    // ---- message editors ---------------------------------------------------------------------

    public MessageEditor Message1 => new(this, "M1", "Message1Tab");

    public MessageEditor Message2 => new(this, "M2", "Message2Tab");

    // ---- traffic list ------------------------------------------------------------------------

    /// <summary>Row names as exposed to UI automation: "Transmit Mesaj 1", "Receive Tanımsız (…)", …</summary>
    public IReadOnlyList<string> TrafficRows =>
        Find("TrafficList").FindAllChildren()
            .Where(e => e.ControlType is ControlType.DataItem or ControlType.ListItem)
            .Select(e => e.Name)
            .ToList();

    public void WaitForTrafficRow(string exactName) =>
        WaitUntil(() => TrafficRows.Contains(exactName), $"traffic row '{exactName}' (rows: {string.Join(" | ", TrafficRows)})");

    public void WaitForTrafficRowCount(Func<int, bool> condition, string description) =>
        WaitUntil(() => condition(TrafficRows.Count), $"traffic row count {description} (now {TrafficRows.Count})");

    public void SelectTrafficRow(int index)
    {
        var rows = Find("TrafficList").FindAllChildren()
            .Where(e => e.ControlType is ControlType.DataItem or ControlType.ListItem)
            .ToList();
        rows[index].Patterns.SelectionItem.Pattern.Select();
    }

    public void ClickClearLog() => Find("ClearLogButton").AsButton().Invoke();

    // ---- detail pane -------------------------------------------------------------------------

    public string DetailHeader => Find("DetailHeader").Name;

    /// <summary>Field rows as "Name = Value".</summary>
    public IReadOnlyList<string> DetailFields =>
        Find("DetailFields").FindAllChildren()
            .Where(e => e.ControlType is ControlType.DataItem or ControlType.ListItem)
            .Select(e => e.Name)
            .ToList();

    public string DetailHex => Find("DetailHex").AsTextBox().Text;

    // ---- helpers -----------------------------------------------------------------------------

    internal AutomationElement Find(string automationId) =>
        Retry.WhileNull(
                () => _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                UiTimeout,
                throwOnTimeout: false)
            .Result
        ?? throw new InvalidOperationException($"Element '{automationId}' was not found in window '{_window.Title}'.");

    internal AutomationElement? TryFind(string automationId) =>
        _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    internal void SetText(string automationId, string text)
    {
        var textBox = Find(automationId).AsTextBox();
        textBox.Text = text;
    }

    internal static void WaitUntil(Func<bool> condition, string description)
    {
        var result = Retry.WhileFalse(condition, UiTimeout, TimeSpan.FromMilliseconds(100), throwOnTimeout: false, ignoreException: true);
        if (!result.Success)
        {
            throw new TimeoutException($"Timed out after {UiTimeout.TotalSeconds} s waiting for {description}.");
        }
    }

    /// <summary>Controls of one message tab. Prefix is "M1" or "M2" as used in the AutomationIds.</summary>
    internal sealed class MessageEditor
    {
        private readonly MainWindowDriver _driver;
        private readonly string _prefix;
        private readonly string _tabId;

        public MessageEditor(MainWindowDriver driver, string prefix, string tabId)
        {
            _driver = driver;
            _prefix = prefix;
            _tabId = tabId;
        }

        public void Open() => _driver.Find(_tabId).AsTabItem().Select();

        public void SetField(string field, string value) => _driver.SetText($"{_prefix}_{field}", value);

        public string GetField(string field) => _driver.Find($"{_prefix}_{field}").AsTextBox().Text;

        public void SetPositionValid(bool value) => _driver.Find($"{_prefix}_IsPositionValid").AsCheckBox().IsChecked = value;

        public void SelectRank(int index) => _driver.Find($"{_prefix}_Rank").AsComboBox().Select(index);

        public bool IsSendEnabled => _driver.Find($"{_prefix}_SendButton").IsEnabled;

        public void ClickSend() => _driver.Find($"{_prefix}_SendButton").AsButton().Invoke();

        public void SetInterval(int milliseconds) => _driver.SetText($"{_prefix}_IntervalMs", milliseconds.ToString(CultureInfo.InvariantCulture));

        public void SetPeriodic(bool enabled) => _driver.Find($"{_prefix}_PeriodicCheckBox").AsCheckBox().IsChecked = enabled;

        public bool IsPeriodicChecked => _driver.Find($"{_prefix}_PeriodicCheckBox").AsCheckBox().IsChecked == true;

        public void WaitForSendEnabled(bool expected) =>
            WaitUntil(() => IsSendEnabled == expected, $"{_prefix} Gönder to be {(expected ? "enabled" : "disabled")}");
    }
}
