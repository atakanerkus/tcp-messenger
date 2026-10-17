using TcpMessenger.Protocol.Framing;

namespace TcpMessenger.Protocol.Validation;

/// <summary>Value ranges from the "Alabileceği Değerler" column of the task specification.</summary>
public static class FieldConstraints
{
    /// <summary>Maximum encoded length of Adı / Soyadı.</summary>
    public const int NameMaxBytes = MessageLayout.FixedStringSize;

    /// <summary>Constraints of <see cref="Messages.Message1"/>.</summary>
    public static class Message1
    {
        public const int UnitReferenceNumberMin = -1000;
        public const int UnitReferenceNumberMax = 9999;
    }

    /// <summary>Constraints of <see cref="Messages.Message2"/>.</summary>
    public static class Message2
    {
        public const int UnitReferenceNumberMin = 1;
        public const int UnitReferenceNumberMax = 9999;
        public const long LatitudeMin = -32_400_000;
        public const long LatitudeMax = 32_400_000;
        public const long LongitudeMin = -64_800_000;
        public const long LongitudeMax = 64_800_000;
        public const int AltitudeMin = 0;
        public const int AltitudeMax = 10_000;
    }
}
