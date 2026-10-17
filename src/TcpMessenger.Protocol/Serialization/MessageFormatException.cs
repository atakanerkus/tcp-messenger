namespace TcpMessenger.Protocol.Serialization;

/// <summary>Thrown when bytes or a message cannot be represented in the wire format.</summary>
public sealed class MessageFormatException : Exception
{
    public MessageFormatException()
    {
    }

    public MessageFormatException(string message)
        : base(message)
    {
    }

    public MessageFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
