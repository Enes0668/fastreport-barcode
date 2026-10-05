using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastReport;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace FastReportBarcodeApp;

public partial class Form1 : Form
{
    private readonly string sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
    private HubConnection? hubConnection;

    // Config değerleri appsettings.json'dan okunur
    private readonly string hubUrl;
    private readonly string printerName;
    private readonly bool silentPrint;
    private readonly int bulkPrintCount;

    public Form1()
    {
        InitializeComponent();
        var config = LoadConfiguration();
        hubUrl         = config["SignalR:HubUrl"]      ?? "http://localhost:5000/barkodHub";
        printerName    = config["Printer:Name"]        ?? string.Empty;
        silentPrint    = bool.TryParse(config["Printer:SilentPrint"], out bool sp) && sp;
        bulkPrintCount = int.TryParse(config["BulkPrint:Count"],     out int bc) ? bc : 10;

        VarsayilanSablonOlustur();
        _ = SignalRBaslat();
    }

    /// <summary>
    /// appsettings.json'u yükler.
    /// </summary>
    private static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>
    /// SignalR istemcisini başlatır. Hub adresi appsettings.json üzerinden gelir.
    /// </summary>
    private async Task SignalRBaslat()
    {
        try
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Web'den "BarkodYazdir" emri geldiğinde tetiklenecek fonksiyon:
            hubConnection.On<BarkodIstekModel>("BarkodYazdir", (istek) =>
            {
                this.Invoke((MethodInvoker)delegate
                {
                    EtiketiDogrudanYazdir(istek.BarkodNo, istek.HastaAdi, istek.ProtokolNo, istek.Bolum, onizlemeGoster: !silentPrint);
                });
            });

            await hubConnection.StartAsync();
            lblSignalRStatus.Text = $"📡 Durum: SignalR Hub'a bağlı. ({hubUrl})";
        }
        catch
        {
            lblSignalRStatus.Text = "📡 Durum: Dinleyici aktif (Simülasyon butonu ile test edilebilir).";
        }
    }

    /// <summary>
    /// Hastane etiket standardına uygun (100mm x 50mm) temiz başlangıç şablonu oluşturur.
    /// Parametre varsayılan değerleri boş bırakılır; gerçek veriler yazdırma sırasında enjekte edilir.
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

            // DataBand
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

            // Parametreler — varsayılan değerler boş; gerçek veri runtime'da enjekte edilir
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamBarkodNo)   { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamHastaAdi)   { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamProtokolNo) { DataType = typeof(string), Value = string.Empty });
            report.Parameters.Add(new FastReport.Data.Parameter(ReportObjectNames.ParamBolum)      { DataType = typeof(string), Value = string.Empty });

            report.Pages.Add(page);
            report.Save(sablonYolu);
        }
    }

    /// <summary>
    /// SÜRÜKLE-BIRAK TASARIMCIYI AÇAR (report.Design())
    /// </summary>
    private void btnTasarla_Click(object sender, EventArgs e)
    {
        try
        {
            using (Report report = new Report())
            {
                if (File.Exists(sablonYolu))
                    report.Load(sablonYolu);

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
    /// Manuel Buton: Formdaki değerlerle önizleme açar.
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
    /// SİMÜLASYON BUTONU: Formdaki mevcut değerleri SignalR'dan gelmiş gibi işler.
    /// Test verisi hardcode yerine form alanlarından okunur.
    /// </summary>
    private void btnSignalRSimule_Click(object sender, EventArgs e)
    {
        var simuleIstek = new BarkodIstekModel
        {
            BarkodNo   = txtBarkod.Text.Trim(),
            HastaAdi   = txtHastaAdi.Text.Trim(),
            ProtokolNo = txtProtokolNo.Text.Trim(),
            Bolum      = txtBolum.Text.Trim()
        };

        EtiketiDogrudanYazdir(
            simuleIstek.BarkodNo,
            simuleIstek.HastaAdi,
            simuleIstek.ProtokolNo,
            simuleIstek.Bolum,
            onizlemeGoster: !silentPrint);

        MessageBox.Show(
            $"SignalR Web İsteği Simüle Edildi!\n\n" +
            $"Gelen Veri:\nHasta: {simuleIstek.HastaAdi}\nProtokol: {simuleIstek.ProtokolNo}\nBölüm: {simuleIstek.Bolum}\nBarkod: {simuleIstek.BarkodNo}\n\n" +
            $"Bu istek '{printerName}' yazıcısına fırlatıldı!",
            "SignalR Entegrasyon Başarılı",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>
    /// Tüm yazdırma işlemlerinin geçtiği çekirdek metod.
    /// Yazıcı adı ve sessiz baskı ayarı appsettings.json üzerinden okunur.
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

                // HBYS'den gelen verileri şablondaki parametrelere aktar
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
                    // Yazıcı adı appsettings.json → Printer:Name'den gelir
                    if (!string.IsNullOrWhiteSpace(printerName))
                        report.PrintSettings.Printer = printerName;

                    report.PrintSettings.ShowDialog = false;
                    report.Print();
                }

                lblDurum.Text = $"Durum: [{barkodNo}] barkodlu hasta etiketi başarıyla basıldı.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// TOPLU ETİKET BASMA: Numune listesi appsettings.json → BulkPrint:Count kadar üretilir.
    /// Gerçek projede bu liste DB veya HBYS API'sından sağlanmalıdır.
    /// </summary>
    private void btnYazdirToplu_Click(object sender, EventArgs e)
    {
        try
        {
            // TODO (Gerçek Uygulama): numuneListesi DB veya HBYS API'sından doldurulmalıdır.
            var numuneListesi = new List<NumuneModel>();
            for (int i = 1; i <= bulkPrintCount; i++)
            {
                numuneListesi.Add(new NumuneModel
                {
                    BarkodNo   = $"{txtBarkod.Text.Trim()}-{i:D3}",
                    HastaAdi   = txtHastaAdi.Text.Trim(),
                    ProtokolNo = $"{txtProtokolNo.Text.Trim()}-{i:D3}",
                    Bolum      = $"{txtBolum.Text.Trim()} / Tüp #{i}"
                });
            }

            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                report.RegisterData(numuneListesi, ReportObjectNames.DataSourceName);
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

                report.Show();

                lblDurum.Text = $"Durum: {numuneListesi.Count} adet kan numune tüpü etiketi hazırlandı.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Toplu yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public class BarkodIstekModel
{
    public string BarkodNo   { get; set; } = string.Empty;
    public string HastaAdi   { get; set; } = string.Empty;
    public string ProtokolNo { get; set; } = string.Empty;
    public string Bolum      { get; set; } = string.Empty;
}

public class NumuneModel
{
    public string BarkodNo   { get; set; } = string.Empty;
    public string HastaAdi   { get; set; } = string.Empty;
    public string ProtokolNo { get; set; } = string.Empty;
    public string Bolum      { get; set; } = string.Empty;
}
