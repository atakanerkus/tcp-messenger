# Protokol Tanımı

Bu doküman, görev tanımındaki iki mesajın tel üzerindeki (wire) yerleşimini ve görev tanımının açık bırakmış olduğu noktalarda alınan kararları içerir. `TcpMessenger.Protocol` projesi bu dokümanın kod karşılığıdır; buradaki tablolar unit testlerde test vektörü olarak kullanılır.

## Genel kurallar

| Konu | Karar |
|---|---|
| Çerçeve başlığı | Yok. Mesaj, görev tanımındaki tabloda yazan alanlardan ibarettir; ekstra uzunluk veya sürüm alanı eklenmez. |
| Byte sırası | Big-endian (network byte order). Tüm çok baytlı sayısal alanlar için geçerlidir. |
| String alanlar | 25 byte sabit genişlik, UTF-8. Kısa değerler sağdan `0x00` ile doldurulur; çözümlemede son `0x00` baytları atılır. UTF-8 karşılığı 25 byte'ı aşan değerler arayüzde reddedilir. |
| İşaret | `int`, `short`, `long` işaretli; `uint32` işaretsiz; `byte` işaretsiz. |
| Mesaj sınırı | TCP bir akış protokolüdür. Alıcı önce 4 baytlık Mesaj ID'yi okur, tanımlı bir ID ise o mesajın sabit uzunluğu kadar bayt tamamlanana dek bekler. |
| Tanımsız mesaj | Okunan ID tanımlı değilse kullanıcı uyarılır ve alım tamponundaki tüm veri atılır (yeniden senkronizasyon). |

## Mesaj 1 — toplam 64 byte

| Offset | Boyut | Alan (görev tanımı) | Kod adı | Tip | Geçerli değerler |
|---:|---:|---|---|---|---|
| 0 | 4 | Mesaj ID | `MessageId` | int32 | `1` |
| 4 | 4 | Birlik Referans Numarası | `UnitReferenceNumber` | int32 | `-1000` … `9999` |
| 8 | 25 | Adı | `FirstName` | UTF-8, sabit 25 byte | en fazla 25 byte |
| 33 | 4 | Unit No | `UnitNo` | uint32 | tam aralık (görev tanımında kısıt yok) |
| 37 | 25 | Soyadı | `LastName` | UTF-8, sabit 25 byte | en fazla 25 byte |
| 62 | 2 | Rütbe | `Rank` | int16 | `0` Üsteğmen, `1` Teğmen, `2` Asteğmen |

## Mesaj 2 — toplam 29 byte

| Offset | Boyut | Alan (görev tanımı) | Kod adı | Tip | Geçerli değerler |
|---:|---:|---|---|---|---|
| 0 | 4 | Mesaj ID | `MessageId` | int32 | `2` |
| 4 | 4 | Birlik Referans Numarası | `UnitReferenceNumber` | int32 | `1` … `9999` |
| 8 | 1 | Birlik Konum Bilgisi Geçerliliği | `IsPositionValid` | byte | `1` True, `0` False |
| 9 | 8 | Enlem | `Latitude` | int64 | `-32 400 000` … `32 400 000` |
| 17 | 8 | Boylam | `Longitude` | int64 | `-64 800 000` … `64 800 000` |
| 25 | 4 | Yükseklik | `Altitude` | int32 | `0` … `10 000` |

## Doğrulama davranışı

- **Gönderim öncesi:** arayüz, tablodaki aralıkların dışındaki değerleri kabul etmez; ilgili alan hata ile işaretlenir ve Gönder devre dışı kalır.
- **Alım sonrası:** ID tanımlı ancak bir alan aralık dışıysa mesaj yine `Receive` olarak loglanır; detay görünümünde geçersiz alanlar vurgulanır. Bu durum "tanımsız mesaj" sayılmaz.

## Örnek bayt dizilimi

Mesaj 2, `UnitReferenceNumber = 1`, `IsPositionValid = true`, `Latitude = 0`, `Longitude = 0`, `Altitude = 10000`:

```
00 00 00 02   00 00 00 01   01   00 00 00 00 00 00 00 00   00 00 00 00 00 00 00 00   00 00 27 10
└─ ID = 2 ─┘ └─ Ref = 1 ─┘ └─┘  └──────── Enlem ────────┘ └─────── Boylam ────────┘ └ Yük=10000 ┘
```
