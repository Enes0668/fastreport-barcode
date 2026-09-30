using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using FastReport;

namespace FastReportBarcodeApp;

public partial class Form1 : Form
{
    private readonly string sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");

    public Form1()
    {
        InitializeComponent();
        VarsayilanSablonOlustur();
    }

    /// <summary>
    /// Eğer henüz hiç .frx dosyası yoksa, kullanıcı hemen test edebilsin diye
    /// 100mm x 50mm boyutlarında hazır bir başlangıç şablonu üretir.
    /// </summary>
    private void VarsayilanSablonOlustur()
    {
        if (File.Exists(sablonYolu)) return;

        using (Report report = new Report())
        {
            ReportPage page = new ReportPage();
            page.Name = "EtiketSayfasi";
            page.PaperWidth = 100;
            page.PaperHeight = 50;
            // Kenar boşluklarını sıfırlıyoruz — termal etikette boşluk istemiyoruz
            page.LeftMargin = 0;
            page.RightMargin = 0;
            page.TopMargin = 0;
            page.BottomMargin = 0;

            float mm = FastReport.Utils.Units.Millimeters;

            // DataBand — sayfanın tamamını kaplıyor (50mm)
            DataBand dataBand = new DataBand();
            dataBand.Name = "Data1";
            dataBand.Height = mm * 50;
            page.Bands.Add(dataBand);

            // ── Ürün Adı (üstte) ──────────────────────────────────────────
            FastReport.TextObject txtUrun = new FastReport.TextObject();
            txtUrun.Name = "txtUrunAdi";
            txtUrun.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 2, mm * 94, mm * 9);
            txtUrun.Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold);
            txtUrun.HorzAlign = FastReport.HorzAlign.Center;
            txtUrun.Text = "[UrunAdi]";
            dataBand.Objects.Add(txtUrun);

            // ── Barkod (Code128) ──────────────────────────────────────────
            // Kesilmemesi için: x=3mm, genişlik=94mm (100-3-3), yükseklik=26mm (çizgiler + rakamlar için yeterli)
            FastReport.Barcode.BarcodeObject barcode = new FastReport.Barcode.BarcodeObject();
            barcode.Name = "Barcode1";
            barcode.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 13, mm * 94, mm * 26);
            barcode.Barcode = new FastReport.Barcode.Barcode128();
            barcode.AutoSize = false;
            barcode.ShowText = true;
            barcode.Expression = "[BarkodNo]";
            dataBand.Objects.Add(barcode);

            // ── Fiyat (altta) ─────────────────────────────────────────────
            FastReport.TextObject txtFiyatObj = new FastReport.TextObject();
            txtFiyatObj.Name = "txtFiyat";
            txtFiyatObj.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 41, mm * 94, mm * 7);
            txtFiyatObj.Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold);
            txtFiyatObj.HorzAlign = FastReport.HorzAlign.Center;
            txtFiyatObj.Text = "Fiyat: [Fiyat] TL";
            dataBand.Objects.Add(txtFiyatObj);

            // ── Parametreler ───────────────────────────────────────────────
            report.Parameters.Add(new FastReport.Data.Parameter("BarkodNo") { DataType = typeof(string), Value = "8690123456789" });
            report.Parameters.Add(new FastReport.Data.Parameter("UrunAdi")  { DataType = typeof(string), Value = "Örnek Ürün" });
            report.Parameters.Add(new FastReport.Data.Parameter("Fiyat")    { DataType = typeof(string), Value = "100.00" });

            report.Pages.Add(page);
            report.Save(sablonYolu);
        }
    }

    /// <summary>
    /// SÜRÜKLE-BIRAK TASARIMCIYI AÇAR (report.Design())
    /// Kullanıcı tasarımı değiştirip diskteki .frx şablonunu günceller.
    /// </summary>
    private void btnTasarla_Click(object sender, EventArgs e)
    {
        try
        {
            using (Report report = new Report())
            {
                if (File.Exists(sablonYolu))
                    report.Load(sablonYolu);

                // Tasarım esnasında önizlemede görünebilsin diye temsili veriler
                report.SetParameterValue("BarkodNo", txtBarkod.Text);
                report.SetParameterValue("UrunAdi", txtUrunAdi.Text);
                report.SetParameterValue("Fiyat", txtFiyat.Text);

                // Tasarım ekranını aç
                report.Design();

                lblDurum.Text = "Durum: Tasarım güncellendi ve kaydedildi.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Tasarımcı açılırken hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// TEKLİ ETİKET BASMA: Ekrana girilen değerleri şablona parametre olarak basar.
    /// </summary>
    private void btnYazdirTekli_Click(object sender, EventArgs e)
    {
        try
        {
            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                // Kullanıcının formda girdiği değerleri şablondaki parametrelere aktar
                report.SetParameterValue("BarkodNo", txtBarkod.Text);
                report.SetParameterValue("UrunAdi", txtUrunAdi.Text);
                report.SetParameterValue("Fiyat", txtFiyat.Text);

                // Önizleme penceresini açar (Buradan direk yazdır simgesiyle yazıcıya da basılabilir)
                report.Show();

                /*
                // EĞER DİREKT BARKOD YAZICIYA HİÇ PENCERE AÇMADAN (SİLENT PRİNT) BASMAK İSTERSEN:
                report.PrintSettings.Printer = "Zebra ZD420"; // veya barkod yazıcının adı
                report.PrintSettings.ShowDialog = false;      // Yazdırma diyaloğunu gizle
                report.Print();
                */

                lblDurum.Text = "Durum: Tekli etiket ekranda önizlendi / yazdırıldı.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// TOPLU ETİKET BASMA: 10 farklı ürün listesini FastReport'a RegisterData ile aktarır.
    /// FastReport her ürün için ayrı bir barkod ve etiket sayfası üretir!
    /// </summary>
    private void btnYazdirToplu_Click(object sender, EventArgs e)
    {
        try
        {
            // 1. Örnek 10 adet farklı ürün oluşturuyoruz (Sanki SQL'den gelmiş gibi)
            var urunListesi = new List<UrunModel>();
            for (int i = 1; i <= 10; i++)
            {
                urunListesi.Add(new UrunModel
                {
                    BarkodNo = $"869000000{i:D4}",
                    UrunAdi = $"Endüstriyel Ürün #{i}",
                    Fiyat = (i * 25.50m).ToString("0.00")
                });
            }

            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                // Listeyi rapora "Urunler" veri kaynağı olarak tanıt
                report.RegisterData(urunListesi, "Urunler");

                // İlk satır değerini de parametreye aktar
                report.SetParameterValue("BarkodNo", urunListesi[0].BarkodNo);
                report.SetParameterValue("UrunAdi", urunListesi[0].UrunAdi);
                report.SetParameterValue("Fiyat", urunListesi[0].Fiyat);

                report.Show();

                lblDurum.Text = $"Durum: {urunListesi.Count} farklı ürün etiketi hazırlandı.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Toplu yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public class UrunModel
{
    public string BarkodNo { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Fiyat { get; set; } = string.Empty;
}
