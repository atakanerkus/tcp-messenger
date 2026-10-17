using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Core.Abstractions;

/// <summary>
/// Supplies the message to send when the application, not the user, initiates a transmission:
/// the automatic reply and the periodic sender both send whatever the user currently has in the
/// form for that message type. Implemented by the UI layer.
/// </summary>
public interface IOutgoingMessageProvider
{
    /// <summary>
    /// Returns the current message of the given type, or null when the form holds invalid values
    /// and nothing should be sent.
    /// </summary>
    IMessage? GetOutgoingMessage(int messageId);
}
