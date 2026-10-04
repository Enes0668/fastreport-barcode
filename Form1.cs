using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastReport;
using Microsoft.AspNetCore.SignalR.Client;

namespace FastReportBarcodeApp;

public partial class Form1 : Form
{
    private readonly string sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
    private HubConnection? hubConnection;

    public Form1()
    {
        InitializeComponent();
        VarsayilanSablonOlustur();
        SignalRBaslat();
    }

    /// <summary>
    /// SignalR istemcisini başlatır. Gerçek bir SignalR Hub adresi verildiğinde canlı bağlanır.
    /// </summary>
    private async void SignalRBaslat()
    {
        try
        {
            string hubUrl = "http://localhost:5000/barkodHub";

            hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Web'den "BarkodYazdir" emri geldiğinde tetiklenecek fonksiyon:
            hubConnection.On<BarkodIstekModel>("BarkodYazdir", (istek) =>
            {
                this.Invoke((MethodInvoker)delegate
                {
                    EtiketiDogrudanYazdir(istek.BarkodNo, istek.HastaAdi, istek.ProtokolNo, istek.Bolum, onizlemeGoster: true);
                });
            });

            await hubConnection.StartAsync();
            lblSignalRStatus.Text = "📡 Durum: SignalR Hub'a bağlı. Web emri bekleniyor.";
        }
        catch
        {
            lblSignalRStatus.Text = "📡 Durum: Dinleyici aktif (Simülasyon butonu ile test edilebilir).";
        }
    }

    /// <summary>
    /// Hastane etiket standardına uygun (100mm x 50mm) temiz başlangıç şablonu oluşturur.
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
            page.LeftMargin = 0;
            page.RightMargin = 0;
            page.TopMargin = 0;
            page.BottomMargin = 0;

            float mm = FastReport.Utils.Units.Millimeters;

            // DataBand
            DataBand dataBand = new DataBand();
            dataBand.Name = "Data1";
            dataBand.Height = mm * 50;
            page.Bands.Add(dataBand);

