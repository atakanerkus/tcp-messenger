using System.Net.Sockets;
using TcpMessenger.App.ViewModels;
using TcpMessenger.Core.Session;
using TcpMessenger.Tests.TestUtilities.Fakes;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class ConnectionViewModelTests
{
    private readonly FakeMessengerSession _session = new() { State = SessionState.Idle, Mode = null };
    private readonly ImmediateUiDispatcher _dispatcher = new();

    private ConnectionViewModel NewViewModel() => new(_session, _dispatcher);

    [Fact]
    public void Defaults_ServerModeLoopbackPort5000Idle()
    {
        var vm = NewViewModel();

        Assert.True(vm.IsServerMode);
        Assert.Equal("127.0.0.1", vm.Host);
        Assert.Equal(5000, vm.Port);
        Assert.Equal("Bağlı değil", vm.StatusText);
        Assert.True(vm.CanEditSettings);
        Assert.Equal("Server", vm.ModeLabel);
    }

    [Fact]
    public async Task Start_InServerMode_StartsServerOnPort()
    {
        var vm = NewViewModel();
        vm.Port = 6000;

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(6000, Assert.Single(_session.StartServerCalls));
        Assert.Empty(_session.ConnectCalls);
        Assert.Equal(SessionState.Listening, vm.State);
        Assert.Contains("6000", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.CanEditSettings);
    }

    [Fact]
    public async Task Start_InClientMode_ConnectsToTrimmedHost()
    {
        var vm = NewViewModel();
        vm.IsServerMode = false;
        vm.Host = " 10.1.2.3 ";
        vm.Port = 7000;

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(("10.1.2.3", 7000), Assert.Single(_session.ConnectCalls));
        Assert.Equal(SessionState.Connected, vm.State);
        Assert.True(vm.IsConnected);
        Assert.StartsWith("Bağlı:", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_WhenSessionThrows_ShowsErrorAndStaysIdle()
    {
        var vm = NewViewModel();
        _session.StartException = new SocketException((int)SocketError.AddressAlreadyInUse);

        await vm.StartCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorText));
        Assert.Equal(SessionState.Idle, vm.State);
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Start_ClearsPreviousError()
    {
        var vm = NewViewModel();
        _session.StartException = new InvalidOperationException("boom");
        await vm.StartCommand.ExecuteAsync(null);
        _session.StartException = null;

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65_536)]
    public void StartCommand_IsDisabledForInvalidPort(int port)
    {
        var vm = NewViewModel();

        vm.Port = port;

        Assert.False(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void StartCommand_IsDisabledForEmptyHostInClientMode()
    {
        var vm = NewViewModel();
        vm.IsServerMode = false;

        vm.Host = "   ";

        Assert.False(vm.StartCommand.CanExecute(null));
        vm.IsServerMode = true;
        Assert.True(vm.StartCommand.CanExecute(null)); // host is irrelevant for a server
    }

    [Fact]
    public async Task Stop_StopsSessionAndReturnsToIdle()
    {
        var vm = NewViewModel();
        await vm.StartCommand.ExecuteAsync(null);

        Assert.True(vm.StopCommand.CanExecute(null));
        await vm.StopCommand.ExecuteAsync(null);

        Assert.Equal(1, _session.StopCalls);
        Assert.Equal(SessionState.Idle, vm.State);
        Assert.Equal("Bağlı değil", vm.StatusText);
        Assert.False(vm.StopCommand.CanExecute(null));
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public void ExternalStateChange_UpdatesStatusAndCommands()
    {
        var vm = NewViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _session.LocalPort = 5000;
        _session.State = SessionState.Listening;

        Assert.Equal(SessionState.Listening, vm.State);
        Assert.Contains(nameof(ConnectionViewModel.StatusText), raised);
        Assert.Contains(nameof(ConnectionViewModel.CanEditSettings), raised);
        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.True(vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public void ModeLabel_FollowsTheRadioButtons()
    {
        var vm = NewViewModel();

        vm.IsServerMode = false;

        Assert.Equal("Client", vm.ModeLabel);
        Assert.True(vm.IsClientMode);
    }
}
