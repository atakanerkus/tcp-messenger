using System.Net.Sockets;
using TcpMessenger.Tests.E2E.Fixtures;
using TcpMessenger.Tests.TestUtilities;

namespace TcpMessenger.Tests.E2E;

/// <summary>
/// The demo, automated: two real application windows on one machine, driven through UI Automation.
/// Requires an interactive desktop; excluded from CI (Category=E2E).
/// </summary>
[Trait("Category", "E2E")]
[Collection(E2ECollection.Name)]
public sealed class ServerClientMessagingTests : IDisposable
{
    private readonly int _port = PortAllocator.GetFreePort();
    private readonly List<AppInstance> _instances = [];

    public void Dispose()
    {
        foreach (var instance in _instances)
        {
            instance.Dispose();
        }
    }

    [Fact]
    public void ServerAndClient_ConnectThroughTheUi()
    {
        var server = LaunchServer(autoReply: true);
        var client = LaunchClient(autoReply: false);

        Assert.Equal("TCP Messenger — Server", server.Driver.Title);
        Assert.Equal("TCP Messenger — Client", client.Driver.Title);
        Assert.Equal("Bağlı değil", server.Driver.StatusText);

        server.Driver.ClickStart();
        server.Driver.WaitForStatusContaining("Dinleniyor");
        Assert.Contains(_port.ToString(System.Globalization.CultureInfo.InvariantCulture), server.Driver.StatusText, StringComparison.Ordinal);
        Assert.False(server.Driver.IsStartEnabled);
        Assert.True(server.Driver.IsStopEnabled);

        client.Driver.ClickStart();
        client.Driver.WaitForStatusContaining("Bağlı:");
        server.Driver.WaitForStatusContaining("Bağlı:");

        client.Driver.Message1.WaitForSendEnabled(true);
        Assert.True(server.Driver.Message1.IsSendEnabled);
    }

    [Fact]
    public void ClientSendsMessage1_ServerShowsReceiveAndAutoRepliesWithMessage2()
    {
        var (server, client) = ConnectPair(serverAutoReply: true, clientAutoReply: false);

        client.Driver.Message1.Open();
        client.Driver.Message1.SetField("UnitReferenceNumber", "1234");
        client.Driver.Message1.SetField("FirstName", "Zeynep");
        client.Driver.Message1.SetField("LastName", "Kaya");
        client.Driver.Message1.SetField("UnitNo", "77");
        client.Driver.Message1.SelectRank(2); // Asteğmen
        client.Driver.Message1.ClickSend();

        client.Driver.WaitForTrafficRow("Transmit Mesaj 1");
        server.Driver.WaitForTrafficRow("Receive Mesaj 1");
        server.Driver.WaitForTrafficRow("Transmit Mesaj 2");
        client.Driver.WaitForTrafficRow("Receive Mesaj 2");

        Assert.Equal(["Transmit Mesaj 1", "Receive Mesaj 2"], client.Driver.TrafficRows);
        Assert.Equal(["Receive Mesaj 1", "Transmit Mesaj 2"], server.Driver.TrafficRows);

        server.Driver.SelectTrafficRow(0);
        MainWindowDriver.WaitUntil(() => server.Driver.DetailHeader.StartsWith("Receive Mesaj 1", StringComparison.Ordinal), "detail pane to show the received message");
        var fields = server.Driver.DetailFields;
        Assert.Contains("Birlik Referans Numarası = 1234", fields);
        Assert.Contains("Adı = Zeynep", fields);
        Assert.Contains("Unit No = 77", fields);
        Assert.Contains("Soyadı = Kaya", fields);
        Assert.Contains("Rütbe = 2 – Asteğmen", fields);
        Assert.Contains("64 byte", server.Driver.DetailHeader, StringComparison.Ordinal);
        Assert.StartsWith("0000  00 00 00 01 00 00 04 D2", server.Driver.DetailHex, StringComparison.Ordinal); // id = 1, ref = 1234

        // Evidence for the docs: both windows right after the exchange (see docs/images).
        server.SaveScreenshot("server-after-exchange");
        client.SaveScreenshot("client-after-exchange");
    }

