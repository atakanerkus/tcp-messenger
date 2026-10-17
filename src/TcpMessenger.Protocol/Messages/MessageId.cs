namespace TcpMessenger.Protocol.Messages;

/// <summary>Identifiers of the messages defined in the task specification.</summary>
public static class MessageId
{
    /// <summary>Birinci Mesaj.</summary>
    public const int Message1 = 1;

    /// <summary>İkinci Mesaj.</summary>
    public const int Message2 = 2;

    /// <summary>True when the identifier belongs to a defined message.</summary>
    public static bool IsDefined(int id) => id is Message1 or Message2;

    /// <summary>Display name used in the traffic log.</summary>
    public static string GetName(int id) => id switch
    {
        Message1 => "Mesaj 1",
        Message2 => "Mesaj 2",
        _ => $"Tanımsız ({id})",
    };
}
