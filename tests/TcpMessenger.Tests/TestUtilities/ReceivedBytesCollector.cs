using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Tests.TestUtilities;

/// <summary>
/// Accumulates <see cref="IConnection.DataReceived"/> chunks and completes once the expected
/// number of bytes has arrived, regardless of how TCP split them.
/// </summary>
internal sealed class ReceivedBytesCollector
{
    private readonly MemoryStream _received = new();
    private readonly int _expectedLength;
    private readonly TaskCompletionSource<byte[]> _completed = AsyncTestHelpers.NewSignal<byte[]>();

    public ReceivedBytesCollector(IConnection connection, int expectedLength)
    {
        _expectedLength = expectedLength;
        connection.DataReceived += OnDataReceived;
    }

    /// <summary>Completes with exactly the first <c>expectedLength</c> bytes received.</summary>
    public Task<byte[]> Bytes => _completed.Task;

    private void OnDataReceived(object? sender, ReadOnlyMemory<byte> chunk)
    {
        lock (_received)
        {
            _received.Write(chunk.Span);
            if (_received.Length >= _expectedLength)
            {
                _completed.TrySetResult(_received.ToArray()[.._expectedLength]);
            }
        }
    }
}
