using TcpMessenger.App.Services;
using TcpMessenger.App.ViewModels;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Protocol.Validation;
using TcpMessenger.Tests.TestUtilities;

namespace TcpMessenger.Tests.Unit.App;

[Trait("Category", "Unit")]
public sealed class MessageDetailViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 14, 30, 15, 250, TimeSpan.FromHours(3));
    private readonly BinaryMessageCodec _codec = new();
    private readonly MessageValidator _validator = new();

    [Fact]
    public void Initially_ShowsHintAndNoFields()
    {
        var vm = new MessageDetailViewModel();

        Assert.False(vm.HasEntry);
        Assert.Empty(vm.Fields);
        Assert.Equal(string.Empty, vm.HexDump);
        Assert.Contains("seçin", vm.Header, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_Message1_ListsFieldsInSpecificationOrderWithTurkishNames()
    {
        var vm = new MessageDetailViewModel();
        var message = TestMessages.ValidMessage1() with { Rank = Rank.Lieutenant, UnitNo = 42 };

        vm.Show(Received(message));

        Assert.True(vm.HasEntry);
        Assert.Equal(
            ["Mesaj ID", "Birlik Referans Numarası", "Adı", "Unit No", "Soyadı", "Rütbe"],
            vm.Fields.Select(f => f.Name));
        Assert.Equal("1", vm.Fields[0].Value);
        Assert.Equal("Ayşe", vm.Fields[2].Value);
        Assert.Equal("42", vm.Fields[3].Value);
        Assert.Equal("1 – Teğmen", vm.Fields[5].Value);
        Assert.All(vm.Fields, f => Assert.False(f.HasError));
    }

    [Fact]
    public void Show_Message2_ListsFieldsAndDescribesValidityByte()
    {
        var vm = new MessageDetailViewModel();

        vm.Show(Received(TestMessages.ValidMessage2()));

        Assert.Equal(
            ["Mesaj ID", "Birlik Referans Numarası", "Birlik Konum Bilgisi Geçerliliği", "Enlem", "Boylam", "Yükseklik"],
            vm.Fields.Select(f => f.Name));
        Assert.Equal("1 – True", vm.Fields[2].Value);
        Assert.Equal("14373180", vm.Fields[3].Value);
    }

    [Fact]
    public void Show_OutOfRangeField_MarksOnlyThatRow()
    {
        var vm = new MessageDetailViewModel();
        var message = TestMessages.ValidMessage2() with { Altitude = 99_999 };

        vm.Show(Received(message));

        var altitude = vm.Fields.Single(f => f.Name == "Yükseklik");
        Assert.True(altitude.HasError);
        Assert.Contains("99999", altitude.Error, StringComparison.Ordinal);
        Assert.Equal(1, vm.Fields.Count(f => f.HasError));
    }

    [Fact]
    public void Show_UndefinedEntry_ExplainsTheUnknownId()
    {
        var vm = new MessageDetailViewModel();
        var entry = TrafficEntry.Undefined(T0, 0x48454C4C, "HELLO"u8.ToArray());

        vm.Show(entry);

        var row = Assert.Single(vm.Fields);
        Assert.Equal("Mesaj ID", row.Name);
        Assert.Contains("0x48454C4C", row.Value, StringComparison.Ordinal);
        Assert.True(row.HasError);
        Assert.StartsWith("Receive Tanımsız", vm.Header, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_Header_HasSummaryTimeAndLength()
    {
        var vm = new MessageDetailViewModel();

        vm.Show(Received(TestMessages.ValidMessage1()));

        Assert.Equal("Receive Mesaj 1 · 14:30:15.250 · 64 byte", vm.Header);
    }

    [Fact]
    public void Show_Null_ClearsEverything()
    {
        var vm = new MessageDetailViewModel();
        vm.Show(Received(TestMessages.ValidMessage1()));

        vm.Show(null);

        Assert.False(vm.HasEntry);
        Assert.Empty(vm.Fields);
        Assert.Equal(string.Empty, vm.HexDump);
    }

    [Fact]
    public void HexDump_FormatsSixteenBytesPerLineWithAsciiColumn()
    {
        byte[] bytes = [.. "ABCDEFGH"u8, 0x00, 0x01, 0xFF, 0x7F, 0x20, 0x7E, 0x0A, 0x41, 0x42];

        string dump = HexDump.Format(bytes);

        string[] lines = dump.Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("0000  41 42 43 44 45 46 47 48  00 01 FF 7F 20 7E 0A 41", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("|ABCDEFGH.... ~.A|", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("0010  42", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("|B|", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void HexDump_Empty_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, HexDump.Format(ReadOnlySpan<byte>.Empty));
    }

    private TrafficEntry Received(TcpMessenger.Protocol.Abstractions.IMessage message) =>
        TrafficEntry.Received(T0, message, _codec.Encode(message), _validator.Validate(message));
}
