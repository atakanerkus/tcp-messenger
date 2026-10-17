using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.Tests.TestUtilities;

/// <summary>Valid baseline messages; tests derive edge cases with <c>with</c> expressions.</summary>
internal static class TestMessages
{
    /// <summary>Latitude of Ankara (39.9255°) in the wire unit of 1/360000 degree.</summary>
    public const long AnkaraLatitude = 14_373_180;

    /// <summary>Longitude of Ankara (32.8663°) in the wire unit of 1/360000 degree.</summary>
    public const long AnkaraLongitude = 11_831_868;

    public static Message1 ValidMessage1() => new(
        UnitReferenceNumber: 1234,
        FirstName: "Ayşe",
        UnitNo: 42u,
        LastName: "Yılmaz",
        Rank: Rank.Lieutenant);

    public static Message2 ValidMessage2() => new(
        UnitReferenceNumber: 77,
        PositionValidity: Message2.PositionValid,
        Latitude: AnkaraLatitude,
        Longitude: AnkaraLongitude,
        Altitude: 938);
}
