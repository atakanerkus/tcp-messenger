using System.Net;
using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary>In-memory <see cref="IConnection"/>: records sends, lets tests inject received bytes and closes.</summary>
internal sealed class FakeConnection : IConnection
{
    private readonly List<byte[]> _sent = [];

    public EndPoint? RemoteEndPoint { get; init; } = new IPEndPoint(IPAddress.Loopback, 54321);

    public bool IsOpen { get; private set; } = true;

    /// <summary>How many times <see cref="Start"/> was called.</summary>
    public int StartCount { get; private set; }

    /// <summary>When set, every <see cref="SendAsync"/> throws this.</summary>
    public Exception? SendException { get; set; }

    /// <summary>When set, <see cref="SendAsync"/> does not complete until this does (simulates a slow link).</summary>
    public TaskCompletionSource? SendGate { get; set; }

    /// <summary>Payloads passed to <see cref="SendAsync"/>, in order.</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    public event EventHandler<Exception?>? Closed;

    public void Start() => StartCount++;

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The connection is closed.");
        }

        if (SendException is not null)
        {
            throw SendException;
        }

        lock (_sent)
        {
            _sent.Add(data.ToArray());
        }

        return SendGate?.Task ?? Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        SimulateRemoteClose(error: null);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        SimulateRemoteClose(error: null);
        return ValueTask.CompletedTask;
    }

    /// <summary>Delivers bytes as if they had been read from the socket.</summary>
    public void SimulateReceive(params byte[] bytes) => DataReceived?.Invoke(this, bytes);

    /// <summary>Ends the connection as the peer or the network would.</summary>
    public void SimulateRemoteClose(Exception? error)
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Closed?.Invoke(this, error);
    }
}
