using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastReport;
using FastReport.Data;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace FastReportBarcodeApp;

public partial class Form1 : Form
{
    private readonly string sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
    private HubConnection? hubConnection;

    // Config değerleri
    private readonly string hubUrl;
    private readonly string postgresConnectionString;
    private readonly bool defaultSilentPrint;
    private readonly int bulkPrintCount;

    public Form1()
    {
        InitializeComponent();

        // FastReport Tasarımcısında ve Motorunda PostgreSQL veri sağlayıcısını aktif et:
        FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));

        var config = LoadConfiguration();
        hubUrl                   = config["SignalR:HubUrl"]                          ?? "http://localhost:5000/barkodHub";
        postgresConnectionString = config["ConnectionStrings:PostgreSql"]            ?? string.Empty;
        defaultSilentPrint       = bool.TryParse(config["Printer:SilentPrint"], out bool sp) && sp;
        bulkPrintCount           = int.TryParse(config["BulkPrint:Count"],     out int bc) ? bc : 10;

        YazicilariYukle(config["Printer:Name"]);
        VarsayilanSablonOlustur();
        _ = SignalRBaslat();
    }

    /// <summary>
    /// Bilgisayarda kurulu olan tüm yazıcıları tarar ve ComboBox'a yükler.
    /// </summary>
    private void YazicilariYukle(string? configYaziciAdi)
    {
        cmbYazicilar.Items.Clear();

        string varsayilanYazici = string.Empty;
        try
        {
            var printDoc = new PrintDocument();
            varsayilanYazici = printDoc.PrinterSettings.PrinterName;
        }
        catch { }

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            cmbYazicilar.Items.Add(printer);
        }

        if (cmbYazicilar.Items.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(configYaziciAdi) && cmbYazicilar.Items.Contains(configYaziciAdi))
            {
                cmbYazicilar.SelectedItem = configYaziciAdi;
            }
            else if (!string.IsNullOrWhiteSpace(varsayilanYazici) && cmbYazicilar.Items.Contains(varsayilanYazici))
            {
                cmbYazicilar.SelectedItem = varsayilanYazici;
            }
            else
            {
                cmbYazicilar.SelectedIndex = 0;
            }
        }
    }

    private string SeciliYaziciAdi => cmbYazicilar.SelectedItem?.ToString() ?? string.Empty;

    private static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>
    /// SignalR dinleyicisini başlatır.
    /// 1. Tekli barkod emri
    /// 2. N adet liste barkod emri
    /// 3. PostgreSQL üzerinden sorguyla N adet barkod emri (IstekId / ProtokolNo ile)
    /// </summary>
    private async Task SignalRBaslat()
    {
        try
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // 1. Dışarıdan TEKLİ doğrudan veri emri geldiğinde:
            hubConnection.On<BarkodIstekModel>("BarkodYazdir", (istek) =>
            {
                this.Invoke((MethodInvoker)delegate
                {
                    EtiketiDogrudanYazdir(istek.BarkodNo, istek.HastaAdi, istek.ProtokolNo, istek.Bolum, onizlemeGoster: !defaultSilentPrint);
                });
            });

            // 2. Dışarıdan N ADET (TOPLU LİSTE) veri emri geldiğinde:
            hubConnection.On<List<BarkodIstekModel>>("BarkodYazdirToplu", (istekListesi) =>
            {
                this.Invoke((MethodInvoker)delegate
                {
                    TopluEtiketleriYazdir(istekListesi, onizlemeGoster: !defaultSilentPrint);
                });
            });

            // 3. POSTGRESQL SORGUSUYLA YAZDIRMA EMRİ (FastReport içinden SQL atar):
            // HBYS sadece bir IstekId / ProtokolNo gönderir, FastReport Postgres'ten N adet satırı kendi çeker!
            hubConnection.On<string>("PostgresBarkodYazdir", (parametreDegeri) =>
            {
                this.Invoke((MethodInvoker)delegate
                {
                    EtiketiPostgresIleYazdir(parametreDegeri, onizlemeGoster: !defaultSilentPrint);
                });
            });

            await hubConnection.StartAsync();
            lblSignalRStatus.Text = $"📡 Durum: Dinleyici bağlı. ({hubUrl})";
        }
        catch
        {
            lblSignalRStatus.Text = "📡 Durum: SignalR arka planda dinlemede.";
        }
    }

    /// <summary>
    /// Başlangıç şablonu oluşturur.
    /// </summary>
    private void VarsayilanSablonOlustur()
    {
        if (File.Exists(sablonYolu)) return;

        using (Report report = new Report())
        {
            ReportPage page = new ReportPage();
            page.Name = "EtiketSayfasi";
            page.PaperWidth  = 100;
            page.PaperHeight = 50;
            page.LeftMargin   = 0;
            page.RightMargin  = 0;
            page.TopMargin    = 0;
            page.BottomMargin = 0;

            float mm = FastReport.Utils.Units.Millimeters;

            DataBand dataBand = new DataBand();
            dataBand.Name   = ReportObjectNames.DataBand;
            dataBand.Height = mm * 50;
            page.Bands.Add(dataBand);

            // 1. Hasta Adı
            FastReport.TextObject txtHasta = new FastReport.TextObject();
            txtHasta.Name      = ReportObjectNames.TextHastaAdi;
            txtHasta.Bounds    = new System.Drawing.RectangleF(mm * 3, mm * 2, mm * 94, mm * 8);
            txtHasta.Font      = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold);
            txtHasta.HorzAlign = FastReport.HorzAlign.Center;
            txtHasta.Text      = $"[{ReportObjectNames.ParamHastaAdi}]";
            dataBand.Objects.Add(txtHasta);

            // 2. Barkod (Code128)
            FastReport.Barcode.BarcodeObject barcode = new FastReport.Barcode.BarcodeObject();
            barcode.Name       = ReportObjectNames.Barcode;
            barcode.Bounds     = new System.Drawing.RectangleF(mm * 3, mm * 11, mm * 94, mm * 24);
            barcode.Barcode    = new FastReport.Barcode.Barcode128();
            barcode.AutoSize   = false;
            barcode.ShowText   = true;
            barcode.Expression = $"[{ReportObjectNames.ParamBarkodNo}]";
            dataBand.Objects.Add(barcode);

            // 3. Protokol No
            FastReport.TextObject txtProtokol = new FastReport.TextObject();
            txtProtokol.Name   = ReportObjectNames.TextProtokolNo;
            txtProtokol.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 36, mm * 45, mm * 6);
            txtProtokol.Font   = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
            txtProtokol.Text   = $"Prot: [{ReportObjectNames.ParamProtokolNo}]";
            dataBand.Objects.Add(txtProtokol);

            // 4. Bölüm / Poliklinik
            FastReport.TextObject txtBolumObj = new FastReport.TextObject();
            txtBolumObj.Name       = ReportObjectNames.TextBolum;
            txtBolumObj.Bounds     = new System.Drawing.RectangleF(mm * 48, mm * 36, mm * 49, mm * 6);
            txtBolumObj.Font       = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Regular);
            txtBolumObj.HorzAlign  = FastReport.HorzAlign.Right;
            txtBolumObj.Text       = $"[{ReportObjectNames.ParamBolum}]";
            dataBand.Objects.Add(txtBolumObj);

            // Parametreler
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamBarkodNo)   { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamHastaAdi)   { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamProtokolNo) { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamBolum)      { DataType = typeof(string), Value = string.Empty });

            report.Pages.Add(page);
            report.Save(sablonYolu);
        }
    }

    /// <summary>
    /// Sürükle-Bırak Tasarımcıyı Açar.
    /// FastReport Tasarımcısında Data -> Add Data Source -> PostgreSQL Connection eklenerek doğrudan sorgu bağlanabilir.
    /// </summary>
    private void btnTasarla_Click(object sender, EventArgs e)
    {
        try
        {
            using (Report report = new Report())
            {
                if (File.Exists(sablonYolu))
                    report.Load(sablonYolu);

                // Eğer şablonda kayıtlı bir Postgres bağlantısı varsa, appsettings'deki güncel connection string ile besle
                PostgresBaglantisiniGuncelle(report);

                report.SetParameterValue(ReportObjectNames.ParamBarkodNo,   txtBarkod.Text);
                report.SetParameterValue(ReportObjectNames.ParamHastaAdi,   txtHastaAdi.Text);
                report.SetParameterValue(ReportObjectNames.ParamProtokolNo, txtProtokolNo.Text);
                report.SetParameterValue(ReportObjectNames.ParamBolum,      txtBolum.Text);

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
    /// Şablondaki DataConnection'ların ConnectionString'ini appsettings.json'dan dinamik olarak günceller.
    /// Böylece veritabanı şifresi veya sunucu IP'si değiştiğinde .frx dosyasını baştan tasarlamak gerekmez!
    /// </summary>
    private void PostgresBaglantisiniGuncelle(Report report)
    {
        if (string.IsNullOrWhiteSpace(postgresConnectionString)) return;

        foreach (DataConnectionBase conn in report.Dictionary.Connections)
        {
            if (conn is PostgresDataConnection || conn.GetType().Name.Contains("Postgres"))
            {
                conn.ConnectionString = postgresConnectionString;
            }
        }
    }

    /// <summary>
    /// POSTGRESQL ENTEGRELİ BASKI METODU:
    /// FastReport'un KENDİ İÇİNDEN PostgreSQL'e sorgu atmasını sağlar!
    /// HBYS sadece bir IstekId / ProtokolNo verir; FastReport N adet barkodu Postgres'ten çeker ve seçili yazıcıya basar.
    /// </summary>
    public void EtiketiPostgresIleYazdir(string filtreDegeri, bool onizlemeGoster)
    {
        try
        {
            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                PostgresBaglantisiniGuncelle(report);

                // FastReport şablonunda tanımlı olan @IstekId veya @ProtokolNo parametresini ayarla
                report.SetParameterValue("IstekId", filtreDegeri);
                report.SetParameterValue("ProtokolNo", filtreDegeri);

                if (onizlemeGoster)
                {
                    report.Show();
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(SeciliYaziciAdi))
                        report.PrintSettings.Printer = SeciliYaziciAdi;

                    report.PrintSettings.ShowDialog = false;
                    report.Print();
                }

                lblDurum.Text = $"Durum: PostgreSQL sorgusu çalıştırıldı -> '{SeciliYaziciAdi}' yazıcısına basıldı.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("PostgreSQL ile yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Formdaki değerlerle tekli test.
    /// </summary>
    private void btnYazdirTekli_Click(object sender, EventArgs e)
    {
        EtiketiDogrudanYazdir(
            txtBarkod.Text.Trim(),
            txtHastaAdi.Text.Trim(),
            txtProtokolNo.Text.Trim(),
            txtBolum.Text.Trim(),
            onizlemeGoster: true);
    }

    /// <summary>
    /// SİMÜLASYON: N Adet Barkod Emri Simülasyonu
    /// </summary>
    private void btnSignalRSimule_Click(object sender, EventArgs e)
    {
        string barkod = string.IsNullOrWhiteSpace(txtBarkod.Text) ? "869012345001" : txtBarkod.Text.Trim();
        string hasta = string.IsNullOrWhiteSpace(txtHastaAdi.Text) ? "Ayşe Kaya" : txtHastaAdi.Text.Trim();
        string protokol = string.IsNullOrWhiteSpace(txtProtokolNo.Text) ? "2026-00451" : txtProtokolNo.Text.Trim();
        string bolum = string.IsNullOrWhiteSpace(txtBolum.Text) ? "Dahiliye" : txtBolum.Text.Trim();

        var ornekListe = new List<BarkodIstekModel>
        {
            new() { BarkodNo = $"{barkod}-01", HastaAdi = hasta, ProtokolNo = protokol, Bolum = $"{bolum} (Biyokimya Tüpü)" },
            new() { BarkodNo = $"{barkod}-02", HastaAdi = hasta, ProtokolNo = protokol, Bolum = $"{bolum} (Hemogram Tüpü)" },
            new() { BarkodNo = $"{barkod}-03", HastaAdi = hasta, ProtokolNo = protokol, Bolum = $"{bolum} (Sedimantasyon Tüpü)" }
        };

        TopluEtiketleriYazdir(ornekListe, onizlemeGoster: true);

        MessageBox.Show(
            $"N-Adet Barkod Emri Simüle Edildi!\n\n" +
            $"Toplam {ornekListe.Count} adet numune tüpü etiketi üretildi.\n" +
            $"Hedef Yazıcı: '{SeciliYaziciAdi}'",
            "N-Adet Barkod Emri Başarılı",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>
    /// TEKLİ ETİKET BASKI METODU: ComboBox'ta seçili yazıcıya yönlendirir.
    /// </summary>
    private void EtiketiDogrudanYazdir(string barkodNo, string hastaAdi, string protokolNo, string bolum, bool onizlemeGoster)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(barkodNo))
            {
                MessageBox.Show("Barkod numarası boş olamaz!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                report.SetParameterValue(ReportObjectNames.ParamBarkodNo,   barkodNo);
                report.SetParameterValue(ReportObjectNames.ParamHastaAdi,   hastaAdi);
                report.SetParameterValue(ReportObjectNames.ParamProtokolNo, protokolNo);
                report.SetParameterValue(ReportObjectNames.ParamBolum,      bolum);

                if (onizlemeGoster)
                {
                    report.Show();
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(SeciliYaziciAdi))
                        report.PrintSettings.Printer = SeciliYaziciAdi;

                    report.PrintSettings.ShowDialog = false;
                    report.Print();
                }

                lblDurum.Text = $"Durum: [{barkodNo}] -> '{SeciliYaziciAdi}' yazıcısına gönderildi.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// N ADET (TOPLU) ETİKET BASKI METODU:
    /// Web'den gelen N adet barkod emrini seçili yazıcıya basar.
    /// </summary>
    public void TopluEtiketleriYazdir(List<BarkodIstekModel> istekler, bool onizlemeGoster)
    {
        if (istekler == null || istekler.Count == 0) return;

        try
        {
            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                report.RegisterData(istekler, ReportObjectNames.DataSourceName);
                var dataSource = report.GetDataSource(ReportObjectNames.DataSourceName);
                dataSource.Enabled = true;

                var dataBand = report.FindObject(ReportObjectNames.DataBand) as DataBand;
                if (dataBand != null)
                    dataBand.DataSource = dataSource;

                var barcode = report.FindObject(ReportObjectNames.Barcode) as FastReport.Barcode.BarcodeObject;
                if (barcode != null)
                    barcode.Expression = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamBarkodNo}]";

                var txtHasta = report.FindObject(ReportObjectNames.TextHastaAdi) as FastReport.TextObject;
                if (txtHasta != null)
                    txtHasta.Text = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamHastaAdi}]";

                var txtProtokol = report.FindObject(ReportObjectNames.TextProtokolNo) as FastReport.TextObject;
                if (txtProtokol != null)
                    txtProtokol.Text = $"Prot: [{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamProtokolNo}]";

                var txtBolumObj = report.FindObject(ReportObjectNames.TextBolum) as FastReport.TextObject;
                if (txtBolumObj != null)
                    txtBolumObj.Text = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamBolum}]";

                if (onizlemeGoster)
                {
                    report.Show();
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(SeciliYaziciAdi))
                        report.PrintSettings.Printer = SeciliYaziciAdi;

                    report.PrintSettings.ShowDialog = false;
                    report.Print();
                }

                lblDurum.Text = $"Durum: {istekler.Count} adet etiket -> '{SeciliYaziciAdi}' yazıcısına gönderildi.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Toplu yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Manuel form butonundan N adet test etiketi basma.
    /// </summary>
    private void btnYazdirToplu_Click(object sender, EventArgs e)
    {
        string barkod = string.IsNullOrWhiteSpace(txtBarkod.Text) ? "869000000" : txtBarkod.Text.Trim();
        string hasta = string.IsNullOrWhiteSpace(txtHastaAdi.Text) ? "Test Hasta" : txtHastaAdi.Text.Trim();
        string protokol = string.IsNullOrWhiteSpace(txtProtokolNo.Text) ? "2026-900" : txtProtokolNo.Text.Trim();
        string bolum = string.IsNullOrWhiteSpace(txtBolum.Text) ? "Biyokimya" : txtBolum.Text.Trim();

        var numuneListesi = new List<BarkodIstekModel>();
        for (int i = 1; i <= bulkPrintCount; i++)
        {
            numuneListesi.Add(new BarkodIstekModel
            {
                BarkodNo   = $"{barkod}-{i:D3}",
                HastaAdi   = hasta,
                ProtokolNo = $"{protokol}-{i:D3}",
                Bolum      = $"{bolum} / Tüp #{i}"
            });
        }

        TopluEtiketleriYazdir(numuneListesi, onizlemeGoster: true);
    }
}

public class BarkodIstekModel
{
    public string BarkodNo   { get; set; } = string.Empty;
    public string HastaAdi   { get; set; } = string.Empty;
    public string ProtokolNo { get; set; } = string.Empty;
    public string Bolum      { get; set; } = string.Empty;
}
