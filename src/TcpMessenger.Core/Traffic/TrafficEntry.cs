using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Validation;

namespace TcpMessenger.Core.Traffic;

/// <summary>One row of the traffic log. Immutable.</summary>
/// <param name="Timestamp">Local time the message was sent or received.</param>
/// <param name="Direction">Transmit or Receive.</param>
/// <param name="Kind">Defined message or undefined bytes.</param>
/// <param name="MessageName">Display name, e.g. "Mesaj 1" or "Tanımsız (7)".</param>
/// <param name="Message">Decoded message; null for undefined bytes.</param>
/// <param name="RawBytes">Exact bytes on the wire.</param>
/// <param name="Errors">Range violations found by the validator; empty when the fields are valid.</param>
public sealed record TrafficEntry(
    DateTimeOffset Timestamp,
    TrafficDirection Direction,
    TrafficEntryKind Kind,
    string MessageName,
    IMessage? Message,
    ReadOnlyMemory<byte> RawBytes,
    IReadOnlyList<FieldError> Errors)
{
    /// <summary>"Transmit" or "Receive", exactly as the task specification spells them.</summary>
    public string DirectionLabel => Direction == TrafficDirection.Transmit ? "Transmit" : "Receive";

    /// <summary>True for a defined message whose every field is within range.</summary>
    public bool IsValid => Kind == TrafficEntryKind.Message && Errors.Count == 0;

    /// <summary>One-line summary for the list, e.g. "Transmit Mesaj 1".</summary>
    public string Summary => $"{DirectionLabel} {MessageName}";

    /// <summary>Creates the entry for a message this application sent.</summary>
    public static TrafficEntry Transmitted(DateTimeOffset timestamp, IMessage message, ReadOnlyMemory<byte> bytes, IReadOnlyList<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new TrafficEntry(timestamp, TrafficDirection.Transmit, TrafficEntryKind.Message, message.Name, message, bytes, errors);
    }

    /// <summary>Creates the entry for a defined message received from the peer.</summary>
    public static TrafficEntry Received(DateTimeOffset timestamp, IMessage message, ReadOnlyMemory<byte> bytes, IReadOnlyList<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new TrafficEntry(timestamp, TrafficDirection.Receive, TrafficEntryKind.Message, message.Name, message, bytes, errors);
    }

    /// <summary>Creates the entry for bytes whose identifier is not defined.</summary>
    public static TrafficEntry Undefined(DateTimeOffset timestamp, int messageId, ReadOnlyMemory<byte> discardedBytes) =>
        new(timestamp, TrafficDirection.Receive, TrafficEntryKind.Undefined, MessageId.GetName(messageId), Message: null, discardedBytes, Errors: []);
}
