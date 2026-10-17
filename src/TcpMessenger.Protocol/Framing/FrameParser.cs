using System.Buffers.Binary;
using System.Runtime.InteropServices;
using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Protocol.Framing;

/// <summary>
/// Stateful stream splitter. Not thread-safe: a connection owns exactly one parser and feeds it
/// from its receive loop.
/// </summary>
public sealed class FrameParser : IFrameParser
{
    private readonly List<byte> _buffer = [];

    /// <inheritdoc />
    public IReadOnlyList<FrameResult> Append(ReadOnlySpan<byte> data)
    {
        _buffer.AddRange(data);

        var results = new List<FrameResult>();
        while (TryTakeNext(results))
        {
            // Keep draining until the buffer holds less than one full frame.
        }

        return results;
    }

    /// <inheritdoc />
    public void Reset() => _buffer.Clear();

    private bool TryTakeNext(List<FrameResult> results)
    {
        if (_buffer.Count < MessageLayout.MessageIdSize)
        {
            return false;
        }

        var buffered = CollectionsMarshal.AsSpan(_buffer);
        int messageId = BinaryPrimitives.ReadInt32BigEndian(buffered);

        if (!MessageLayout.TryGetSize(messageId, out int size))
        {
            results.Add(new FrameResult.Undefined(messageId, buffered.ToArray()));
            _buffer.Clear();
            return false;
        }

        if (buffered.Length < size)
        {
            return false;
        }

        results.Add(new FrameResult.Complete(messageId, buffered[..size].ToArray()));
        _buffer.RemoveRange(0, size);
        return true;
    }
}
