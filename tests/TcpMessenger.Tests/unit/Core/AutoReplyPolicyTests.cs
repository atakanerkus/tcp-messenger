using TcpMessenger.Core.AutoReply;
using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.Tests.Unit.Core;

[Trait("Category", "Unit")]
public sealed class AutoReplyPolicyTests
{
    [Fact]
    public void Message1_IsAnsweredWithMessage2()
    {
        Assert.Equal(MessageId.Message2, AutoReplyPolicy.GetReplyMessageId(MessageId.Message1));
    }

    [Fact]
    public void Message2_IsAnsweredWithMessage1()
    {
        Assert.Equal(MessageId.Message1, AutoReplyPolicy.GetReplyMessageId(MessageId.Message2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void UndefinedId_HasNoReply(int id)
    {
        Assert.Null(AutoReplyPolicy.GetReplyMessageId(id));
    }
}
