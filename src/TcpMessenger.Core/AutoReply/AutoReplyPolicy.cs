using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.Core.AutoReply;

/// <summary>
/// The reply rule from the task specification: Mesaj 1 is answered with Mesaj 2, Mesaj 2 with Mesaj 1.
/// Pure function; whether a reply is actually sent is decided by the session (K4).
/// </summary>
public static class AutoReplyPolicy
{
    /// <summary>Identifier of the message to send back, or null when the received message has no reply.</summary>
    public static int? GetReplyMessageId(int receivedMessageId) => receivedMessageId switch
    {
        MessageId.Message1 => MessageId.Message2,
        MessageId.Message2 => MessageId.Message1,
        _ => null,
    };
}
