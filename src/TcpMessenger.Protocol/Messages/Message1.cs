using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Protocol.Messages;

/// <summary>
/// Birinci Mesaj: personnel identity. 64 bytes on the wire
/// (see <see cref="Framing.MessageLayout.Message1"/> for offsets).
/// </summary>
/// <param name="UnitReferenceNumber">Birlik Referans Numarası, valid range −1000…9999.</param>
/// <param name="FirstName">Adı, at most 25 UTF-8 bytes.</param>
/// <param name="UnitNo">Unit No, unconstrained unsigned 32-bit value.</param>
/// <param name="LastName">Soyadı, at most 25 UTF-8 bytes.</param>
/// <param name="Rank">Rütbe.</param>
public sealed record Message1(
    int UnitReferenceNumber,
    string FirstName,
    uint UnitNo,
    string LastName,
    Rank Rank) : IMessage
{
    /// <inheritdoc />
    public int Id => MessageId.Message1;

    /// <inheritdoc />
    public string Name => MessageId.GetName(Id);
}
