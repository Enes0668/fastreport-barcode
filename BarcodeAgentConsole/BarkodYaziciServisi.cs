using System;
using System.Collections.Generic;
using System.IO;
using FastReport;
using FastReport.Data;

namespace BarcodeAgentConsole;

public class BarkodYaziciServisi
{
    private readonly string _sablonYolu;
    private readonly string _postgresConnectionString;
    public string HedefYazici { get; set; }

    public BarkodYaziciServisi(string hedefYazici, string postgresConnString)
    {
        HedefYazici = hedefYazici;
        _postgresConnectionString = postgresConnString;
        _sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");

        // FastReport motoruna PostgreSQL bağlantısını kaydet
        FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));

        VarsayilanSablonYoksaOlustur();
    }

    /// <summary>
    /// TEKLİ BARKOD BASMA METODU (Sessiz)
    /// </summary>
    public bool TekliBarkodBas(BarkodIstekModel istek)
    {
        try
        {
            using (Report report = new Report())
            {
                report.Load(_sablonYolu);

                report.SetParameterValue(ReportObjectNames.ParamBarkodNo,   istek.BarkodNo);
                report.SetParameterValue(ReportObjectNames.ParamHastaAdi,   istek.HastaAdi);
                report.SetParameterValue(ReportObjectNames.ParamProtokolNo, istek.ProtokolNo);
                report.SetParameterValue(ReportObjectNames.ParamBolum,      istek.Bolum);

                report.Prepare();

                if (!string.IsNullOrWhiteSpace(HedefYazici))
                    report.PrintSettings.Printer = HedefYazici;

                report.PrintSettings.ShowDialog = false;
                report.PrintPrepared();

                LogMesaj($"[TEKLİ BASKI] [{istek.BarkodNo}] {istek.HastaAdi} -> '{HedefYazici}' yazıcısına basıldı.", ConsoleColor.Green);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMesaj($"[HATA] Tekli barkod basılamadı: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    /// <summary>
    /// N ADET (TOPLU LİSTE) BARKOD BASMA METODU (Sessiz)
    /// </summary>
    public bool TopluBarkodBas(List<BarkodIstekModel> istekler)
    {
        if (istekler == null || istekler.Count == 0) return false;

        try
        {
            using (Report report = new Report())
            {
                report.Load(_sablonYolu);

                report.RegisterData(istekler, ReportObjectNames.DataSourceName);
                var dataSource = report.GetDataSource(ReportObjectNames.DataSourceName);
                dataSource.Enabled = true;

                var dataBand = report.FindObject(ReportObjectNames.DataBand) as DataBand;
                if (dataBand != null)
                    dataBand.DataSource = dataSource;

                var barcode = report.FindObject(ReportObjectNames.Barcode) as FastReport.Barcode.BarcodeObject;
                if (barcode != null)
                {
                    barcode.Expression = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamBarkodNo}]";
                    barcode.AutoSize = true;
                    barcode.HorzAlign = FastReport.Barcode.BarcodeObject.Alignment.Center;
                }

                var txtHasta = report.FindObject(ReportObjectNames.TextHastaAdi) as FastReport.TextObject;
                if (txtHasta != null)
                    txtHasta.Text = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamHastaAdi}]";

                var txtProtokol = report.FindObject(ReportObjectNames.TextProtokolNo) as FastReport.TextObject;
                if (txtProtokol != null)
                    txtProtokol.Text = $"Prot: [{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamProtokolNo}]";

                var txtBolumObj = report.FindObject(ReportObjectNames.TextBolum) as FastReport.TextObject;
                if (txtBolumObj != null)
                    txtBolumObj.Text = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamBolum}]";

                report.Prepare();

                if (!string.IsNullOrWhiteSpace(HedefYazici))
                    report.PrintSettings.Printer = HedefYazici;

                report.PrintSettings.ShowDialog = false;
                report.PrintPrepared();

                LogMesaj($"[TOPLU BASKI] {istekler.Count} adet etiket '{HedefYazici}' yazıcısına başarıyla gönderildi.", ConsoleColor.Green);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMesaj($"[HATA] Toplu barkod basılamadı: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    /// <summary>
    /// POSTGRESQL SORGUSUYLA BARKOD BASMA:
    /// FastReport doğrudan veritabanına SQL atar ve N adet barkodu çeker.
    /// </summary>
    public bool PostgresIleBarkodBas(string istekId)
    {
        try
        {
            using (Report report = new Report())
            {
                report.Load(_sablonYolu);

                // PostgreSQL connection string'ini şablona enjekte et
                if (!string.IsNullOrWhiteSpace(_postgresConnectionString))
                {
                    foreach (DataConnectionBase conn in report.Dictionary.Connections)
                    {
                        if (conn is PostgresDataConnection || conn.GetType().Name.Contains("Postgres"))
                            conn.ConnectionString = _postgresConnectionString;
                    }
                }

                report.SetParameterValue("IstekId", istekId);
                report.SetParameterValue("ProtokolNo", istekId);

                report.Prepare();

                if (!string.IsNullOrWhiteSpace(HedefYazici))
                    report.PrintSettings.Printer = HedefYazici;

                report.PrintSettings.ShowDialog = false;
                report.PrintPrepared();

                LogMesaj($"[POSTGRES BASKI] İstek ID #{istekId} -> PostgreSQL sorgusuyla çekildi ve '{HedefYazici}' yazıcısına basıldı.", ConsoleColor.Cyan);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMesaj($"[HATA] PostgreSQL ile yazdırma başarısız: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    private void VarsayilanSablonYoksaOlustur()
    {
        if (File.Exists(_sablonYolu)) return;

        using (Report report = new Report())
        {
            ReportPage page = new ReportPage();
            page.Name = "EtiketSayfasi";
            page.PaperWidth  = 100;
            page.PaperHeight = 50;
            page.LeftMargin   = 2;
            page.RightMargin  = 2;
            page.TopMargin    = 2;
            page.BottomMargin = 2;

            float mm = FastReport.Utils.Units.Millimeters;

            DataBand dataBand = new DataBand();
            dataBand.Name   = ReportObjectNames.DataBand;
            dataBand.Height = mm * 46;
            page.Bands.Add(dataBand);

            FastReport.TextObject txtHasta = new FastReport.TextObject();
            txtHasta.Name      = ReportObjectNames.TextHastaAdi;
            txtHasta.Bounds    = new System.Drawing.RectangleF(mm * 2, mm * 1, mm * 92, mm * 7);
            txtHasta.Font      = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold);
            txtHasta.HorzAlign = FastReport.HorzAlign.Center;
            txtHasta.VertAlign = FastReport.VertAlign.Center;
            txtHasta.Text      = $"[{ReportObjectNames.ParamHastaAdi}]";
            dataBand.Objects.Add(txtHasta);

            FastReport.Barcode.BarcodeObject barcode = new FastReport.Barcode.BarcodeObject();
            barcode.Name       = ReportObjectNames.Barcode;
            barcode.Bounds     = new System.Drawing.RectangleF(mm * 5, mm * 9, mm * 86, mm * 24);
            barcode.Barcode    = new FastReport.Barcode.Barcode128();
            barcode.AutoSize   = true;
            barcode.ShowText   = true;
            barcode.HorzAlign  = FastReport.Barcode.BarcodeObject.Alignment.Center;
            barcode.Expression = $"[{ReportObjectNames.ParamBarkodNo}]";
            dataBand.Objects.Add(barcode);

            FastReport.TextObject txtProtokol = new FastReport.TextObject();
            txtProtokol.Name      = ReportObjectNames.TextProtokolNo;
            txtProtokol.Bounds    = new System.Drawing.RectangleF(mm * 2, mm * 34, mm * 45, mm * 6);
            txtProtokol.Font      = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
            txtProtokol.VertAlign = FastReport.VertAlign.Center;
            txtProtokol.Text      = $"Prot: [{ReportObjectNames.ParamProtokolNo}]";
            dataBand.Objects.Add(txtProtokol);

            FastReport.TextObject txtBolumObj = new FastReport.TextObject();
            txtBolumObj.Name       = ReportObjectNames.TextBolum;
            txtBolumObj.Bounds     = new System.Drawing.RectangleF(mm * 48, mm * 34, mm * 46, mm * 6);
            txtBolumObj.Font       = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Regular);
            txtBolumObj.HorzAlign  = FastReport.HorzAlign.Right;
            txtBolumObj.VertAlign  = FastReport.VertAlign.Center;
            txtBolumObj.Text       = $"[{ReportObjectNames.ParamBolum}]";
            dataBand.Objects.Add(txtBolumObj);

            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamBarkodNo)   { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamHastaAdi)   { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamProtokolNo) { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamBolum)      { DataType = typeof(string), Value = string.Empty });

            report.Pages.Add(page);
            report.Save(_sablonYolu);
        }
    }

    private static void LogMesaj(string mesaj, ConsoleColor renk)
    {
        var eskiRenk = Console.ForegroundColor;
        Console.ForegroundColor = renk;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {mesaj}");
        Console.ForegroundColor = eskiRenk;
    }
}
