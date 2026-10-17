using TcpMessenger.Core.Abstractions;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>Dictionary-backed <see cref="IOutgoingMessageProvider"/>.</summary>
internal sealed class StubOutgoingMessageProvider : IOutgoingMessageProvider
{
    /// <summary>Message returned per id; a missing or null entry means "form invalid".</summary>
    public Dictionary<int, IMessage?> Messages { get; } = [];

    /// <summary>How many times each id was requested.</summary>
    public Dictionary<int, int> Requests { get; } = [];

    /// <summary>Provider that returns the valid baseline messages for both ids.</summary>
    public static StubOutgoingMessageProvider WithValidMessages()
    {
        var provider = new StubOutgoingMessageProvider();
        provider.Messages[MessageId.Message1] = TestMessages.ValidMessage1();
        provider.Messages[MessageId.Message2] = TestMessages.ValidMessage2();
        return provider;
    }

    public IMessage? GetOutgoingMessage(int messageId)
    {
        Requests[messageId] = Requests.GetValueOrDefault(messageId) + 1;
        return Messages.GetValueOrDefault(messageId);
    }
}
