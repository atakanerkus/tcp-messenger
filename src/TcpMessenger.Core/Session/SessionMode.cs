namespace TcpMessenger.Core.Session;

/// <summary>How the application takes part in the conversation.</summary>
public enum SessionMode
{
    /// <summary>Listens and accepts one client.</summary>
    Server,

    /// <summary>Connects to a server.</summary>
    Client,
}
