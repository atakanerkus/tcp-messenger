# Demo Senaryosu

> Taslak. Uygulama tamamlandığında (Faz 6) adımlar ekran görüntüleri ve anlatım notlarıyla netleştirilecektir.

## Hazırlık

1. Laptopta `dotnet build TcpMessenger.slnx -c Release` çalıştırılır; tüm test kategorileri bir kez koşturulur.
2. Uygulama iki kez başlatılır:
   - Instance A: `TcpMessenger.App.exe --mode server --port 5000`
   - Instance B: `TcpMessenger.App.exe --mode client --host 127.0.0.1 --port 5000 --autoreply off`
3. Pencereler yan yana yerleştirilir.

## Akış

| Adım | Gereksinim maddesi | Gösterilecek |
|---|---|---|
| 1 | Server / client modu | A'da Başlat, B'de Bağlan; iki tarafta da durum "Bağlı" |
| 2 | Alan girişi ve gönderim | B'de Mesaj 1 alanları doldurulur, Gönder |
| 3 | Trafik listesi | B'de `Transmit Mesaj 1`, A'da `Receive Mesaj 1` satırı, zaman damgalı |
| 4 | Otomatik cevap | A otomatik olarak Mesaj 2 gönderir; B'de `Receive Mesaj 2` |
| 5 | İçerik görüntüleme | A'da gelen satır seçilir, alanlar ve ham hex görünür |
| 6 | Tanımsız mesaj | Üçüncü bir araçtan rastgele bayt gönderilir; A'da uyarı banner'ı ve kırmızı satır |
| 7 | Periyodik gönderim | B'de Mesaj 1 için 100 ms, Mesaj 2 için 500 ms açılır; iki tarafta akan log |
| 8 | Doğrulama | B'de aralık dışı değer girilir; alan kırmızı, Gönder pasif |

## Anlatım notları

- Gereksinimdeki otomatik cevap döngüsü (K4) ve neden anahtarla çözüldüğü.
- Test piramidi: Protocol unit testleri (sınır değerleri), Transport loopback integration testleri, FlaUI e2e testleri.
- Katman sınırları ve `Abstractions/` kuralı; Core'un soketsiz test edilmesi.
