# Çalıştırma Detayları

Terminalden derleme, test ve çalıştırma komutları. Komutlar Windows PowerShell içindir ve **depo kök klasöründen** çalıştırılır (`TcpMessenger.slnx` dosyasının olduğu yer). Visual Studio profilleri için [README.md](README.md#derleme-ve-çalıştırma) dosyasına bakın.

## 1. Ön koşullar

```powershell
dotnet --version        # 10.0.x olmalı
git clone https://github.com/atakanerkus/tcp-messenger.git
cd tcp-messenger
```

## 2. Derleme

```powershell
dotnet restore TcpMessenger.slnx
dotnet build TcpMessenger.slnx                    # Debug
dotnet build TcpMessenger.slnx -c Release         # Release (demo ve CI ile aynı)
```

Uyarılar hata olarak ele alınır (`TreatWarningsAsErrors`); derleme 0 uyarıyla bitmelidir.

## 3. Testler

Seviyeler `[Trait("Category", ...)]` etiketiyle ayrılır.

| Komut | Ne çalışır |
|---|---|
| `dotnet test TcpMessenger.slnx` | Hepsi (E2E dahil, masaüstü gerekir) |
| `dotnet test TcpMessenger.slnx --filter Category=Unit` | 233 unit test |
| `dotnet test TcpMessenger.slnx --filter Category=Integration` | 25 integration test (gerçek localhost TCP) |
| `dotnet test TcpMessenger.slnx --filter Category=E2E` | 9 E2E test (FlaUI, iki gerçek pencere açılır) |
| `dotnet test TcpMessenger.slnx --filter "Category!=E2E"` | CI'daki ile aynı: unit + integration |

Belirli bir test sınıfı ya da test:

```powershell
dotnet test TcpMessenger.slnx --filter "FullyQualifiedName~UiBatcherTests"
dotnet test TcpMessenger.slnx --filter "FullyQualifiedName~BothSidesAutoReply_PingPongKeepsTheUiResponsive"
```

Testleri listelemek ve ayrıntılı çıktı almak:

```powershell
dotnet test TcpMessenger.slnx --list-tests
dotnet test TcpMessenger.slnx --filter Category=Unit --logger "console;verbosity=detailed"
```

Kod kapsamı (coverlet) ve TRX raporu:

```powershell
dotnet test TcpMessenger.slnx --filter "Category!=E2E" --collect:"XPlat Code Coverage"
# rapor: tests\TcpMessenger.Tests\TestResults\<guid>\coverage.cobertura.xml

dotnet test TcpMessenger.slnx --filter "Category!=E2E" --logger "trx;LogFileName=test-results.trx"
# rapor: tests\TcpMessenger.Tests\TestResults\test-results.trx
```

**E2E notları:** Testler çalışırken pencereler açılıp kapanır; bu sırada fareye ve klavyeye dokunmayın, ekranı kilitlemeyin. E2E testleri CI'da çalışmaz, yalnızca lokalde.

## 4. Uygulamayı çalıştırma

### Komut satırı argümanları

| Argüman | Değerler | Varsayılan |
|---|---|---|
| `--mode` | `server` / `client` | `server` |
| `--host` | IP ya da makine adı (client için) | `127.0.0.1` |
| `--port` | 1–65535 | `5000` |
| `--autoreply` | `on` / `off` | `on` |

Argümanlar yalnızca formu doldurur; bağlantıyı başlatmak için pencerede **Başlat** (server) ya da **Bağlan** (client) düğmesine basılır.

### `dotnet run` ile, iki terminalde

Önce bir kez derleyin, sonra `--no-build` ile çalıştırın (iki `dotnet run` aynı anda derlemeye çalışırsa dosya kilidi hatası alınır):

```powershell
dotnet build TcpMessenger.slnx -c Release

# Terminal 1: server
dotnet run --project src/TcpMessenger.App -c Release --no-build -- --mode server --port 5000 --autoreply on

# Terminal 2: client
dotnet run --project src/TcpMessenger.App -c Release --no-build -- --mode client --host 127.0.0.1 --port 5000 --autoreply off
```

### Derlenmiş exe ile, iki pencere birden

```powershell
dotnet build TcpMessenger.slnx -c Release
$exe = "src\TcpMessenger.App\bin\Release\net10.0-windows\TcpMessenger.App.exe"
Start-Process $exe -ArgumentList "--mode server --port 5000 --autoreply on"
Start-Process $exe -ArgumentList "--mode client --host 127.0.0.1 --port 5000 --autoreply off"
```

Demoda otomatik cevap yalnızca server'da açıktır. İki tarafta da açılırsa Mesaj 1 ↔ Mesaj 2 sonsuz döngüye girer (karar K4); pencere donmaz (K14) ve anahtar kapatılınca döngü durur.

## 5. .NET kurulu olmayan makine için tek dosya

```powershell
dotnet publish src/TcpMessenger.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/win-x64

Start-Process "publish\win-x64\TcpMessenger.App.exe" -ArgumentList "--mode server --port 5000 --autoreply on"
Start-Process "publish\win-x64\TcpMessenger.App.exe" -ArgumentList "--mode client --host 127.0.0.1 --port 5000 --autoreply off"
```

`publish/` klasörü git'e eklenmez.

## 6. Demo yardımcıları

### Tanımsız mesaj göndermek

Server tek bağlantı kabul ettiği için önce client penceresinde **Durdur**'a basın, sonra:

```powershell
$c = New-Object Net.Sockets.TcpClient('127.0.0.1', 5000)
$s = $c.GetStream()
$b = [byte[]](0x00, 0x00, 0x00, 0x09, 0xAA, 0xBB)   # ID = 9, tanımlı değil
$s.Write($b, 0, $b.Length)
Start-Sleep -Milliseconds 200
$c.Close()
```

Server'da sarı uyarı bandı ve listede kırmızı `Receive Tanımsız (9)` satırı görünür.

### Port kullanımda mı?

```powershell
Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue |
    Select-Object LocalAddress, LocalPort, State, OwningProcess
```

### Açık kalan pencereleri kapatmak

```powershell
Get-Process TcpMessenger.App -ErrorAction SilentlyContinue | Stop-Process
```

## 7. Sorun giderme

| Belirti | Neden | Çözüm |
|---|---|---|
| Derlemede `MSB3027` / "dosya başka bir işlem tarafından kullanılıyor" | Uygulama penceresi açık, `bin\` altındaki DLL'ler kilitli | Pencereleri kapatın (yukarıdaki `Stop-Process`) ya da diğer yapılandırmayla derleyin (`-c Release`) |
| Server başlatılınca "port kullanımda" hatası | Aynı portta başka bir server açık | Diğer instance'ı kapatın ya da `--port 5001` kullanın |
| Client bağlanamıyor | Server'da **Başlat**'a basılmadı ya da port farklı | Server durum satırında "Dinleniyor: port 5000" yazdığını kontrol edin |
| E2E testleri zaman aşımına uğruyor | Ekran kilitli, pencere öne gelemedi ya da test sırasında fare kullanıldı | Ekranı açık bırakıp testleri dokunmadan yeniden çalıştırın |
| İkinci client bağlanınca hemen kopuyor | Server tek bağlantı kabul eder (K5) | Önce diğer client'ı durdurun |
