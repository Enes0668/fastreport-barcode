using System;
using System.IO;
using System.Windows.Forms;
using FastReport;
using FastReport.Data;

namespace FastReportBarcodeApp;

static class Program
{
    /// <summary>
    /// Uygulama çalıştırıldığında hiçbir gereksiz ara pencere açmadan
    /// doğrudan FastReport Görsel Şablon Tasarımcısını (.frx) açar ve
    /// kaydedildiğinde Ajan klasörlerine otomatik eşitler.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            // FastReport motoruna PostgreSQL bağlantı desteğini kaydet (Tasarımcıda Data menüsünde çıkması için)
            FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));
        }
        catch { }

        string sablonYolu = SablonYolunuGetir();

        if (!File.Exists(sablonYolu))
        {
            VarsayilanSablonOlustur(sablonYolu);
        }

        try
        {
            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                // Doğrudan FastReport Görsel Tasarımcısını aç!
                report.Design();

                // Tasarımcı kapatıldığında güncellenen .frx şablonunu Ajan klasörlerine otomatik eşitle
                SenkronizeEtSablonu(sablonYolu);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"FastReport Tasarımcısı açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string SablonYolunuGetir()
    {
        string anaDizin = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
        string kokSablon = Path.Combine(anaDizin, "etiket_sablonu.frx");

        if (File.Exists(kokSablon))
            return kokSablon;

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
    }

    private static void SenkronizeEtSablonu(string kaynakSablonYolu)
    {
        try
        {
            if (!File.Exists(kaynakSablonYolu)) return;

            string anaDizin = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
            string[] hedefler =
            {
                Path.Combine(anaDizin, "etiket_sablonu.frx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx"),
                Path.Combine(anaDizin, "BarcodeAgentConsole", "etiket_sablonu.frx"),
                Path.Combine(anaDizin, "BarcodeAgentConsole", "bin", "Debug", "net8.0-windows", "etiket_sablonu.frx")
            };

            string kaynakTamYol = Path.GetFullPath(kaynakSablonYolu);

            foreach (var hedef in hedefler)
            {
                string hedefTamYol = Path.GetFullPath(hedef);
                if (string.Equals(kaynakTamYol, hedefTamYol, StringComparison.OrdinalIgnoreCase))
                    continue;

                string? klasor = Path.GetDirectoryName(hedefTamYol);
                if (!string.IsNullOrEmpty(klasor) && Directory.Exists(klasor))
                {
                    File.Copy(kaynakTamYol, hedefTamYol, overwrite: true);
                }
            }
        }
        catch { }
    }

    private static void VarsayilanSablonOlustur(string sablonYolu)
    {
        using (Report report = new Report())
        {
            ReportPage page = new ReportPage
            {
                Name = "EtiketSayfasi",
                PaperWidth = 100,
                PaperHeight = 50,
                LeftMargin = 2,
                RightMargin = 2,
                TopMargin = 2,
                BottomMargin = 2
            };

            float mm = FastReport.Utils.Units.Millimeters;

            DataBand dataBand = new DataBand
            {
                Name = ReportObjectNames.DataBand,
                Height = mm * 46
            };
            page.Bands.Add(dataBand);

            FastReport.TextObject txtHasta = new FastReport.TextObject
            {
                Name = ReportObjectNames.TextHastaAdi,
                Bounds = new System.Drawing.RectangleF(mm * 2, mm * 1, mm * 92, mm * 7),
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                HorzAlign = FastReport.HorzAlign.Center,
                VertAlign = FastReport.VertAlign.Center,
                Text = "[HastaAdi]"
            };
            dataBand.Objects.Add(txtHasta);

            FastReport.Barcode.BarcodeObject barcode = new FastReport.Barcode.BarcodeObject
            {
                Name = ReportObjectNames.Barcode,
                Bounds = new System.Drawing.RectangleF(mm * 5, mm * 9, mm * 86, mm * 24),
                Barcode = new FastReport.Barcode.Barcode128(),
                AutoSize = true,
                ShowText = true,
                HorzAlign = FastReport.Barcode.BarcodeObject.Alignment.Center,
                Expression = "[BarkodNo]"
            };
            dataBand.Objects.Add(barcode);

            FastReport.TextObject txtProtokol = new FastReport.TextObject
            {
                Name = ReportObjectNames.TextProtokolNo,
                Bounds = new System.Drawing.RectangleF(mm * 2, mm * 34, mm * 45, mm * 6),
                Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold),
                VertAlign = FastReport.VertAlign.Center,
                Text = "Prot: [ProtokolNo]"
            };
            dataBand.Objects.Add(txtProtokol);

            FastReport.TextObject txtBolumObj = new FastReport.TextObject
            {
                Name = ReportObjectNames.TextBolum,
                Bounds = new System.Drawing.RectangleF(mm * 48, mm * 34, mm * 46, mm * 6),
                Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Regular),
                HorzAlign = FastReport.HorzAlign.Right,
                VertAlign = FastReport.VertAlign.Center,
                Text = "[Bolum]"
            };
            dataBand.Objects.Add(txtBolumObj);

            report.Parameters.Add(new FastReport.Data.Parameter("IstekId") { DataType = typeof(string), Value = "504" });
            report.Parameters.Add(new FastReport.Data.Parameter("BarkodNo") { DataType = typeof(string), Value = "869012345001" });
            report.Parameters.Add(new FastReport.Data.Parameter("HastaAdi") { DataType = typeof(string), Value = "Ahmet Yılmaz" });
            report.Parameters.Add(new FastReport.Data.Parameter("ProtokolNo") { DataType = typeof(string), Value = "2026-98451" });
            report.Parameters.Add(new FastReport.Data.Parameter("Bolum") { DataType = typeof(string), Value = "Dahiliye / Biyokimya" });

            report.Pages.Add(page);
            report.Save(sablonYolu);
        }
    }
}
