using Microsoft.Extensions.Time.Testing;
using TcpMessenger.Core.Scheduling;
using TcpMessenger.Core.Session;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.Core;

/// <summary>Deterministic timing through <see cref="FakeTimeProvider"/>; no real waiting.</summary>
[Trait("Category", "Unit")]
public sealed class PeriodicSenderTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _clock = new();
    private readonly FakeMessengerSession _session = new();
    private readonly StubOutgoingMessageProvider _outgoing = StubOutgoingMessageProvider.WithValidMessages();
    private readonly PeriodicSender _sender;

    public PeriodicSenderTests()
    {
        _sender = new PeriodicSender(_session, _outgoing, _clock);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();

    [Fact]
    public void Start_Message1Every100ms_SendsTenTimesInOneSecond()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(10, _session.Sent.Count);
        Assert.All(_session.Sent, m => Assert.Equal(MessageId.Message1, m.Id));
    }

    [Fact]
    public void Start_TwoSchedules_RunIndependently()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));
        _sender.Start(MessageId.Message2, TimeSpan.FromMilliseconds(500));

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(10, _session.Sent.Count(m => m.Id == MessageId.Message1));
        Assert.Equal(2, _session.Sent.Count(m => m.Id == MessageId.Message2));
        Assert.True(_sender.IsRunning(MessageId.Message1));
        Assert.True(_sender.IsRunning(MessageId.Message2));
    }

    [Fact]
    public void Start_DoesNotSendBeforeFirstIntervalElapses()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));

        _clock.Advance(TimeSpan.FromMilliseconds(99));

        Assert.Empty(_session.Sent);
    }

    [Fact]
    public void Stop_HaltsOnlyThatSchedule()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));
        _sender.Start(MessageId.Message2, TimeSpan.FromMilliseconds(100));
        _clock.Advance(TimeSpan.FromMilliseconds(300));

        _sender.Stop(MessageId.Message1);
        _clock.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Equal(3, _session.Sent.Count(m => m.Id == MessageId.Message1));
        Assert.Equal(6, _session.Sent.Count(m => m.Id == MessageId.Message2));
        Assert.False(_sender.IsRunning(MessageId.Message1));
        Assert.True(_sender.IsRunning(MessageId.Message2));
    }

    [Fact]
    public void Stop_WhenNotRunning_IsANoOp()
    {
        _sender.Stop(MessageId.Message1);

        Assert.False(_sender.IsRunning(MessageId.Message1));
    }

    [Fact]
    public void StopAll_HaltsEverything()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));
        _sender.Start(MessageId.Message2, TimeSpan.FromMilliseconds(100));

        _sender.StopAll();
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_session.Sent);
        Assert.False(_sender.IsRunning(MessageId.Message1));
        Assert.False(_sender.IsRunning(MessageId.Message2));
    }

    [Fact]
    public void Start_Again_ReplacesTheInterval()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));
        _clock.Advance(TimeSpan.FromMilliseconds(200));

        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(500));
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(2 + 2, _session.Sent.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-100)]
    public void Start_IntervalBelowMinimum_Throws(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(milliseconds)));
        Assert.False(_sender.IsRunning(MessageId.Message1));
    }

    [Fact]
    public void Start_AtMinimumInterval_IsAccepted()
    {
        _sender.Start(MessageId.Message1, _sender.MinimumInterval);

        _clock.Advance(_sender.MinimumInterval);

        Assert.Single(_session.Sent);
    }

    [Fact]
    public void Tick_WhenSessionNotConnected_SkipsSilently()
    {
        _session.State = SessionState.Listening;
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_session.Sent);
        Assert.True(_sender.IsRunning(MessageId.Message1)); // resumes automatically once connected
        Assert.Empty(_outgoing.Requests);
    }

    [Fact]
    public void Tick_WhenFormInvalid_SkipsThatTickOnly()
    {
        _outgoing.Messages[MessageId.Message1] = null;
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));
        _clock.Advance(TimeSpan.FromMilliseconds(300));

        _outgoing.Messages[MessageId.Message1] = TcpMessenger.Tests.TestUtilities.TestMessages.ValidMessage1();
        _clock.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(2, _session.Sent.Count);
        Assert.Equal(5, _outgoing.Requests[MessageId.Message1]);
    }

    [Fact]
    public void Tick_SendsCurrentProviderValueEachTime()
    {
        _sender.Start(MessageId.Message2, TimeSpan.FromMilliseconds(100));
        _clock.Advance(TimeSpan.FromMilliseconds(100));

        var updated = TcpMessenger.Tests.TestUtilities.TestMessages.ValidMessage2() with { Altitude = 4242 };
        _outgoing.Messages[MessageId.Message2] = updated;
        _clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(2, _session.Sent.Count);
        Assert.Equal(4242, Assert.IsType<Message2>(_session.Sent[1]).Altitude);
    }

    [Fact]
    public void Tick_WhenSendThrows_StopsScheduleAndRaisesSendFailed()
    {
        PeriodicSendFailure? failure = null;
        _sender.SendFailed += (_, f) => failure = f;
        _session.SendException = new IOException("link down");
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));

        _clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.NotNull(failure);
        Assert.Equal(MessageId.Message1, failure.MessageId);
        Assert.IsType<IOException>(failure.Error);
        Assert.False(_sender.IsRunning(MessageId.Message1));
    }

    [Fact]
    public async Task Tick_WhilePreviousSendStillRunning_IsSkippedNotQueued()
    {
        var gate = new TaskCompletionSource();
        _session.SendGate = gate;
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));

        _clock.Advance(TimeSpan.FromMilliseconds(500)); // 5 ticks while the first send hangs

        Assert.Single(_session.Sent);

        // Release the hanging send. Its continuation may run on another thread (xunit installs a
        // synchronization context), so keep ticking until the slot is free again.
        _session.SendGate = null;
        await Task.Run(gate.SetResult);
        for (int i = 0; i < 100 && _session.Sent.Count < 2; i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }

        Assert.Equal(2, _session.Sent.Count);
    }

    [Fact]
    public async Task DisposeAsync_StopsAllAndRejectsNewSchedules()
    {
        _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100));

        await _sender.DisposeAsync();
        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_session.Sent);
        Assert.Throws<ObjectDisposedException>(() => _sender.Start(MessageId.Message1, TimeSpan.FromMilliseconds(100)));
    }
}
