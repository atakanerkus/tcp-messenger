using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;

namespace TcpMessenger.Tests.Unit.Protocol;

[Trait("Category", "Unit")]
public sealed class MessageLayoutTests
{
    [Fact]
    public void Message1_FieldOffsets_AreContiguousAndSumTo64Bytes()
    {
        Assert.Equal(0, MessageLayout.Message1.IdOffset);
        Assert.Equal(4, MessageLayout.Message1.UnitReferenceNumberOffset);
        Assert.Equal(8, MessageLayout.Message1.FirstNameOffset);
        Assert.Equal(8 + 25, MessageLayout.Message1.UnitNoOffset);
        Assert.Equal(33 + 4, MessageLayout.Message1.LastNameOffset);
        Assert.Equal(37 + 25, MessageLayout.Message1.RankOffset);
        Assert.Equal(62 + 2, MessageLayout.Message1.Size);
    }

    [Fact]
    public void Message2_FieldOffsets_AreContiguousAndSumTo29Bytes()
    {
        Assert.Equal(0, MessageLayout.Message2.IdOffset);
        Assert.Equal(4, MessageLayout.Message2.UnitReferenceNumberOffset);
        Assert.Equal(8, MessageLayout.Message2.PositionValidityOffset);
        Assert.Equal(8 + 1, MessageLayout.Message2.LatitudeOffset);
        Assert.Equal(9 + 8, MessageLayout.Message2.LongitudeOffset);
        Assert.Equal(17 + 8, MessageLayout.Message2.AltitudeOffset);
        Assert.Equal(25 + 4, MessageLayout.Message2.Size);
    }

    [Theory]
    [InlineData(MessageId.Message1, 64)]
    [InlineData(MessageId.Message2, 29)]
    public void TryGetSize_DefinedId_ReturnsFixedSize(int id, int expectedSize)
    {
        bool found = MessageLayout.TryGetSize(id, out int size);

        Assert.True(found);
        Assert.Equal(expectedSize, size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void TryGetSize_UndefinedId_ReturnsFalse(int id)
    {
        bool found = MessageLayout.TryGetSize(id, out int size);

        Assert.False(found);
        Assert.Equal(0, size);
        Assert.False(MessageId.IsDefined(id));
    }
}
