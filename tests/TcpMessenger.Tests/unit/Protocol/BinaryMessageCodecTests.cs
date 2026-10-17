using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Tests.TestUtilities;

namespace TcpMessenger.Tests.Unit.Protocol;

[Trait("Category", "Unit")]
public sealed class BinaryMessageCodecTests
{
    private readonly BinaryMessageCodec _codec = new();

    [Fact]
    public void Encode_Message1_Produces64Bytes()
    {
        byte[] bytes = _codec.Encode(TestMessages.ValidMessage1());

        Assert.Equal(MessageLayout.Message1.Size, bytes.Length);
    }

    [Fact]
    public void Encode_Message2_Produces29Bytes()
    {
        byte[] bytes = _codec.Encode(TestMessages.ValidMessage2());

        Assert.Equal(MessageLayout.Message2.Size, bytes.Length);
    }

    [Fact]
    public void Encode_Message2_MatchesByteVectorFromProtocolDocument()
    {
        // docs/protocol.md example: Ref=1, valid=1, lat=0, lon=0, alt=10000
        var message = new Message2(
            UnitReferenceNumber: 1,
            PositionValidity: Message2.PositionValid,
            Latitude: 0,
            Longitude: 0,
            Altitude: 10_000);

        byte[] bytes = _codec.Encode(message);

        byte[] expected =
        [
            0x00, 0x00, 0x00, 0x02,                         // Message ID = 2
            0x00, 0x00, 0x00, 0x01,                         // UnitReferenceNumber = 1
            0x01,                                           // PositionValidity = true
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Latitude = 0
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Longitude = 0
            0x00, 0x00, 0x27, 0x10,                         // Altitude = 10000
        ];
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Encode_Message1_WritesEveryFieldBigEndianAtDocumentedOffset()
    {
        var message = new Message1(
            UnitReferenceNumber: -1,            // FF FF FF FF
            FirstName: "AB",                    // 41 42 + 23 × 00
            UnitNo: 0x0102_0304u,               // 01 02 03 04
            LastName: string.Empty,             // 25 × 00
            Rank: Rank.SecondLieutenant);       // 00 02

        byte[] bytes = _codec.Encode(message);

        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x01 }, bytes[0..4]);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, bytes[4..8]);
        Assert.Equal((byte)'A', bytes[8]);
        Assert.Equal((byte)'B', bytes[9]);
        Assert.All(bytes[10..33], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, bytes[33..37]);
        Assert.All(bytes[37..62], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 0x00, 0x02 }, bytes[62..64]);
    }

    [Fact]
    public void RoundTrip_Message1_PreservesEveryField()
    {
        var original = TestMessages.ValidMessage1();

        var decoded = _codec.Decode(_codec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_Message2_PreservesEveryField()
    {
        var original = TestMessages.ValidMessage2();

        var decoded = _codec.Decode(_codec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_Message1_ExtremeNumericValues()
    {
        var original = TestMessages.ValidMessage1() with
        {
            UnitReferenceNumber = int.MinValue,
            UnitNo = uint.MaxValue,
            Rank = (Rank)short.MaxValue,
        };

        var decoded = Assert.IsType<Message1>(_codec.Decode(_codec.Encode(original)));

        Assert.Equal(int.MinValue, decoded.UnitReferenceNumber);
        Assert.Equal(uint.MaxValue, decoded.UnitNo);
        Assert.Equal((Rank)short.MaxValue, decoded.Rank);
    }

    [Fact]
    public void RoundTrip_Message2_ExtremeNumericValues()
    {
        var original = TestMessages.ValidMessage2() with
        {
            UnitReferenceNumber = int.MaxValue,
            PositionValidity = 0xFF,
            Latitude = long.MinValue,
            Longitude = long.MaxValue,
            Altitude = int.MinValue,
        };

        var decoded = _codec.Decode(_codec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RoundTrip_Message1_TurkishNames()
    {
        var original = TestMessages.ValidMessage1() with { FirstName = "Şükrü İlhan", LastName = "Öztürkoğlu" };

        var decoded = _codec.Decode(_codec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_NameLongerThan25Bytes_Throws()
    {
        var message = TestMessages.ValidMessage1() with { FirstName = new string('a', 26) };

        Assert.Throws<MessageFormatException>(() => _codec.Encode(message));
    }

    [Fact]
    public void Encode_UnsupportedMessageType_Throws()
    {
        var unknown = new UnknownMessage();

        Assert.Throws<MessageFormatException>(() => _codec.Encode(unknown));
    }

    [Fact]
    public void Decode_UndefinedId_Throws()
    {
        byte[] frame = [0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x00];

        Assert.Throws<MessageFormatException>(() => _codec.Decode(frame));
    }

    [Theory]
    [InlineData(MessageLayout.Message1.Size - 1)]
    [InlineData(MessageLayout.Message1.Size + 1)]
    public void Decode_Message1WrongLength_Throws(int length)
    {
        var frame = new byte[length];
        frame[3] = MessageId.Message1;

        Assert.Throws<MessageFormatException>(() => _codec.Decode(frame));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Decode_ShorterThanIdentifier_Throws(int length)
    {
        Assert.Throws<MessageFormatException>(() => _codec.Decode(new byte[length]));
    }

    private sealed class UnknownMessage : TcpMessenger.Protocol.Abstractions.IMessage
    {
        public int Id => 99;

        public string Name => "Unknown";
    }
}
