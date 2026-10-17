namespace TcpMessenger.Protocol.Validation;

/// <summary>One violated constraint on one message field.</summary>
/// <param name="FieldName">Property name on the message record, e.g. <c>UnitReferenceNumber</c>.</param>
/// <param name="Message">Human readable explanation shown in the UI.</param>
public sealed record FieldError(string FieldName, string Message);
