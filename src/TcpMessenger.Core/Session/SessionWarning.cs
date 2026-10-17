namespace TcpMessenger.Core.Session;

/// <summary>Category of a <see cref="SessionWarning"/>.</summary>
public enum SessionWarningKind
{
    /// <summary>Bytes with an undefined message identifier arrived (task requirement: warn the user).</summary>
    UndefinedMessageReceived,

    /// <summary>A second client tried to connect while one was already connected (K5).</summary>
    ClientRejected,

    /// <summary>The peer closed the connection or it dropped.</summary>
    ConnectionClosed,

    /// <summary>An automatic reply could not be sent.</summary>
    AutoReplyFailed,
}

/// <summary>Something the user should be told that is not itself a message.</summary>
/// <param name="Timestamp">When it happened.</param>
/// <param name="Kind">Category.</param>
/// <param name="Message">Turkish, user-facing text.</param>
public sealed record SessionWarning(DateTimeOffset Timestamp, SessionWarningKind Kind, string Message);
