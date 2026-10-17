using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities;

namespace TcpMessenger.Tests.Unit.Protocol;

[Trait("Category", "Unit")]
public sealed class MessageValidatorTests
{
    private readonly MessageValidator _validator = new();

    [Fact]
    public void Validate_ValidMessage1_ReturnsNoErrors()
    {
        Assert.Empty(_validator.Validate(TestMessages.ValidMessage1()));
    }

    [Fact]
    public void Validate_ValidMessage2_ReturnsNoErrors()
    {
        Assert.Empty(_validator.Validate(TestMessages.ValidMessage2()));
    }

    // ---- Message 1 -------------------------------------------------------------------------

    [Theory]
    [InlineData(-1000)]
    [InlineData(0)]
    [InlineData(9999)]
    public void Validate_Message1UnitReferenceNumber_Boundary_IsValid(int value)
    {
        var message = TestMessages.ValidMessage1() with { UnitReferenceNumber = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData(-1001)]
    [InlineData(10000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Validate_Message1UnitReferenceNumber_OutOfRange_IsReported(int value)
    {
        var message = TestMessages.ValidMessage1() with { UnitReferenceNumber = value };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message1.UnitReferenceNumber), error.FieldName);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void Validate_Message1UnitNo_HasNoConstraint(uint value)
    {
        var message = TestMessages.ValidMessage1() with { UnitNo = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData(Rank.FirstLieutenant)]
    [InlineData(Rank.Lieutenant)]
    [InlineData(Rank.SecondLieutenant)]
    public void Validate_Message1Rank_Defined_IsValid(Rank rank)
    {
        var message = TestMessages.ValidMessage1() with { Rank = rank };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData((short)3)]
    [InlineData((short)-1)]
    [InlineData(short.MaxValue)]
    public void Validate_Message1Rank_Undefined_IsReported(short raw)
    {
        var message = TestMessages.ValidMessage1() with { Rank = (Rank)raw };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message1.Rank), error.FieldName);
    }

    [Fact]
    public void Validate_Message1Names_Exactly25Bytes_IsValid()
    {
        var message = TestMessages.ValidMessage1() with
        {
            FirstName = new string('a', 25),
            LastName = new string('ş', 12) + "a", // 12 × 2 + 1 = 25 bytes
        };

        Assert.Empty(_validator.Validate(message));
    }

    [Fact]
    public void Validate_Message1Names_Over25Bytes_IsReportedPerField()
    {
        var message = TestMessages.ValidMessage1() with
        {
            FirstName = new string('a', 26),
            LastName = new string('ş', 13), // 26 bytes although only 13 characters
        };

        var errors = _validator.Validate(message);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.FieldName == nameof(Message1.FirstName));
        Assert.Contains(errors, e => e.FieldName == nameof(Message1.LastName));
    }

    [Fact]
    public void Validate_Message1EmptyName_IsValid()
    {
        var message = TestMessages.ValidMessage1() with { FirstName = string.Empty };

        Assert.Empty(_validator.Validate(message));
    }

    // ---- Message 2 -------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(9999)]
    public void Validate_Message2UnitReferenceNumber_Boundary_IsValid(int value)
    {
        var message = TestMessages.ValidMessage2() with { UnitReferenceNumber = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    public void Validate_Message2UnitReferenceNumber_OutOfRange_IsReported(int value)
    {
        var message = TestMessages.ValidMessage2() with { UnitReferenceNumber = value };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message2.UnitReferenceNumber), error.FieldName);
    }

    [Theory]
    [InlineData(Message2.PositionInvalid)]
    [InlineData(Message2.PositionValid)]
    public void Validate_Message2PositionValidity_ZeroOrOne_IsValid(byte value)
    {
        var message = TestMessages.ValidMessage2() with { PositionValidity = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)0xFF)]
    public void Validate_Message2PositionValidity_Other_IsReported(byte value)
    {
        var message = TestMessages.ValidMessage2() with { PositionValidity = value };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message2.PositionValidity), error.FieldName);
    }

    [Theory]
    [InlineData(-32_400_000)]
    [InlineData(0)]
    [InlineData(32_400_000)]
    public void Validate_Message2Latitude_Boundary_IsValid(long value)
    {
        var message = TestMessages.ValidMessage2() with { Latitude = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData(-32_400_001)]
    [InlineData(32_400_001)]
    [InlineData(long.MinValue)]
    public void Validate_Message2Latitude_OutOfRange_IsReported(long value)
    {
        var message = TestMessages.ValidMessage2() with { Latitude = value };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message2.Latitude), error.FieldName);
    }

    [Theory]
    [InlineData(-64_800_000)]
    [InlineData(64_800_000)]
    public void Validate_Message2Longitude_Boundary_IsValid(long value)
    {
        var message = TestMessages.ValidMessage2() with { Longitude = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData(-64_800_001)]
    [InlineData(64_800_001)]
    [InlineData(long.MaxValue)]
    public void Validate_Message2Longitude_OutOfRange_IsReported(long value)
    {
        var message = TestMessages.ValidMessage2() with { Longitude = value };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message2.Longitude), error.FieldName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000)]
    public void Validate_Message2Altitude_Boundary_IsValid(int value)
    {
        var message = TestMessages.ValidMessage2() with { Altitude = value };

        Assert.Empty(_validator.Validate(message));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Validate_Message2Altitude_OutOfRange_IsReported(int value)
    {
        var message = TestMessages.ValidMessage2() with { Altitude = value };

        var error = Assert.Single(_validator.Validate(message));
        Assert.Equal(nameof(Message2.Altitude), error.FieldName);
    }

    [Fact]
    public void Validate_Message2AllFieldsInvalid_ReportsEveryField()
    {
        var message = new Message2(
            UnitReferenceNumber: 0,
            PositionValidity: 9,
            Latitude: long.MaxValue,
            Longitude: long.MinValue,
            Altitude: -5);

        var errors = _validator.Validate(message);

        Assert.Equal(5, errors.Count);
        Assert.Equal(
            [
                nameof(Message2.UnitReferenceNumber),
                nameof(Message2.PositionValidity),
                nameof(Message2.Latitude),
                nameof(Message2.Longitude),
                nameof(Message2.Altitude),
            ],
            errors.Select(e => e.FieldName));
    }

    [Fact]
    public void Validate_ErrorMessages_AreNonEmptyHumanReadableText()
    {
        var message = TestMessages.ValidMessage2() with { Altitude = 99_999 };

        var error = Assert.Single(_validator.Validate(message));

        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Contains("99999", error.Message, StringComparison.Ordinal);
    }
}
