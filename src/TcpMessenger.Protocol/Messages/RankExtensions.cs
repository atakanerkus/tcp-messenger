namespace TcpMessenger.Protocol.Messages;

/// <summary>Display helpers for <see cref="Rank"/>.</summary>
public static class RankExtensions
{
    /// <summary>Turkish display name as written in the task specification.</summary>
    public static string ToDisplayName(this Rank rank) => rank switch
    {
        Rank.FirstLieutenant => "Üsteğmen",
        Rank.Lieutenant => "Teğmen",
        Rank.SecondLieutenant => "Asteğmen",
        _ => $"Tanımsız ({(short)rank})",
    };
}
