# Tasarım Kararları

Görev tanımının açık bıraktığı ya da birden fazla şekilde yorumlanabilecek noktalarda alınan kararlar ve gerekçeleri. Her karar kısa bir ADR (Architecture Decision Record) biçimindedir.

## K1 — Wire format görev tanımındaki tablolarla birebir

**Karar:** Mesajlara uzunluk, sürüm veya sağlama (checksum) başlığı eklenmez. Mesaj 1 = 64 byte, Mesaj 2 = 29 byte.
**Gerekçe:** Görev tanımı alanları tam olarak veriyor; başlık eklemek karşı tarafın da aynı başlığı bilmesini gerektirir ve "tabloda yazan mesaj" ile birebir izlenebilirliği bozar. Mesaj sınırı, ID'den türetilen sabit uzunlukla belirlenir.

## K2 — Big-endian byte sırası

**Karar:** Tüm çok baytlı sayılar network byte order (big-endian) ile yazılır.
**Gerekçe:** Görev tanımı belirtmiyor; TCP/IP protokollerinde yerleşik kural budur. Tek bir yerde (`BinaryMessageCodec`) tanımlıdır, gerekirse değiştirilebilir.

## K3 — 25 byte sabit UTF-8 string

**Karar:** "25 karakterlik String" alanları 25 byte sabit genişlikte, UTF-8 kodlu ve `0x00` ile doldurulmuş tutulur. UTF-8 karşılığı 25 byte'ı aşan girişler arayüzde reddedilir.
**Gerekçe:** Türkçe isimlerde (ş, ğ, ı) veri kaybı olmaması için ASCII yerine UTF-8 seçildi. Sabit genişlik korunduğu için mesaj boyutu değişmez.

## K4 — Otomatik cevap kullanıcı anahtarına bağlı (varsayılan açık)

**Karar:** "Mesaj 1 gelirse Mesaj 2, Mesaj 2 gelirse Mesaj 1 gönder" davranışı arayüzdeki bir anahtarla açılıp kapatılır; varsayılan açıktır.
**Gerekçe:** Gereksinim iki tarafta da aynen uygulanırsa tek bir mesaj sonsuz bir Mesaj 1 ↔ Mesaj 2 döngüsü başlatır (ping-pong). Tel üzerinde "bu bir cevaptır" bilgisi bulunmadığından döngü protokol seviyesinde kırılamaz. Bu, gereksinim analizinde tespit edilen bir boşluktur; demo sırasında anahtar yalnızca bir tarafta açık tutulur. İki tarafta da açılırsa döngü loopback hızında (saniyede binlerce mesaj) akar; arayüz cevap vermeye devam eder ve anahtar kapatılınca döngü durur (K14).

## K5 — Server tek aktif bağlantı kabul eder

**Karar:** Server modunda aynı anda tek bir client bağlı olabilir. Ek bağlantı denemeleri kabul edilip hemen kapatılır ve logda bilgi kaydı oluşur.
**Gerekçe:** Görev senaryosu bir server ve bir client öngörüyor. Çoklu istemci, otomatik cevabın kime gideceği gibi görev dışı soruları beraberinde getirir.

## K6 — Aralık dışı değer "tanımsız mesaj" değildir

**Karar:** Tanımlı bir ID ile gelen ancak bir veya daha fazla alanı aralık dışı olan mesaj `Receive` olarak loglanır; detay görünümünde geçersiz alanlar vurgulanır. "Tanımsız mesaj" uyarısı yalnızca bilinmeyen ID için verilir.
**Gerekçe:** Mesaj tanınmıştır; kullanıcıya hangi alanın neden geçersiz olduğunu göstermek, mesajı tamamen reddetmekten daha fazla bilgi sağlar.

## K7 — Uyarılar modal pencere değil

**Karar:** Tanımsız mesaj uyarısı kırmızı log satırı ve sayaçlı bir banner ile verilir; `MessageBox` kullanılmaz.
**Gerekçe:** Periyodik gönderim sırasında saniyede onlarca mesaj gelebilir; modal pencereler arayüzü kilitler.

## K8 — Trafik logu son 5000 kayıtla sınırlı

**Karar:** Log, en eski kaydı atarak son 5000 kaydı tutar; liste sanallaştırılmıştır.
**Gerekçe:** 100 ms periyotla saatlerce çalışan bir oturumda bellek ve arayüz performansı korunur.

