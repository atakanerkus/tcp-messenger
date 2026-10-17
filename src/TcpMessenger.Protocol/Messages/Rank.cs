namespace TcpMessenger.Protocol.Messages;

/// <summary>Rütbe. Encoded as a 2-byte signed integer on the wire.</summary>
public enum Rank : short
{
    /// <summary>Üsteğmen.</summary>
    FirstLieutenant = 0,

    /// <summary>Teğmen.</summary>
    Lieutenant = 1,

    /// <summary>Asteğmen.</summary>
    SecondLieutenant = 2,
}
