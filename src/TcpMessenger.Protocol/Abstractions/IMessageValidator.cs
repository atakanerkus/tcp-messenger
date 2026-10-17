using TcpMessenger.Protocol.Validation;

namespace TcpMessenger.Protocol.Abstractions;

/// <summary>
/// Checks message fields against the value ranges given in the task specification.
/// Used before sending (to block invalid input) and after receiving (to flag suspicious data).
/// </summary>
public interface IMessageValidator
{
    /// <summary>Returns one error per violated field; an empty list means the message is valid.</summary>
    IReadOnlyList<FieldError> Validate(IMessage message);
}
