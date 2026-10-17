namespace TcpMessenger.Core.Traffic;

/// <summary>What a traffic entry represents.</summary>
public enum TrafficEntryKind
{
    /// <summary>A defined message (possibly with out-of-range fields, see K6).</summary>
    Message,

    /// <summary>Bytes with an undefined message identifier, discarded by the frame parser.</summary>
    Undefined,
}
