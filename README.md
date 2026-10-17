# TcpMessenger

İki sabit yerleşimli binary mesajı TCP/IP üzerinden gönderip alan, server veya client olarak çalışabilen WPF masaüstü uygulaması. Yazılım test mühendisliği mülakat görevinin 4. sorusuna cevap olarak geliştirilmiştir.

> Durum: Faz 0 — repo iskeleti. Özellikler fazlar hâlinde eklenmektedir; ilerleme için commit geçmişine bakınız.

## Özellikler (hedef)

- Server veya client modunda çalışma
- Mesaj 1 ve Mesaj 2'nin tüm alanlarının arayüzden girilip gönderilmesi
- Gelen/giden tüm trafiğin zaman, yön (`Transmit` / `Receive`) ve mesaj adı ile loglanması, seçilen kaydın içeriğinin gösterilmesi
- Otomatik cevap: Mesaj 1 alınınca Mesaj 2, Mesaj 2 alınınca Mesaj 1 gönderilir (kullanıcı anahtarı ile)
- Tanımsız mesaj alındığında kullanıcı uyarısı
- Mesaj tipi başına ayarlanabilir aralıkla periyodik gönderim

## Mimari

```
TcpMessenger.Protocol   mesaj modelleri, binary codec, framing, doğrulama      (bağımlılık yok)
TcpMessenger.Transport  TCP server/client, ham byte akışı                      (bağımlılık yok)
TcpMessenger.Core       oturum, otomatik cevap, periyodik gönderim, trafik logu (Protocol + Transport)
TcpMessenger.App        WPF, MVVM                                              (Core)
TcpMessenger.Tests      unit/ · integration/ · e2e/                            (hepsi)
```

Her projede arayüzler `Abstractions/` klasöründe, implementasyonlar işlev klasörlerinde yer alır. Üst katmanlar ve testlerdeki sahte nesneler yalnızca arayüzlere bağımlıdır.

## Klasör yapısı

```
├── .github/workflows/ci.yml    GitHub Actions: build + unit/integration testleri
├── docs/                       protokol, tasarım kararları, demo senaryosu
├── src/                        uygulama ve kütüphane projeleri
├── tests/TcpMessenger.Tests/   unit/, integration/, e2e/, TestUtilities/
├── Directory.Build.props       ortak derleme ayarları (warnings as errors, analyzers)
├── Directory.Packages.props    merkezi NuGet sürüm yönetimi
└── TcpMessenger.slnx
```

## Gereksinimler

- .NET SDK 10.0 (geliştirme ve testler)
- Windows 10/11 (WPF uygulaması ve e2e testleri)

## Derleme ve test

```bash
dotnet build TcpMessenger.slnx
dotnet test  --filter Category=Unit
dotnet test  --filter Category=Integration
dotnet test  --filter Category=E2E          # yalnızca lokal, etkileşimli masaüstü gerekir
```

## Dokümantasyon

- [docs/protocol.md](docs/protocol.md) — mesaj yerleşimleri, byte sırası, framing kuralları
- [docs/decisions.md](docs/decisions.md) — tasarım kararları ve gerekçeleri
- [docs/demo-script.md](docs/demo-script.md) — sunum akışı
