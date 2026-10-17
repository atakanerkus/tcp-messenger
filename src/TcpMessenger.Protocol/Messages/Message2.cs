using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Protocol.Messages;

/// <summary>
/// İkinci Mesaj: unit position. 29 bytes on the wire
/// (see <see cref="Framing.MessageLayout.Message2"/> for offsets).
/// </summary>
/// <param name="UnitReferenceNumber">Birlik Referans Numarası, valid range 1…9999.</param>
/// <param name="PositionValidity">
/// Birlik Konum Bilgisi Geçerliliği as received on the wire: 1 = true, 0 = false.
/// Kept as the raw byte so that any other value can be reported by the validator.
/// </param>
/// <param name="Latitude">Enlem, valid range ±32 400 000.</param>
/// <param name="Longitude">Boylam, valid range ±64 800 000.</param>
/// <param name="Altitude">Yükseklik, valid range 0…10 000.</param>
public sealed record Message2(
    int UnitReferenceNumber,
    byte PositionValidity,
    long Latitude,
    long Longitude,
    int Altitude) : IMessage
{
    /// <summary>Wire value meaning the position is valid.</summary>
    public const byte PositionValid = 1;

    /// <summary>Wire value meaning the position is not valid.</summary>
    public const byte PositionInvalid = 0;

    /// <inheritdoc />
    public int Id => MessageId.Message2;

    /// <inheritdoc />
    public string Name => MessageId.GetName(Id);

    /// <summary>Convenience view of <see cref="PositionValidity"/>.</summary>
    public bool IsPositionValid => PositionValidity == PositionValid;
}
