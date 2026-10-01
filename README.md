# FastReport Barkod & Etiket Tasarım ve Baskı Sistemi (.NET 8 WinForms)

Bu proje; termal barkod ve etiket yazıcıları (Zebra, TSC, Argox, Honeywell vb.) için **son kullanıcının sürükle-bırak ile şablon tasarlayabilmesini** ve bu şablonların dinamik verilerle **anlık olarak yazıcıya basılabilmesini** sağlayan örnek bir masaüstü uygulamasıdır.

---

## 🎯 Çözülen Kronik Sorun
Geleneksel sistemlerde etiket tasarımları yazıcı dilleriyle (ZPL, TSPL, EPL) kod tarafında elle yazılır veya her yeni etiket talebinde (boyut, logo, barkod tipi değişikliği) yazılımcı müdahalesi gerekirdi.

**Bu Proje ile:**
1. **Yazılımcı Bağımsızlığı:** Kullanıcı, uygulamanın içindeki görsel tasarımcı ile istediği etiket boyutunu, barkod türünü (Code128, QR Code, DataMatrix vb.) ve yerleşimini sürükle-bırak yöntemiyle kendisi tasarlar.
2. **Harici Kurulum Yok:** Kullanıcının bilgisayarına FastReport kurmaya gerek yoktur; tasarım motoru (`Embedded Designer`) uygulamanın içine gömülüdür.
3. **Cihaz Bağımsız Baskı:** USB veya Ağ üzerinden bağlı tüm termal yazıcılara standart Windows Spooler üzerinden gecikmesiz ve kesintisiz çıktı gönderilir.

---

## 🚀 Mimari ve Çalışma Mantığı

```
[Kullanıcı Arayüzü]
       │
       ├──► "Etiket Tasarla" ──────► [FastReport Embedded Designer] ──► (etiket_sablonu.frx kaydedilir)
       │
       └──► "Tekli / Toplu Bas" ───► [FastReport Engine]
                                            │
                                            ▼ (Dinamik SQL / Form Verisi Birleşir)
                                     [Termal Barkod Yazıcı (Zebra, TSC vb.)]
```

---

## ⚙️ Özellikler

- **🎨 Dahili Tasarımcı (`report.Design()`):** Uygulama içinden açılan tam yetenekli görsel şablon editörü.
- **🖨️ Tekli Anlık Baskı:** Form üzerinden girilen anlık barkod, ürün adı ve fiyat bilgisini şablona giydirerek yazdırma.
- **📦 Toplu Baskı:** Liste halindeki (örneğin 10 veya 1000 farklı) ürünü `DataBand` veri kaynağına bağlayıp her ürün için benzersiz barkod etiketi üretme.
- **⚡ Sessiz Yazdırma (Silent Print):** Kullanıcıya diyalog penceresi göstermeden doğrudan hedef yazıcıya basabilme altyapısı:
  ```csharp
  report.PrintSettings.Printer = "Zebra ZD420";
  report.PrintSettings.ShowDialog = false;
  report.Print();
  ```

---

## 🛠️ Kurulum ve Çalıştırma

### Gereksinimler
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows İşletim Sistemi

### Çalıştırma
```powershell
git clone <repo-url>
cd FastReportBarcodeApp
dotnet run
```

---

## 📂 Dosya Yapısı

- `Form1.cs`: Tasarımcıyı açma, tekli/toplu yazdırma ve şablon oluşturma mantığı.
- `Form1.Designer.cs`: Masaüstü arayüz bileşenleri.
- `etiket_sablonu.frx`: FastReport XML formatındaki standart 100x50 mm etiket şablonu.
