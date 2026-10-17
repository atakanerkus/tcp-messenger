using System.Net;
using TcpMessenger.Core.Session;
using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.Core.Abstractions;

/// <summary>
/// The application's single conversation: runs as server or client, sends messages, decodes and
/// logs incoming traffic, answers automatically and reports anomalies. One instance per process.
/// </summary>
/// <remarks>Events may be raised on thread-pool threads; the UI must marshal them.</remarks>
public interface IMessengerSession : IAsyncDisposable
{
    /// <summary>Server or client while active, null when idle.</summary>
    SessionMode? Mode { get; }

    /// <summary>Current lifecycle state.</summary>
    SessionState State { get; }

    /// <summary>Port the server is bound to while listening or connected in server mode.</summary>
    int? LocalPort { get; }

    /// <summary>Address of the connected peer, when connected.</summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// When true, a received Mesaj 1 is answered with Mesaj 2 and vice versa (K4).
    /// Can be toggled at any time.
    /// </summary>
    bool AutoReplyEnabled { get; set; }

    /// <summary>Everything sent and received.</summary>
    ITrafficLog Log { get; }

    /// <summary>Raised after <see cref="State"/> changed.</summary>
    event EventHandler<SessionState>? StateChanged;

    /// <summary>Raised for anything the user should know about that is not a message.</summary>
    event EventHandler<SessionWarning>? WarningRaised;

    /// <summary>Starts listening as a server. Port 0 picks a free port.</summary>
    /// <exception cref="InvalidOperationException">The session is not idle.</exception>
    Task StartServerAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>Connects to a server as a client.</summary>
    /// <exception cref="InvalidOperationException">The session is not idle.</exception>
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>Closes the connection and stops listening. Safe to call in any state.</summary>
    Task StopAsync();

    /// <summary>Encodes, sends and logs a message.</summary>
    /// <exception cref="InvalidOperationException">No peer is connected.</exception>
    /// <exception cref="Protocol.Serialization.MessageFormatException">The message cannot be encoded.</exception>
    Task SendAsync(IMessage message, CancellationToken cancellationToken = default);
}
