# Demo Senaryosu

Sunumda uygulama aynı laptopta iki kez çalıştırılır: biri **Server**, biri **Client**. Aşağıdaki akış görev tanımındaki her maddeyi sırayla gösterir. Tahmini süre: 8–10 dakika.

## Hazırlık (sunumdan önce)

1. `dotnet build TcpMessenger.slnx -c Release` ve üç test kategorisi bir kez koşturulur:
   ```bash
   dotnet test --filter Category=Unit
   dotnet test --filter Category=Integration
   dotnet test --filter Category=E2E
   ```
2. Yedek olarak runtime bağımsız exe üretilir (ağ veya SDK sorununa karşı):
   ```bash
   dotnet publish src/TcpMessenger.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/win-x64
   ```
3. İki instance komut satırından, form önceden doldurulmuş hâlde açılır:
   ```bash
   src/TcpMessenger.App/bin/Release/net10.0-windows/TcpMessenger.App.exe --mode server --port 5000 --autoreply on
   src/TcpMessenger.App/bin/Release/net10.0-windows/TcpMessenger.App.exe --mode client --host 127.0.0.1 --port 5000 --autoreply off
   ```
   Pencere başlıkları "TCP Messenger — Server" ve "TCP Messenger — Client" olarak ayrışır; yan yana yerleştirilir.

## Akış

| # | Gereksinim | Adım | Beklenen |
|---|---|---|---|
| 1 | Server veya client olarak çalışma | Server'da **Başlat**; Client'ta **Bağlan** | Server durum satırı "Dinleniyor: port 5000" → "Bağlı: 127.0.0.1:…"; Client "Bağlı: 127.0.0.1:5000" |
| 2 | Alanların girilip gönderilmesi | Client'ta Mesaj 1 sekmesi: Ad "Zeynep", Soyad "Kaya", Rütbe "Asteğmen", **Gönder** | Client listesinde `Transmit Mesaj 1` |
| 3 | Zaman + yön + mesaj adı ile liste | Server listesine bakılır | `Receive Mesaj 1`, zaman damgası ms hassasiyetinde |
| 4 | Otomatik cevap | Server otomatik olarak Mesaj 2 gönderir | Server: `Transmit Mesaj 2`; Client: `Receive Mesaj 2` |
| 5 | Seçilen mesajın içeriği | Server'da `Receive Mesaj 1` satırı seçilir | Alt panelde alan tablosu (Adı = Zeynep …) ve ham hex dökümü (`00 00 00 01 …`, 64 byte) |
| 6 | Tanımsız mesaj uyarısı | Üçüncü bir araçtan ham bayt gönderilir, ör. PowerShell: `$c=New-Object Net.Sockets.TcpClient('127.0.0.1',5000); $s=$c.GetStream(); $b=[Text.Encoding]::ASCII.GetBytes('HELLO'); $s.Write($b,0,$b.Length)` (önce Client durdurulur, server tek bağlantı kabul eder) | Server'da sarı uyarı bandı "Tanımsız mesaj alındı: ID …", listede kırmızı `Receive Tanımsız (…)` satırı |
| 7 | Periyodik gönderim | Client'ta Mesaj 1 için "Her 100 ms", Mesaj 2 için "Her 500 ms" işaretlenir | İki tarafta akan log; durum çubuğunda sayaçlar artar; işaret kaldırılınca durur |
| 8 | Doğrulama | Client'ta Birlik Referans No'ya `10000` yazılır, sonra alan boşaltılır | Alan kırmızı çerçeveli, ipucu aralığı gösterir, **Gönder** pasif; boş alanda da pasif kalır (K15); `9999` yazılınca aktif |
| 9 | Bağlantı kopması | Server'da **Durdur** | Client'ta "Bağlantı karşı tarafça kapatıldı." uyarısı, durum "Bağlı değil"; Server tekrar başlatılabilir |

## Anlatım notları

- **Gereksinimdeki boşluk (K4):** otomatik cevap iki tarafta da açıkken tek bir mesaj sonsuz Mesaj 1 ↔ Mesaj 2 döngüsü başlatır. Tel üzerinde "bu bir cevaptır" bilgisi yok; bu yüzden anahtar var ve demoda sadece server'da açık. İstenirse canlı gösterilebilir: Client'ta anahtar açılır, bir mesaj gönderilir, log saniyede binlerce satırla akar, pencereler yine de cevap verir (K14), anahtar kapatılınca durur. (Integration testi `BothSidesAutoReply_ProducesPingPong_WhichIsWhyTheToggleExists` döngüyü, e2e testi `BothSidesAutoReply_PingPongKeepsTheUiResponsive_AndTheToggleStopsIt` arayüzün donmadığını belgeliyor.)
- **Katmanlar:** Protocol (saf, soketsiz) · Transport (yalnızca byte) · Core (orkestrasyon, yalnızca arayüzlere bağımlı) · App (ince MVVM). Her projede arayüzler `Abstractions/` altında.
- **Test piramidi:** 233 unit (sınır değerleri, byte vektörleri, sahte zamanla periyodik gönderim, boş/sayı olmayan form alanları), 25 integration (gerçek loopback TCP, iki tam yığın), 9 e2e (FlaUI ile iki gerçek pencere). CI her push'ta unit + integration koşturur.
- **Tek doğruluk kaynağı:** Arayüz doğrulaması Protocol'deki `MessageValidator`'ı kullanır; aralık kuralı iki yerde yazılmaz. Alınan mesajlarda aralık dışı değer "tanımsız" sayılmaz, alan bazında işaretlenir (K6).
- **Framing:** Başlık yok; ID'den sabit uzunluk (64 / 29). Tanımsız ID'de tampon temizlenir ve bir sonraki mesajla toparlanır (FrameParser testleri).
- **Dayanıklılık:** Uyarılar modal değil (saniyede onlarca mesaj gelebilir), log 5000 kayıtla sınırlı, periyodik gönderimde çakışan tikler atlanır, fire-and-forget sınırlarında tüm istisnalar yakalanıp uyarıya çevrilir.
