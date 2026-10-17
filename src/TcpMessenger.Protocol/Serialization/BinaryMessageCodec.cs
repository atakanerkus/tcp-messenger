using System.Buffers.Binary;
using System.Diagnostics;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;
using Layout1 = TcpMessenger.Protocol.Framing.MessageLayout.Message1;
using Layout2 = TcpMessenger.Protocol.Framing.MessageLayout.Message2;

namespace TcpMessenger.Protocol.Serialization;

/// <summary>
/// Big-endian (network byte order) codec that writes exactly the fields of the task specification.
/// Stateless and thread-safe.
/// </summary>
public sealed class BinaryMessageCodec : IMessageCodec
{
    /// <inheritdoc />
    public byte[] Encode(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            Message1 m => EncodeMessage1(m),
            Message2 m => EncodeMessage2(m),
            _ => throw new MessageFormatException($"Unsupported message type '{message.GetType().Name}'."),
        };
    }

    /// <inheritdoc />
    public IMessage Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < MessageLayout.MessageIdSize)
        {
            throw new MessageFormatException(
                $"Frame of {frame.Length} bytes is too short to contain a message identifier.");
        }

        int id = BinaryPrimitives.ReadInt32BigEndian(frame);
        if (!MessageLayout.TryGetSize(id, out int expectedSize))
        {
            throw new MessageFormatException($"Message identifier {id} is not defined.");
        }

        if (frame.Length != expectedSize)
        {
            throw new MessageFormatException(
                $"Message {id} must be {expectedSize} bytes but the frame has {frame.Length}.");
        }

        return id switch
        {
            MessageId.Message1 => DecodeMessage1(frame),
            MessageId.Message2 => DecodeMessage2(frame),
            _ => throw new UnreachableException("TryGetSize accepted an identifier the decoder does not know."),
        };
    }

    private static byte[] EncodeMessage1(Message1 message)
    {
        var buffer = new byte[Layout1.Size];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(span[Layout1.IdOffset..], MessageId.Message1);
        BinaryPrimitives.WriteInt32BigEndian(span[Layout1.UnitReferenceNumberOffset..], message.UnitReferenceNumber);
        FixedString.Write(span.Slice(Layout1.FirstNameOffset, MessageLayout.FixedStringSize), message.FirstName);
        BinaryPrimitives.WriteUInt32BigEndian(span[Layout1.UnitNoOffset..], message.UnitNo);
        FixedString.Write(span.Slice(Layout1.LastNameOffset, MessageLayout.FixedStringSize), message.LastName);
        BinaryPrimitives.WriteInt16BigEndian(span[Layout1.RankOffset..], (short)message.Rank);

        return buffer;
    }

    private static Message1 DecodeMessage1(ReadOnlySpan<byte> frame) => new(
        UnitReferenceNumber: BinaryPrimitives.ReadInt32BigEndian(frame[Layout1.UnitReferenceNumberOffset..]),
        FirstName: FixedString.Read(frame.Slice(Layout1.FirstNameOffset, MessageLayout.FixedStringSize)),
        UnitNo: BinaryPrimitives.ReadUInt32BigEndian(frame[Layout1.UnitNoOffset..]),
        LastName: FixedString.Read(frame.Slice(Layout1.LastNameOffset, MessageLayout.FixedStringSize)),
        Rank: (Rank)BinaryPrimitives.ReadInt16BigEndian(frame[Layout1.RankOffset..]));

    private static byte[] EncodeMessage2(Message2 message)
    {
        var buffer = new byte[Layout2.Size];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(span[Layout2.IdOffset..], MessageId.Message2);
        BinaryPrimitives.WriteInt32BigEndian(span[Layout2.UnitReferenceNumberOffset..], message.UnitReferenceNumber);
        span[Layout2.PositionValidityOffset] = message.PositionValidity;
        BinaryPrimitives.WriteInt64BigEndian(span[Layout2.LatitudeOffset..], message.Latitude);
        BinaryPrimitives.WriteInt64BigEndian(span[Layout2.LongitudeOffset..], message.Longitude);
        BinaryPrimitives.WriteInt32BigEndian(span[Layout2.AltitudeOffset..], message.Altitude);

        return buffer;
    }

    private static Message2 DecodeMessage2(ReadOnlySpan<byte> frame) => new(
        UnitReferenceNumber: BinaryPrimitives.ReadInt32BigEndian(frame[Layout2.UnitReferenceNumberOffset..]),
        PositionValidity: frame[Layout2.PositionValidityOffset],
        Latitude: BinaryPrimitives.ReadInt64BigEndian(frame[Layout2.LatitudeOffset..]),
        Longitude: BinaryPrimitives.ReadInt64BigEndian(frame[Layout2.LongitudeOffset..]),
        Altitude: BinaryPrimitives.ReadInt32BigEndian(frame[Layout2.AltitudeOffset..]));
}
