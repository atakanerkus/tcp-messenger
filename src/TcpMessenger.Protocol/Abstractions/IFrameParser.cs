using TcpMessenger.Protocol.Framing;

namespace TcpMessenger.Protocol.Abstractions;

/// <summary>
/// Splits a TCP byte stream into message frames. TCP has no message boundaries, so the parser
/// reads the 4-byte identifier, looks up the fixed size of that message and waits until the
/// whole frame has arrived. An undefined identifier yields <see cref="FrameResult.Undefined"/>
/// and clears the buffer to resynchronize.
/// </summary>
public interface IFrameParser
{
    /// <summary>
    /// Appends bytes received from the stream and returns every result that became available,
    /// in arrival order. Incomplete trailing data is buffered until the next call.
    /// </summary>
    IReadOnlyList<FrameResult> Append(ReadOnlySpan<byte> data);

    /// <summary>Discards any buffered partial data.</summary>
    void Reset();
}
