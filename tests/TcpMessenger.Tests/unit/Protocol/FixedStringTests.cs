using TcpMessenger.Protocol.Serialization;

namespace TcpMessenger.Tests.Unit.Protocol;

[Trait("Category", "Unit")]
public sealed class FixedStringTests
{
    [Fact]
    public void Write_ShortValue_PadsRemainderWithZeros()
    {
        var buffer = new byte[25];
        buffer.AsSpan().Fill(0xAA); // pre-fill to prove padding is written, not assumed

        FixedString.Write(buffer, "AB");

        Assert.Equal((byte)'A', buffer[0]);
        Assert.Equal((byte)'B', buffer[1]);
        Assert.All(buffer[2..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Write_ExactlyFieldSize_FillsWholeField()
    {
        var buffer = new byte[25];
        string value = new('x', 25);

        FixedString.Write(buffer, value);

        Assert.All(buffer, b => Assert.Equal((byte)'x', b));
    }

    [Fact]
    public void Write_OneByteTooLong_Throws()
    {
        var buffer = new byte[25];
        string value = new('x', 26);

        Assert.Throws<MessageFormatException>(() => FixedString.Write(buffer, value));
    }

    [Fact]
    public void Write_TurkishCharactersCountAsTwoBytes()
    {
        // "ş" is 2 bytes in UTF-8, so 13 of them need 26 bytes and do not fit.
        Assert.Equal(26, FixedString.GetByteCount(new string('ş', 13)));
        Assert.False(FixedString.Fits(new string('ş', 13), 25));
        Assert.True(FixedString.Fits(new string('ş', 12), 25));
    }

    [Fact]
    public void Read_StripsTrailingZerosOnly()
    {
        var buffer = new byte[25];
        FixedString.Write(buffer, "Gökçe");

        string value = FixedString.Read(buffer);

        Assert.Equal("Gökçe", value);
    }

    [Fact]
    public void Read_AllZeros_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, FixedString.Read(new byte[25]));
    }

    [Fact]
    public void Read_InvalidUtf8_DoesNotThrow()
    {
        byte[] invalid = [0xFF, 0xFE, 0x41];

        string value = FixedString.Read(invalid);

        Assert.EndsWith("A", value, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_PreservesValue()
    {
        var buffer = new byte[25];
        const string original = "Çağrı Şentürk";

        FixedString.Write(buffer, original);

        Assert.Equal(original, FixedString.Read(buffer));
    }
}
