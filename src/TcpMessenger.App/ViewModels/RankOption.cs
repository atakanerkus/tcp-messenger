using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.App.ViewModels;

/// <summary>One entry of the Rütbe combo box.</summary>
/// <param name="Value">Enum value written to the wire.</param>
/// <param name="Display">"0 – Üsteğmen" style label.</param>
public sealed record RankOption(Rank Value, string Display)
{
    /// <summary>All defined ranks in wire order.</summary>
    public static IReadOnlyList<RankOption> All { get; } =
        Enum.GetValues<Rank>()
            .OrderBy(r => (short)r)
            .Select(r => new RankOption(r, $"{(short)r} – {r.ToDisplayName()}"))
            .ToList();
}
