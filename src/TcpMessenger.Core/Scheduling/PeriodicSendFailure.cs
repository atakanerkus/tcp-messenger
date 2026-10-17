namespace TcpMessenger.Core.Scheduling;

/// <summary>Raised by <see cref="Abstractions.IPeriodicSender.SendFailed"/>.</summary>
/// <param name="MessageId">Message type whose schedule was stopped.</param>
/// <param name="Error">What went wrong.</param>
public sealed record PeriodicSendFailure(int MessageId, Exception Error);
