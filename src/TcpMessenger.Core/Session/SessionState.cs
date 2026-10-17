namespace TcpMessenger.Core.Session;

/// <summary>Lifecycle of a <see cref="Abstractions.IMessengerSession"/>.</summary>
public enum SessionState
{
    /// <summary>Nothing is open. Server or client can be started.</summary>
    Idle,

    /// <summary>Server mode, waiting for a client.</summary>
    Listening,

    /// <summary>Client mode, connect attempt in progress.</summary>
    Connecting,

    /// <summary>A peer is connected; messages can be exchanged.</summary>
    Connected,
}