## K9 — Kod İngilizce, arayüz Türkçe

**Karar:** Tip ve üye adları İngilizce; arayüz etiketleri ve dokümanlar Türkçe. `Transmit` / `Receive` ön ekleri görev tanımındaki gibi İngilizce bırakılmıştır. Mesaj tipleri görev tanımıyla birebir izlenebilirlik için `Message1` ve `Message2` olarak adlandırılmıştır.
**Gerekçe:** Kod tabanı sektör pratiğine uygun; kullanıcı ve değerlendirici kitlesi Türkçe.

## K10 — Katmanlı yapı ve `Abstractions/` kuralı

**Karar:** `Protocol` ve `Transport` bağımsız; `Core` ikisini orkestre eder; `App` yalnızca `Core`'a bağlıdır. Her projede arayüzler `Abstractions/` klasöründe toplanır; üst katmanlar ve test sahteleri yalnızca arayüzlere bağımlıdır.
**Gerekçe:** Soketsiz unit test (Protocol, Core), gerçek loopback ile dar kapsamlı integration test (Transport) ve gerçek arayüzle e2e test (App) birbirinden bağımsız yazılabilir.

## K11 — Arayüz doğrulaması protokol doğrulayıcısını yeniden kullanır

**Karar:** Mesaj editörleri alan kurallarını kendileri tanımlamaz; her değişiklikte mesajı kurup `IMessageValidator`'a verir ve dönen alan hatalarını `INotifyDataErrorInfo` ile gösterir.
**Gerekçe:** Aralık kuralları tek yerde yaşar; arayüz ile tel formatı birbirinden farklı bir şey kabul edemez. Görev tablosundaki bir aralık değişirse yalnızca `FieldConstraints` güncellenir.

## K12 — Otomatik cevap ve periyodik gönderim formdaki güncel değerleri kullanır

**Karar:** Uygulamanın kendi başlattığı gönderimler (`AutoReply`, `PeriodicSender`) içeriği `IOutgoingMessageProvider` üzerinden alır; arayüz katmanı bunu o anki form değerleriyle uygular. Form geçersizse gönderim yapılmaz ve kullanıcı uyarılır.
**Gerekçe:** Kullanıcı periyodik gönderim sürerken bir alanı değiştirdiğinde yeni değer hemen tele yansır; Core katmanı arayüzden habersiz kalır.

## K13 — Fire-and-forget sınırlarında tüm istisnalar yakalanır

**Karar:** Zamanlayıcı tikleri ve otomatik cevaplar gibi beklenmeyen (`await` edilmeyen) görevlerde her istisna yakalanır ve kullanıcıya uyarı olarak iletilir; periyodik gönderimde ilgili zamanlama durdurulur.
**Gerekçe:** Gözlemlenmeyen bir görev istisnası sessizce kaybolur ya da süreci düşürür; canlı demoda ikisi de kabul edilemez. Senkron çağrı yollarında (ör. `SendAsync`) istisnalar normal şekilde yayılır ve komut katmanında gösterilir.

## K14 — Ağ olayları arayüze toplu ve düşük öncelikle aktarılır

**Karar:** Log kayıtları ve uyarılar arayüz thread'ine tek tek değil, toplu (`UiBatcher`) aktarılır. Aynı anda en fazla bir aktarım kuyrukta bekler ve bu aktarım girdi (fare/klavye) ile çizimden düşük öncelikte (`DispatcherPriority.Background`) çalışır. Kuyruktaki kayıt sayısı log kapasitesini aşarsa yalnızca en yeni 5000 kayıt listeye eklenir; sayaçlar yine tüm trafiği sayar. Otomatik kaydırma da toplu aktarım başına bir kez yapılır.
**Gerekçe:** İlk sürüm her kaydı ayrı bir `Normal` öncelikli iş olarak gönderiyordu; `Normal` öncelik girdi ve çizimden yüksektir. İki tarafta otomatik cevap açıkken (K4) gelen saniyede binlerce mesaj kuyruğu arayüzün işleyebileceğinden hızlı doldurdu, pencere girdi alamadı ve dondu; kullanıcı döngüyü kıracak anahtara bile basamıyordu. Toplu aktarım arayüzün yükünü mesaj hızından bağımsız kılar; aynı koruma 10 ms periyotlu gönderim ve tanımsız byte seli için de geçerlidir.
