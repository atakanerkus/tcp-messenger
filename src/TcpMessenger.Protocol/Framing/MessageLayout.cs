using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.Protocol.Framing;

/// <summary>
/// Byte offsets and sizes of every field, derived from the tables in the task specification.
/// Single source of truth for the codec, the frame parser and the protocol tests.
/// </summary>
public static class MessageLayout
{
    /// <summary>Every message starts with a 4-byte identifier.</summary>
    public const int MessageIdSize = sizeof(int);

    /// <summary>"25 karakterlik String" fields occupy exactly 25 bytes.</summary>
    public const int FixedStringSize = 25;

    /// <summary>Layout of <see cref="Messages.Message1"/>.</summary>
    public static class Message1
    {
        public const int Size = 64;
        public const int IdOffset = 0;
        public const int UnitReferenceNumberOffset = 4;
        public const int FirstNameOffset = 8;
        public const int UnitNoOffset = 33;
        public const int LastNameOffset = 37;
        public const int RankOffset = 62;
    }

    /// <summary>Layout of <see cref="Messages.Message2"/>.</summary>
    public static class Message2
    {
        public const int Size = 29;
        public const int IdOffset = 0;
        public const int UnitReferenceNumberOffset = 4;
        public const int PositionValidityOffset = 8;
        public const int LatitudeOffset = 9;
        public const int LongitudeOffset = 17;
        public const int AltitudeOffset = 25;
    }

    /// <summary>Returns the fixed wire size of a defined message, or false for an undefined identifier.</summary>
    public static bool TryGetSize(int messageId, out int size)
    {
        switch (messageId)
        {
            case MessageId.Message1:
                size = Message1.Size;
                return true;
            case MessageId.Message2:
                size = Message2.Size;
                return true;
            default:
                size = 0;
                return false;
        }
    }
}
