using TcpMessenger.App;
using TcpMessenger.Core.Session;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class StartupOptionsTests
{
    [Fact]
    public void NoArguments_GivesServerDefaults()
    {
        var options = StartupOptions.Parse([]);

        Assert.Equal(SessionMode.Server, options.Mode);
        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(5000, options.Port);
        Assert.True(options.AutoReply);
    }

    [Fact]
    public void AllArguments_AreParsedCaseInsensitively()
    {
        var options = StartupOptions.Parse(["--MODE", "Client", "--host", "192.168.1.10", "--Port", "6001", "--autoreply", "OFF"]);

        Assert.Equal(SessionMode.Client, options.Mode);
        Assert.Equal("192.168.1.10", options.Host);
        Assert.Equal(6001, options.Port);
        Assert.False(options.AutoReply);
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("off", false)]
    [InlineData("0", false)]
    [InlineData("maybe", false)]
    public void AutoReply_AcceptsOnOffAndDigits(string value, bool expected)
    {
        Assert.Equal(expected, StartupOptions.Parse(["--autoreply", value]).AutoReply);
    }

    [Fact]
    public void MalformedOrUnknownValues_KeepDefaults()
    {
        var options = StartupOptions.Parse(["--port", "abc", "--mode", "peer", "--verbose", "--host"]);

        Assert.Equal(5000, options.Port);
        Assert.Equal(SessionMode.Server, options.Mode);
        Assert.Equal("127.0.0.1", options.Host);
    }
}
