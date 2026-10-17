using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using Constraints1 = TcpMessenger.Protocol.Validation.FieldConstraints.Message1;
using Constraints2 = TcpMessenger.Protocol.Validation.FieldConstraints.Message2;

namespace TcpMessenger.Protocol.Validation;

/// <summary>Range validation exactly as tabulated in the task specification. Stateless and thread-safe.</summary>
public sealed class MessageValidator : IMessageValidator
{
    /// <inheritdoc />
    public IReadOnlyList<FieldError> Validate(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var errors = new List<FieldError>();
        switch (message)
        {
            case Message1 m:
                ValidateMessage1(m, errors);
                break;
            case Message2 m:
                ValidateMessage2(m, errors);
                break;
            default:
                errors.Add(new FieldError(nameof(IMessage.Id), $"Mesaj tipi desteklenmiyor: {message.GetType().Name}."));
                break;
        }

        return errors;
    }

    private static void ValidateMessage1(Message1 message, List<FieldError> errors)
    {
        CheckRange(errors, nameof(Message1.UnitReferenceNumber), message.UnitReferenceNumber,
            Constraints1.UnitReferenceNumberMin, Constraints1.UnitReferenceNumberMax);
        CheckName(errors, nameof(Message1.FirstName), message.FirstName);
        CheckName(errors, nameof(Message1.LastName), message.LastName);

        if (!Enum.IsDefined(message.Rank))
        {
            errors.Add(new FieldError(nameof(Message1.Rank),
                $"Rütbe değeri {(short)message.Rank} tanımlı değil (0, 1 veya 2 olmalı)."));
        }
    }

    private static void ValidateMessage2(Message2 message, List<FieldError> errors)
    {
        CheckRange(errors, nameof(Message2.UnitReferenceNumber), message.UnitReferenceNumber,
            Constraints2.UnitReferenceNumberMin, Constraints2.UnitReferenceNumberMax);

        if (message.PositionValidity is not (Message2.PositionValid or Message2.PositionInvalid))
        {
            errors.Add(new FieldError(nameof(Message2.PositionValidity),
                $"Konum geçerliliği {message.PositionValidity} olamaz (0 veya 1 olmalı)."));
        }

        CheckRange(errors, nameof(Message2.Latitude), message.Latitude, Constraints2.LatitudeMin, Constraints2.LatitudeMax);
        CheckRange(errors, nameof(Message2.Longitude), message.Longitude, Constraints2.LongitudeMin, Constraints2.LongitudeMax);
        CheckRange(errors, nameof(Message2.Altitude), message.Altitude, Constraints2.AltitudeMin, Constraints2.AltitudeMax);
    }

    private static void CheckRange<T>(List<FieldError> errors, string fieldName, T value, T min, T max)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            errors.Add(new FieldError(fieldName, $"{value} değeri [{min}, {max}] aralığının dışında."));
        }
    }

    private static void CheckName(List<FieldError> errors, string fieldName, string? value)
    {
        if (value is null)
        {
            errors.Add(new FieldError(fieldName, "Değer boş olamaz."));
            return;
        }

        int byteCount = FixedString.GetByteCount(value);
        if (byteCount > FieldConstraints.NameMaxBytes)
        {
            errors.Add(new FieldError(fieldName,
                $"UTF-8 karşılığı {byteCount} byte; en fazla {FieldConstraints.NameMaxBytes} byte olabilir."));
        }
    }
}