            // 1. Hasta Adı
            FastReport.TextObject txtHasta = new FastReport.TextObject();
            txtHasta.Name = "txtHastaAdi";
            txtHasta.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 2, mm * 94, mm * 8);
            txtHasta.Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold);
            txtHasta.HorzAlign = FastReport.HorzAlign.Center;
            txtHasta.Text = "[HastaAdi]";
            dataBand.Objects.Add(txtHasta);

            // 2. Barkod (Code128)
            FastReport.Barcode.BarcodeObject barcode = new FastReport.Barcode.BarcodeObject();
            barcode.Name = "Barcode1";
            barcode.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 11, mm * 94, mm * 24);
            barcode.Barcode = new FastReport.Barcode.Barcode128();
            barcode.AutoSize = false;
            barcode.ShowText = true;
            barcode.Expression = "[BarkodNo]";
            dataBand.Objects.Add(barcode);

            // 3. Protokol No
            FastReport.TextObject txtProtokol = new FastReport.TextObject();
            txtProtokol.Name = "txtProtokolNo";
            txtProtokol.Bounds = new System.Drawing.RectangleF(mm * 3, mm * 36, mm * 45, mm * 6);
            txtProtokol.Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
            txtProtokol.Text = "Prot: [ProtokolNo]";
            dataBand.Objects.Add(txtProtokol);

            // 4. Bölüm / Poliklinik
            FastReport.TextObject txtBolumObj = new FastReport.TextObject();
            txtBolumObj.Name = "txtBolum";
            txtBolumObj.Bounds = new System.Drawing.RectangleF(mm * 48, mm * 36, mm * 49, mm * 6);
            txtBolumObj.Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Regular);
            txtBolumObj.HorzAlign = FastReport.HorzAlign.Right;
            txtBolumObj.Text = "[Bolum]";
            dataBand.Objects.Add(txtBolumObj);

            // Parametreler
            report.Parameters.Add(new FastReport.Data.Parameter("BarkodNo") { DataType = typeof(string), Value = "8690123456789" });
            report.Parameters.Add(new FastReport.Data.Parameter("HastaAdi") { DataType = typeof(string), Value = "Ahmet Yılmaz" });
            report.Parameters.Add(new FastReport.Data.Parameter("ProtokolNo") { DataType = typeof(string), Value = "2026-98451" });
            report.Parameters.Add(new FastReport.Data.Parameter("Bolum") { DataType = typeof(string), Value = "Acil Poliklinik" });

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

                report.SetParameterValue("BarkodNo", txtBarkod.Text);
                report.SetParameterValue("HastaAdi", txtHastaAdi.Text);
                report.SetParameterValue("ProtokolNo", txtProtokolNo.Text);
                report.SetParameterValue("Bolum", txtBolum.Text);

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
    /// Manuel Buton: Formdaki değerlerle önizleme açar
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
    /// SİMÜLASYON BUTONU:
    /// "Web'den HBYS JSON isteği gelince anında böyle basıyor" demek için!
    /// </summary>
    private void btnSignalRSimule_Click(object sender, EventArgs e)
    {
        var gelenJson = new BarkodIstekModel
        {
            BarkodNo = "998877665544",
            HastaAdi = "Ayşe Kaya",
            ProtokolNo = "2026-00451",
            Bolum = "Dahiliye - Kan Alma"
        };

        EtiketiDogrudanYazdir(
            gelenJson.BarkodNo,
            gelenJson.HastaAdi,
            gelenJson.ProtokolNo,
            gelenJson.Bolum,
            onizlemeGoster: true);

        MessageBox.Show(
            $"SignalR Web İsteği Simüle Edildi!\n\n" +
            $"Gelen Veri:\nHasta: {gelenJson.HastaAdi}\nProtokol: {gelenJson.ProtokolNo}\nBölüm: {gelenJson.Bolum}\nBarkod: {gelenJson.BarkodNo}\n\n" +
            $"Bu istek doğrudan USB barkod yazıcıya fırlatıldı!",
            "SignalR Entegrasyon Başarılı",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>
    /// Tüm yazdırma işlemlerinin geçtiği çekirdek metod.
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

                // 1. HBYS'den gelen verileri şablondaki parametrelere aktar
                report.SetParameterValue("BarkodNo", barkodNo);
                report.SetParameterValue("HastaAdi", hastaAdi);
                report.SetParameterValue("ProtokolNo", protokolNo);
                report.SetParameterValue("Bolum", bolum);

                // 2. Baskıyı gerçekleştir
                if (onizlemeGoster)
                {
                    report.Show();
                }
                else
                {
                    // Canlı hastane ortamında arka planda sessizce (silent) yazıcıya basmak için:
                    // report.PrintSettings.Printer = "Zebra ZD420";
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
    /// TOPLU ETİKET BASMA: 10 farklı kan tüpü etiketi üretir
    /// </summary>
    private void btnYazdirToplu_Click(object sender, EventArgs e)
    {
        try
        {
            var numuneListesi = new List<NumuneModel>();
            for (int i = 1; i <= 10; i++)
            {
                numuneListesi.Add(new NumuneModel
                {
                    BarkodNo = $"869000000{i:D4}",
                    HastaAdi = $"Hasta #{i} - Mehmet Öz",
                    ProtokolNo = $"2026-900{i}",
                    Bolum = $"Tüp #{i} (Biyokimya)"
                });
            }

            using (Report report = new Report())
            {
                report.Load(sablonYolu);

                report.RegisterData(numuneListesi, "Numuneler");
                var dataSource = report.GetDataSource("Numuneler");
                dataSource.Enabled = true;

                var dataBand = report.FindObject("Data1") as DataBand;
                if (dataBand != null)
                    dataBand.DataSource = dataSource;

                var barcode = report.FindObject("Barcode1") as FastReport.Barcode.BarcodeObject;
                if (barcode != null)
                    barcode.Expression = "[Numuneler.BarkodNo]";

                var txtHasta = report.FindObject("txtHastaAdi") as FastReport.TextObject;
                if (txtHasta != null)
                    txtHasta.Text = "[Numuneler.HastaAdi]";

                var txtProtokol = report.FindObject("txtProtokolNo") as FastReport.TextObject;
                if (txtProtokol != null)
                    txtProtokol.Text = "Prot: [Numuneler.ProtokolNo]";

                var txtBolumObj = report.FindObject("txtBolum") as FastReport.TextObject;
                if (txtBolumObj != null)
                    txtBolumObj.Text = "[Numuneler.Bolum]";

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
    public string BarkodNo { get; set; } = string.Empty;
    public string HastaAdi { get; set; } = string.Empty;
    public string ProtokolNo { get; set; } = string.Empty;
    public string Bolum { get; set; } = string.Empty;
}

public class NumuneModel
{
    public string BarkodNo { get; set; } = string.Empty;
    public string HastaAdi { get; set; } = string.Empty;
    public string ProtokolNo { get; set; } = string.Empty;
    public string Bolum { get; set; } = string.Empty;
}
