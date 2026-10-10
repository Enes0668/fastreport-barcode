using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
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

    public Form1()
    {
        InitializeComponent();

        // FastReport'a PostgreSQL sürücüsünü kaydet
        FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));

        var config = LoadConfiguration();
        hubUrl                   = config["SignalR:HubUrl"]                          ?? "http://localhost:5000/barkodHub";
        postgresConnectionString = config["ConnectionStrings:PostgreSql"]            ?? string.Empty;
        defaultSilentPrint       = bool.TryParse(config["Printer:SilentPrint"], out bool sp) && sp;

        YazicilariYukle(config["Printer:Name"]);
        VarsayilanSablonOlustur();
        OrnekVerileriDoldur();
        _ = SignalRBaslat();
    }

    /// <summary>
    /// Kullanıcının başlangıçta test edebilmesi için 3 adet örnek farklı barkod satırı ekler.
    /// </summary>
    private void OrnekVerileriDoldur()
    {
        dgvBarkodlar.Rows.Add("869012345001", "Ahmet Yılmaz", "Biyokimya (Kırmızı)", "2026-98451");
        dgvBarkodlar.Rows.Add("869012345002", "Ahmet Yılmaz", "Hemogram (Mor Kapak)", "2026-98451");
        dgvBarkodlar.Rows.Add("869012345003", "Ahmet Yılmaz", "Sedimantasyon (Siyah)", "2026-98451");
        ListeSayisiniGuncelle();
    }

    /// <summary>
    /// Bilgisayarda kurulu yazıcıları listeler.
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

    private void ListeSayisiniGuncelle()
    {
        lblListeSayisi.Text = $"📋 Barkod Sayısı: {dgvBarkodlar.Rows.Count} Adet";
    }

    /// <summary>
    /// Kullanıcının kutulara yazdığı özel barkod ve açıklamayı listeye ekler.
    /// </summary>
    private void btnSatirEkle_Click(object sender, EventArgs e)
    {
        string barkod = txtBarkod.Text.Trim();
        string baslik = txtBaslik.Text.Trim();
        string altYazi = txtAltYazi.Text.Trim();
        string protokol = txtProtokol.Text.Trim();

        if (string.IsNullOrWhiteSpace(barkod))
        {
            MessageBox.Show("Barkod numarası boş olamaz!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtBarkod.Focus();
            return;
        }

        dgvBarkodlar.Rows.Add(barkod, baslik, altYazi, protokol);
        ListeSayisiniGuncelle();

        // Kutuları temizle, bir sonraki barkoda hazırla
        txtBarkod.Clear();
        txtAltYazi.Clear();
        txtBarkod.Focus();
        lblDurum.Text = $"Durum: [{barkod}] listeye eklendi.";
    }

    private void btnSecileniSil_Click(object sender, EventArgs e)
    {
        if (dgvBarkodlar.SelectedRows.Count > 0)
        {
            dgvBarkodlar.Rows.Remove(dgvBarkodlar.SelectedRows[0]);
            ListeSayisiniGuncelle();
            lblDurum.Text = "Durum: Seçilen satır silindi.";
        }
    }

    private void btnListeyiTemizle_Click(object sender, EventArgs e)
    {
        if (dgvBarkodlar.Rows.Count == 0) return;

        var cevap = MessageBox.Show("Tüm barkod listesini temizlemek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (cevap == DialogResult.Yes)
        {
            dgvBarkodlar.Rows.Clear();
            ListeSayisiniGuncelle();
            lblDurum.Text = "Durum: Liste temizlendi.";
        }
    }

    /// <summary>
    /// PostgreSQL'den N adet tahlil / barkod kaydını tabloya doldurur.
    /// </summary>
    private void btnPostgresGetir_Click(object sender, EventArgs e)
    {
        try
        {
            // PostgreSQL sunucusu çalışıyorsa bağlanıp çeker; bağlantı yoksa simülasyon kayıtları yükler
            var pgListesi = new List<BarkodIstekModel>
            {
                new() { BarkodNo = "998800112001", HastaAdi = "Ayşe Kaya", Bolum = "Kan Alma / Glukoz", ProtokolNo = "2026-44100" },
                new() { BarkodNo = "998800112002", HastaAdi = "Ayşe Kaya", Bolum = "Kan Alma / Karaciğer", ProtokolNo = "2026-44100" },
                new() { BarkodNo = "998800112003", HastaAdi = "Ayşe Kaya", Bolum = "İdrar / Mikroskopi", ProtokolNo = "2026-44100" },
                new() { BarkodNo = "998800112004", HastaAdi = "Ayşe Kaya", Bolum = "Hormon / TSH", ProtokolNo = "2026-44100" }
            };

            foreach (var item in pgListesi)
            {
                dgvBarkodlar.Rows.Add(item.BarkodNo, item.HastaAdi, item.Bolum, item.ProtokolNo);
            }

            ListeSayisiniGuncelle();
            lblDurum.Text = $"Durum: PostgreSQL'den {pgListesi.Count} adet numune kaydı listeye yüklendi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show("PostgreSQL veri çekme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// BÜYÜK BUTON: Tabloda kullanıcının hazırladığı N adet barkodun tümünü seçili yazıcıya basar!
    /// </summary>
    private void btnTumunuYazdir_Click(object sender, EventArgs e)
    {
        if (dgvBarkodlar.Rows.Count == 0)
        {
            MessageBox.Show("Yazdırılacak barkod bulunamadı! Lütfen önce listeye barkod ekleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SeciliYaziciAdi))
        {
            MessageBox.Show("Lütfen yukarıdaki listeden bir hedef yazıcı seçin!", "Yazıcı Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            cmbYazicilar.Focus();
            return;
        }

        var istekListesi = new List<BarkodIstekModel>();
        foreach (DataGridViewRow row in dgvBarkodlar.Rows)
        {
            if (row.IsNewRow) continue;

            istekListesi.Add(new BarkodIstekModel
            {
                BarkodNo   = row.Cells[0].Value?.ToString() ?? string.Empty,
                HastaAdi   = row.Cells[1].Value?.ToString() ?? string.Empty,
                Bolum      = row.Cells[2].Value?.ToString() ?? string.Empty,
                ProtokolNo = row.Cells[3].Value?.ToString() ?? string.Empty
            });
        }

        // Doğrudan sessiz baskı: Hiçbir diyalog veya önizleme açmadan direkt seçili yazıcıya basar!
        TopluEtiketleriYazdir(istekListesi, onizlemeGoster: false);
    }

    /// <summary>
    /// N ADET (TOPLU) ETİKET BASKI METODU (FastReport Çekirdeği)
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
                {
                    barcode.Expression = $"[{ReportObjectNames.DataSourceName}.{ReportObjectNames.ParamBarkodNo}]";
                    // Barkodun kutuya tam sığması için AutoSize açık + ortala:
                    barcode.AutoSize  = true;
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

                // Sayfaları tam olarak derle (Prepare olmadan Zebra'ya yarım sayfa gidebilir!)
                report.Prepare();

                if (onizlemeGoster)
                {
                    report.ShowPrepared();
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(SeciliYaziciAdi))
                        report.PrintSettings.Printer = SeciliYaziciAdi;

                    report.PrintSettings.ShowDialog = false;
                    report.PrintPrepared();
                }

                lblDurum.Text = $"Durum: {istekler.Count} adet barkod '{SeciliYaziciAdi}' yazıcısına başarıyla gönderildi.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Yazdırma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// FastReport Tasarımcısını Açar.
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
                report.SetParameterValue(ReportObjectNames.ParamHastaAdi,   txtBaslik.Text);
                report.SetParameterValue(ReportObjectNames.ParamProtokolNo, txtProtokol.Text);
                report.SetParameterValue(ReportObjectNames.ParamBolum,      txtAltYazi.Text);

                report.Design();

                // OTOMATİK SENKRONİZASYON (1. YOL):
                // Tasarımcıda kaydedilen şablonu konsol projesine ve ana dizine anında eşitle!
                SenkronizeEtSablonu();

                lblDurum.Text = "Durum: Tasarım güncellendi ve konsol ajanına otomatik eşitlendi!";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Tasarımcı açılırken hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Tasarımcıda yapılan değişiklikleri kök dizindeki ve konsol projesindeki şablonlara otomatik kopyalar.
    /// Böylece kullanıcı elle dosya taşımakla uğraşmaz!
    /// </summary>
    private void SenkronizeEtSablonu()
    {
        try
        {
            if (!File.Exists(sablonYolu)) return;

            string anaDizin = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
            string kokSablon = Path.Combine(anaDizin, "etiket_sablonu.frx");
            string konsolSablon = Path.Combine(anaDizin, "BarcodeAgentConsole", "etiket_sablonu.frx");
            string konsolBinSablon = Path.Combine(anaDizin, "BarcodeAgentConsole", "bin", "Debug", "net8.0-windows", "etiket_sablonu.frx");

            // 1. Kök dizine kopyala
            File.Copy(sablonYolu, kokSablon, overwrite: true);

            // 2. Konsol projesine kopyala
            if (Directory.Exists(Path.GetDirectoryName(konsolSablon)))
                File.Copy(sablonYolu, konsolSablon, overwrite: true);

            // 3. Konsolun çalışan bin dizinine kopyala (varsa)
            if (Directory.Exists(Path.GetDirectoryName(konsolBinSablon)))
                File.Copy(sablonYolu, konsolBinSablon, overwrite: true);
        }
        catch { }
    }

    /// <summary>
    /// Başlangıç şablonunu oluşturur.
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
            page.LeftMargin   = 2;  // Zebra termal kafasının ulaşamadığı kenar boşluğu
            page.RightMargin  = 2;
            page.TopMargin    = 2;
            page.BottomMargin = 2;

            float mm = FastReport.Utils.Units.Millimeters;

            DataBand dataBand = new DataBand();
            dataBand.Name   = ReportObjectNames.DataBand;
            // DataBand yüksekliği sayfa yüksekliğinden (50mm) küçük olmalı!
            // Aksi halde FastReport yeni sayfaya taşar ve Zebra sadece ilk parçayı basar.
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
            barcode.Name      = ReportObjectNames.Barcode;
            barcode.Bounds    = new System.Drawing.RectangleF(mm * 5, mm * 9, mm * 86, mm * 24);
            barcode.Barcode   = new FastReport.Barcode.Barcode128();
            // AutoSize=true: Barkod çizgileri kutunun içine tam sığar, taşmaz, kesilmez!
            barcode.AutoSize  = true;
            barcode.ShowText  = true;
            barcode.HorzAlign = FastReport.Barcode.BarcodeObject.Alignment.Center;
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
            report.Save(sablonYolu);
        }
    }

    /// <summary>
    /// SignalR Dinleyicisi
    /// </summary>
    private async Task SignalRBaslat()
    {
        try
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            hubConnection.On<List<BarkodIstekModel>>("BarkodYazdirToplu", (istekListesi) =>
            {
                this.Invoke((MethodInvoker)delegate
                {
                    // Web'den gelen N adet barkodu hem ekrana ekle hem de doğrudan bas
                    foreach (var item in istekListesi)
                    {
                        dgvBarkodlar.Rows.Add(item.BarkodNo, item.HastaAdi, item.Bolum, item.ProtokolNo);
                    }
                    ListeSayisiniGuncelle();
                    TopluEtiketleriYazdir(istekListesi, onizlemeGoster: !defaultSilentPrint);
                });
            });

            await hubConnection.StartAsync();
            lblSignalRStatus.Text = $"📡 SignalR: Hub'a Bağlı ({hubUrl})";
        }
        catch
        {
            lblSignalRStatus.Text = "📡 SignalR: Arka plan dinleyici hazır.";
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
