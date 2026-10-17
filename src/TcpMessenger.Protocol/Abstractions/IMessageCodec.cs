using TcpMessenger.Protocol.Serialization;

namespace TcpMessenger.Protocol.Abstractions;

/// <summary>
/// Converts messages to and from their wire representation. The wire format is exactly the
/// field list of the task specification: fixed size, big-endian, no extra header.
/// </summary>
public interface IMessageCodec
{
    /// <summary>Serializes a message into its fixed-size wire representation.</summary>
    /// <exception cref="MessageFormatException">
    /// The message type is unsupported or a field cannot be represented (e.g. a string longer than its slot).
    /// </exception>
    byte[] Encode(IMessage message);

    /// <summary>Deserializes one complete frame as produced by <see cref="IFrameParser"/>.</summary>
    /// <exception cref="MessageFormatException">The frame has an undefined identifier or a wrong length.</exception>
    IMessage Decode(ReadOnlySpan<byte> frame);
}