    [Fact]
    public void ServerSendsMessage2_ClientShowsReceiveAndAutoRepliesWithMessage1()
    {
        var (server, client) = ConnectPair(serverAutoReply: false, clientAutoReply: true);

        server.Driver.Message2.Open();
        server.Driver.Message2.SetField("Altitude", "1500");
        server.Driver.Message2.SetPositionValid(false);
        server.Driver.Message2.ClickSend();

        client.Driver.WaitForTrafficRow("Receive Mesaj 2");
        server.Driver.WaitForTrafficRow("Receive Mesaj 1");

        client.Driver.SelectTrafficRow(0);
        MainWindowDriver.WaitUntil(() => client.Driver.DetailHeader.StartsWith("Receive Mesaj 2", StringComparison.Ordinal), "detail pane to show Mesaj 2");
        var fields = client.Driver.DetailFields;
        Assert.Contains("Yükseklik = 1500", fields);
        Assert.Contains("Birlik Konum Bilgisi Geçerliliği = 0 – False", fields);
    }

    [Fact]
    public void OutOfRangeValue_DisablesSendUntilCorrected()
    {
        var (_, client) = ConnectPair(serverAutoReply: false, clientAutoReply: false);

        client.Driver.Message1.Open();
        client.Driver.Message1.WaitForSendEnabled(true);
        client.Driver.Message1.SetField("UnitReferenceNumber", "10000");
        client.Driver.Message1.WaitForSendEnabled(false);

        client.Driver.Message1.SetField("UnitReferenceNumber", "9999");
        client.Driver.Message1.WaitForSendEnabled(true);

        client.Driver.Message1.SetField("FirstName", new string('ş', 13)); // 26 UTF-8 bytes
        client.Driver.Message1.WaitForSendEnabled(false);
    }

    [Fact]
    public void EmptyOrNonNumericValue_DisablesSendUntilCorrected()
    {
        // K15: an emptied box used to keep sending the previous value with Gönder still enabled.
        var (_, client) = ConnectPair(serverAutoReply: false, clientAutoReply: false);

        client.Driver.Message2.Open();
        client.Driver.Message2.WaitForSendEnabled(true);
        client.Driver.Message2.SetField("Altitude", "");
        client.Driver.Message2.WaitForSendEnabled(false);

        client.Driver.Message2.SetField("Altitude", "abc");
        client.Driver.Message2.WaitForSendEnabled(false);

        client.Driver.Message2.SetField("Altitude", "1500");
        client.Driver.Message2.WaitForSendEnabled(true);
    }

    [Fact]
    public void UndefinedBytes_ShowWarningBannerAndUndefinedRow()
    {
        var server = LaunchServer(autoReply: true);
        server.Driver.ClickStart();
        server.Driver.WaitForStatusContaining("Dinleniyor");
        Assert.False(server.Driver.IsWarningVisible);

        byte[] garbage = "HELLO WORLD"u8.ToArray();
        int bogusId = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(garbage); // "HELL" read as a big-endian int

        // Keep the raw socket open until the end so that the later "connection closed" warning
        // cannot race with the banner assertions below.
        using var raw = new TcpClient();
        raw.Connect("127.0.0.1", _port);
        raw.GetStream().Write(garbage);
        raw.GetStream().Flush();

        server.Driver.WaitForWarningContaining("Tanımsız mesaj");
        server.Driver.WaitForTrafficRow($"Receive Tanımsız ({bogusId})");
        Assert.True(server.Driver.IsWarningVisible);

        server.Driver.DismissWarning();
        MainWindowDriver.WaitUntil(() => !server.Driver.IsWarningVisible, "warning banner to disappear");
    }

