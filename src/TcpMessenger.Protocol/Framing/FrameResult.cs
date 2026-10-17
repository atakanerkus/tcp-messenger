namespace TcpMessenger.Protocol.Framing;

/// <summary>
/// Outcome of feeding bytes to an <see cref="Abstractions.IFrameParser"/>. Closed hierarchy:
/// either a complete frame of a defined message or an undefined identifier.
/// </summary>
public abstract record FrameResult
{
    private FrameResult()
    {
    }

    /// <summary>A complete frame of a defined message, ready for <see cref="Abstractions.IMessageCodec.Decode"/>.</summary>
    /// <param name="MessageId">Identifier read from the first four bytes.</param>
    /// <param name="Bytes">Exactly the bytes of this message, nothing more.</param>
    public sealed record Complete(int MessageId, ReadOnlyMemory<byte> Bytes) : FrameResult;

    /// <summary>
    /// An undefined identifier was read. Everything that was buffered at that moment has been
    /// discarded so that the parser can resynchronize with the next frame.
    /// </summary>
    /// <param name="MessageId">The unknown identifier.</param>
    /// <param name="DiscardedBytes">All bytes dropped from the buffer, for display in the traffic log.</param>
    public sealed record Undefined(int MessageId, ReadOnlyMemory<byte> DiscardedBytes) : FrameResult;
}
