using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Tests.TestUtilities;

namespace TcpMessenger.Tests.Unit.Protocol;

[Trait("Category", "Unit")]
public sealed class FrameParserTests
{
    private readonly BinaryMessageCodec _codec = new();
    private readonly FrameParser _parser = new();

    [Fact]
    public void Append_SingleCompleteFrame_ReturnsOneCompleteResult()
    {
        byte[] frame = _codec.Encode(TestMessages.ValidMessage1());

        var results = _parser.Append(frame);

        var complete = Assert.IsType<FrameResult.Complete>(Assert.Single(results));
        Assert.Equal(MessageId.Message1, complete.MessageId);
        Assert.Equal(frame, complete.Bytes.ToArray());
    }

    [Fact]
    public void Append_TwoFramesInOneChunk_ReturnsBothInOrder()
    {
        byte[] first = _codec.Encode(TestMessages.ValidMessage1());
        byte[] second = _codec.Encode(TestMessages.ValidMessage2());

        var results = _parser.Append([.. first, .. second]);

        Assert.Collection(results,
            r => Assert.Equal(MessageId.Message1, Assert.IsType<FrameResult.Complete>(r).MessageId),
            r => Assert.Equal(MessageId.Message2, Assert.IsType<FrameResult.Complete>(r).MessageId));
    }

    [Fact]
    public void Append_FrameSplitAcrossThreeChunks_ReturnsFrameOnlyWhenComplete()
    {
        byte[] frame = _codec.Encode(TestMessages.ValidMessage2());

        var afterFirst = _parser.Append(frame[..2]);    // not even the full identifier yet
        var afterSecond = _parser.Append(frame[2..20]); // identifier known, body incomplete
        var afterThird = _parser.Append(frame[20..]);   // completes the frame

        Assert.Empty(afterFirst);
        Assert.Empty(afterSecond);
        var complete = Assert.IsType<FrameResult.Complete>(Assert.Single(afterThird));
        Assert.Equal(frame, complete.Bytes.ToArray());
    }

    [Fact]
    public void Append_FrameBoundaryInsideSecondFrame_CarriesRemainderToNextCall()
    {
        byte[] first = _codec.Encode(TestMessages.ValidMessage2());
        byte[] second = _codec.Encode(TestMessages.ValidMessage1());
        byte[] stream = [.. first, .. second];

        var firstCall = _parser.Append(stream[..40]);  // whole first frame + 11 bytes of the second
        var secondCall = _parser.Append(stream[40..]);

        Assert.Equal(MessageId.Message2, Assert.IsType<FrameResult.Complete>(Assert.Single(firstCall)).MessageId);
        var complete = Assert.IsType<FrameResult.Complete>(Assert.Single(secondCall));
        Assert.Equal(second, complete.Bytes.ToArray());
    }

    [Fact]
    public void Append_UndefinedIdentifier_ReportsUndefinedAndDiscardsEverything()
    {
        byte[] garbage = "HELLO"u8.ToArray(); // 0x48454C4C is not a defined id

        var results = _parser.Append(garbage);

        var undefined = Assert.IsType<FrameResult.Undefined>(Assert.Single(results));
        Assert.Equal(0x48454C4C, undefined.MessageId);
        Assert.Equal(garbage, undefined.DiscardedBytes.ToArray());
    }

    [Fact]
    public void Append_ValidFrameFollowedByGarbage_ReturnsCompleteThenUndefined()
    {
        byte[] frame = _codec.Encode(TestMessages.ValidMessage1());
        byte[] garbage = [0x00, 0x00, 0x00, 0x07, 0xDE, 0xAD];

        var results = _parser.Append([.. frame, .. garbage]);

        Assert.Collection(results,
            r => Assert.IsType<FrameResult.Complete>(r),
            r =>
            {
                var undefined = Assert.IsType<FrameResult.Undefined>(r);
                Assert.Equal(7, undefined.MessageId);
                Assert.Equal(garbage, undefined.DiscardedBytes.ToArray());
            });
    }

    [Fact]
    public void Append_AfterUndefined_ParserRecoversWithNextValidFrame()
    {
        _parser.Append([0xFF, 0xFF, 0xFF, 0xFF]);
        byte[] frame = _codec.Encode(TestMessages.ValidMessage2());

        var results = _parser.Append(frame);

        var complete = Assert.IsType<FrameResult.Complete>(Assert.Single(results));
        Assert.Equal(frame, complete.Bytes.ToArray());
    }

    [Fact]
    public void Append_LessThanFourBytes_WaitsWithoutReporting()
    {
        var results = _parser.Append([0x00, 0x00, 0x00]);

        Assert.Empty(results);
    }

    [Fact]
    public void Reset_DropsPartialFrame()
    {
        byte[] frame = _codec.Encode(TestMessages.ValidMessage1());
        _parser.Append(frame[..30]);

        _parser.Reset();
        var results = _parser.Append(frame);

        var complete = Assert.IsType<FrameResult.Complete>(Assert.Single(results));
        Assert.Equal(frame, complete.Bytes.ToArray());
    }

    [Fact]
    public void Append_ManyFramesByteByByte_ReturnsExactlyOnePerFrame()
    {
        byte[] stream =
        [
            .. _codec.Encode(TestMessages.ValidMessage1()),
            .. _codec.Encode(TestMessages.ValidMessage2()),
            .. _codec.Encode(TestMessages.ValidMessage2()),
        ];

        var all = new List<FrameResult>();
        foreach (byte b in stream)
        {
            all.AddRange(_parser.Append([b]));
        }

        Assert.Equal(3, all.Count);
        Assert.All(all, r => Assert.IsType<FrameResult.Complete>(r));
    }
}