    [Fact]
    public void PeriodicSend_ProducesRepeatedTransmitsUntilSwitchedOff()
    {
        var (server, client) = ConnectPair(serverAutoReply: false, clientAutoReply: false);

        client.Driver.Message1.Open();
        client.Driver.Message1.SetInterval(100);
        client.Driver.Message1.SetPeriodic(true);

        server.Driver.WaitForTrafficRowCount(count => count >= 5, ">= 5");
        client.Driver.Message1.SetPeriodic(false);

        int settled = server.Driver.TrafficRows.Count;
        Thread.Sleep(600);
        int later = server.Driver.TrafficRows.Count;

        Assert.All(server.Driver.TrafficRows, row => Assert.Equal("Receive Mesaj 1", row));
        Assert.InRange(later, settled, settled + 1); // at most one in-flight message after the switch
        Assert.False(client.Driver.Message1.IsPeriodicChecked);
    }

    [Fact]
    public void BothSidesAutoReply_PingPongKeepsTheUiResponsive_AndTheToggleStopsIt()
    {
        // K4: with auto-reply on at both ends a single message starts an endless Mesaj 1 ↔ Mesaj 2
        // loop at loopback speed. The loop is the specified behaviour; a frozen window is not.
        var (server, client) = ConnectPair(serverAutoReply: true, clientAutoReply: true);

        client.Driver.Message1.Open();
        client.Driver.Message1.ClickSend();

        server.Driver.WaitForTransmitCount(count => count >= 1000, ">= 1000");
        Thread.Sleep(2000); // let the flood run long enough to starve a naive UI

        Assert.True(server.IsResponding, "Server window stopped responding during the ping-pong.");
        Assert.True(client.IsResponding, "Client window stopped responding during the ping-pong.");

        // The user must be able to break the loop from the UI.
        client.Driver.SetAutoReply(false);
        server.Driver.SetAutoReply(false);
        Assert.False(client.Driver.IsAutoReplyChecked);
        Assert.False(server.Driver.IsAutoReplyChecked);

        int settled = 0;
        MainWindowDriver.WaitUntil(
            () =>
            {
                int before = server.Driver.TransmitCount + server.Driver.ReceiveCount;
                Thread.Sleep(500);
                settled = server.Driver.TransmitCount + server.Driver.ReceiveCount;
                return settled == before;
            },
            "the ping-pong to stop after switching auto-reply off");

        Assert.True(settled >= 2000);
        Assert.True(server.IsResponding);
        Assert.True(client.IsResponding);
    }

    [Fact]
    public void ServerStop_ClientSeesDisconnectAndBothReturnToIdle()
    {
        var (server, client) = ConnectPair(serverAutoReply: true, clientAutoReply: false);

        server.Driver.ClickStop();

        server.Driver.WaitForStatusContaining("Bağlı değil");
        client.Driver.WaitForStatusContaining("Bağlı değil");
        client.Driver.WaitForWarningContaining("kapatıldı");
        Assert.True(client.Driver.IsStartEnabled);
        Assert.True(server.Driver.IsStartEnabled);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private AppInstance LaunchServer(bool autoReply) => Track(AppInstance.Launch("server", _port, autoReply));

    private AppInstance LaunchClient(bool autoReply) => Track(AppInstance.Launch("client", _port, autoReply));

    private (AppInstance Server, AppInstance Client) ConnectPair(bool serverAutoReply, bool clientAutoReply)
    {
        var server = LaunchServer(serverAutoReply);
        var client = LaunchClient(clientAutoReply);

        server.Driver.ClickStart();
        server.Driver.WaitForStatusContaining("Dinleniyor");
        client.Driver.ClickStart();
        client.Driver.WaitForStatusContaining("Bağlı:");
        server.Driver.WaitForStatusContaining("Bağlı:");

        return (server, client);
    }

    private AppInstance Track(AppInstance instance)
    {
        _instances.Add(instance);
        return instance;
    }
}
