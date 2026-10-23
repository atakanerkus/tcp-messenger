using TcpMessenger.App.ViewModels;
using TcpMessenger.Core.Session;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class MessageEditorViewModelTests
{
    private readonly FakeMessengerSession _session = new();
    private readonly FakePeriodicSender _periodic = new();
    private readonly ImmediateUiDispatcher _dispatcher = new();
    private readonly MessageValidator _validator = new();

    private Message1EditorViewModel NewMessage1() => new(_session, _periodic, _validator, _dispatcher);

    private Message2EditorViewModel NewMessage2() => new(_session, _periodic, _validator, _dispatcher);

    // ---- validation --------------------------------------------------------------------------

    [Fact]
    public void Defaults_AreValidAndProduceAMessage()
    {
        var vm = NewMessage1();

        Assert.False(vm.HasErrors);
        var message = Assert.IsType<Message1>(vm.CurrentMessage);
        Assert.Equal(MessageId.Message1, message.Id);
        Assert.Equal("Mesaj 1", vm.Title);
    }

    [Fact]
    public void OutOfRangeField_ReportsErrorOnThatPropertyAndHidesCurrentMessage()
    {
        var vm = NewMessage1();
        var changed = new List<string?>();
        vm.ErrorsChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UnitReferenceNumber = "10000";

        Assert.True(vm.HasErrors);
        Assert.Null(vm.CurrentMessage);
        Assert.Contains(nameof(Message1.UnitReferenceNumber), changed);
        Assert.NotEmpty(vm.GetErrors(nameof(Message1.UnitReferenceNumber)));
        Assert.Empty(vm.GetErrors(nameof(Message1.FirstName)));
    }

    [Fact]
    public void FixingTheField_ClearsTheErrorAndNotifies()
    {
        var vm = NewMessage1();
        vm.UnitReferenceNumber = "10000";
        var changed = new List<string?>();
        vm.ErrorsChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UnitReferenceNumber = "9999";

        Assert.False(vm.HasErrors);
        Assert.Contains(nameof(Message1.UnitReferenceNumber), changed);
        Assert.NotNull(vm.CurrentMessage);
    }

    [Fact]
    public void NameLongerThan25Bytes_IsAnErrorOnThatName()
    {
        var vm = NewMessage1();

        vm.LastName = new string('ş', 13); // 26 bytes

        Assert.True(vm.HasErrors);
        Assert.NotEmpty(vm.GetErrors(nameof(Message1.LastName)));
        Assert.Empty(vm.GetErrors(nameof(Message1.FirstName)));
    }

    [Fact]
    public void Message2_CheckBoxMapsToWireByte()
    {
        var vm = NewMessage2();

        vm.IsPositionValid = false;
        var off = Assert.IsType<Message2>(vm.CurrentMessage);
        vm.IsPositionValid = true;
        var on = Assert.IsType<Message2>(vm.CurrentMessage);

        Assert.Equal(Message2.PositionInvalid, off.PositionValidity);
        Assert.Equal(Message2.PositionValid, on.PositionValidity);
    }

    [Fact]
    public void Message2_LatitudeOutOfRange_IsReported()
    {
        var vm = NewMessage2();

        vm.Latitude = "32400001";

        Assert.True(vm.HasErrors);
        Assert.NotEmpty(vm.GetErrors(nameof(Message2.Latitude)));
        Assert.Equal("Mesaj 2", vm.Title);
    }

    [Fact]
    public void GetErrors_WithoutPropertyName_ReturnsAllErrors()
    {
        var vm = NewMessage2();
        vm.Latitude = "9223372036854775807";
        vm.Altitude = "-1";

        var all = vm.GetErrors(null).Cast<string>().ToList();

        Assert.Equal(2, all.Count);
    }

    // ---- numeric text (K15) ------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(Message1.UnitReferenceNumber), "")]
    [InlineData(nameof(Message1.UnitReferenceNumber), "   ")]
    [InlineData(nameof(Message1.UnitReferenceNumber), "abc")]
    [InlineData(nameof(Message1.UnitReferenceNumber), "12.5")]
    [InlineData(nameof(Message1.UnitReferenceNumber), "2147483648")] // int.MaxValue + 1
    [InlineData(nameof(Message1.UnitNo), "")]
    [InlineData(nameof(Message1.UnitNo), "-1")]                      // uint has no sign
    [InlineData(nameof(Message1.UnitNo), "4294967296")]              // uint.MaxValue + 1
    public void Message1_NumericFieldThatIsNotANumber_BlocksSending(string field, string text)
    {
        var vm = NewMessage1();

        SetText(vm, field, text);

        Assert.True(vm.HasErrors);
        Assert.NotEmpty(vm.GetErrors(field));
        Assert.Null(vm.CurrentMessage);
        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(nameof(Message2.UnitReferenceNumber))]
    [InlineData(nameof(Message2.Latitude))]
    [InlineData(nameof(Message2.Longitude))]
    [InlineData(nameof(Message2.Altitude))]
    public void Message2_EmptyNumericField_BlocksSending(string field)
    {
        var vm = NewMessage2();

        SetText(vm, field, "");

        Assert.Equal("Değer boş olamaz.", Assert.Single(vm.GetErrors(field).Cast<string>()));
        Assert.Null(vm.CurrentMessage);
        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [Fact]
    public void EmptiedField_DoesNotFallBackToThePreviousValue()
    {
        // The bug K15 fixes: WPF could not convert "" and left the old value in place, so it was sent.
        var vm = NewMessage1();
        vm.UnitReferenceNumber = "1234";

        vm.UnitReferenceNumber = "";

        Assert.Null(vm.CurrentMessage);
    }

    [Fact]
    public void NumericText_TrimsSurroundingSpacesAndAcceptsASign()
    {
        var vm = NewMessage2();

        vm.Latitude = " -32400000 ";

        var message = Assert.IsType<Message2>(vm.CurrentMessage);
        Assert.Equal(-32_400_000, message.Latitude);
    }

    [Fact]
    public void RetypingAValidNumber_ClearsTheError()
    {
        var vm = NewMessage1();
        vm.UnitNo = "abc";

        vm.UnitNo = "42";

        Assert.False(vm.HasErrors);
        Assert.Equal(42u, Assert.IsType<Message1>(vm.CurrentMessage).UnitNo);
    }

    [Fact]
    public void EmptyNames_AreAllowed()
    {
        // The task gives no minimum length; "" is 25 zero bytes on the wire.
        var vm = NewMessage1();

        vm.FirstName = "";
        vm.LastName = "";

        Assert.False(vm.HasErrors);
        var message = Assert.IsType<Message1>(vm.CurrentMessage);
        Assert.Equal("", message.FirstName);
    }

    // ---- send --------------------------------------------------------------------------------

    [Fact]
    public void SendCommand_IsDisabledWhileNotConnected()
    {
        _session.State = SessionState.Idle;
        var vm = NewMessage1();

        Assert.False(vm.SendCommand.CanExecute(null));
        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void SendCommand_BecomesEnabledWhenSessionConnects()
    {
        _session.State = SessionState.Idle;
        var vm = NewMessage1();

        _session.State = SessionState.Connected;

        Assert.True(vm.SendCommand.CanExecute(null));
        Assert.True(vm.IsConnected);
    }

    [Fact]
    public void SendCommand_IsDisabledWhileAFieldIsInvalid()
    {
        var vm = NewMessage1();

        vm.Rank = (Rank)9;

        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task Send_PassesTheCurrentMessageToTheSession()
    {
        var vm = NewMessage1();
        vm.FirstName = "Zeynep";
        vm.UnitNo = "77";

        await vm.SendCommand.ExecuteAsync(null);

        var sent = Assert.IsType<Message1>(Assert.Single(_session.Sent));
        Assert.Equal("Zeynep", sent.FirstName);
        Assert.Equal(77u, sent.UnitNo);
        Assert.Null(vm.SendError);
    }

    [Fact]
    public async Task Send_WhenSessionThrows_ShowsTheErrorInsteadOfPropagating()
    {
        var vm = NewMessage1();
        _session.SendException = new InvalidOperationException("Karşı tarafa bağlı değil.");

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Karşı tarafa bağlı değil.", vm.SendError);
    }

    // ---- periodic ----------------------------------------------------------------------------

    [Fact]
    public void DefaultIntervals_FollowTheTaskExample()
    {
        Assert.Equal("100", NewMessage1().IntervalMs);
        Assert.Equal("500", NewMessage2().IntervalMs);
    }

    [Fact]
    public void IsPeriodic_On_StartsScheduleWithCurrentInterval()
    {
        var vm = NewMessage1();
        vm.IntervalMs = "250";

        vm.IsPeriodic = true;

        Assert.Equal((MessageId.Message1, TimeSpan.FromMilliseconds(250)), Assert.Single(_periodic.StartCalls));
        Assert.True(_periodic.IsRunning(MessageId.Message1));
    }

    [Fact]
    public void IsPeriodic_Off_StopsSchedule()
    {
        var vm = NewMessage1();
        vm.IsPeriodic = true;

        vm.IsPeriodic = false;

        Assert.Equal(MessageId.Message1, Assert.Single(_periodic.StopCalls));
        Assert.False(_periodic.IsRunning(MessageId.Message1));
    }

    [Fact]
    public void ChangingInterval_WhileRunning_RestartsWithNewInterval()
    {
        var vm = NewMessage2();
        vm.IsPeriodic = true;

        vm.IntervalMs = "1000";

        Assert.Equal(TimeSpan.FromSeconds(1), _periodic.IntervalOf(MessageId.Message2));
        Assert.Equal(2, _periodic.StartCalls.Count);
    }

    [Fact]
    public void IntervalBelowMinimum_ShowsErrorAndRefusesToStart()
    {
        var vm = NewMessage1();

        vm.IntervalMs = "5";
        vm.IsPeriodic = true;

        Assert.NotNull(vm.IntervalError);
        Assert.False(vm.IsPeriodic);
        Assert.Empty(_periodic.StartCalls);
    }

    [Fact]
    public void IntervalError_ClearsWhenIntervalBecomesValid()
    {
        var vm = NewMessage1();
        vm.IntervalMs = "5";

        vm.IntervalMs = "10";

        Assert.Null(vm.IntervalError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void IntervalThatIsNotANumber_RefusesToStart(string text)
    {
        var vm = NewMessage1();

        vm.IntervalMs = text;
        vm.IsPeriodic = true;

        Assert.NotNull(vm.IntervalError);
        Assert.False(vm.IsPeriodic);
        Assert.Empty(_periodic.StartCalls);
    }

    [Fact]
    public void IntervalEmptiedWhileRunning_KeepsTheLastValidPeriod()
    {
        var vm = NewMessage1();
        vm.IsPeriodic = true;

        vm.IntervalMs = "";

        Assert.Equal("Değer boş olamaz.", vm.IntervalError);
        Assert.True(_periodic.IsRunning(MessageId.Message1));
        Assert.Equal(TimeSpan.FromMilliseconds(100), _periodic.IntervalOf(MessageId.Message1));
    }

    private static void SetText(object vm, string property, string text) =>
        vm.GetType().GetProperty(property)!.SetValue(vm, text);
}
