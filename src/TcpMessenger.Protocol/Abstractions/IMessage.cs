namespace TcpMessenger.Protocol.Abstractions;

/// <summary>
/// A message defined in the task specification. Every message starts with a 4-byte identifier
/// that determines its fixed wire size (see <see cref="Framing.MessageLayout"/>).
/// </summary>
public interface IMessage
{
    /// <summary>Numeric identifier written as the first field on the wire.</summary>
    int Id { get; }

    /// <summary>Human readable name shown in the traffic log ("Mesaj 1", "Mesaj 2").</summary>
    string Name { get; }
}
