using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BarcodeAgentConsole.Services;
using FastReport;

namespace BarcodeAgentConsole;

/// <summary>
/// Uygulama açıldığında ekrana gelen; personelin veya mühendisin şablon seçip
/// FastReport Tasarımcısını açmasını, yeni şablon eklemesini ve yazıcı eşleştirmelerini
/// yönetmesini sağlayan modern Windows Form arayüzü.
/// </summary>
public class AnaForm : Form
{
    private readonly IYaziciServisi _yaziciServisi;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;

    private ComboBox cmbSablonlar = null!;
    private ComboBox cmbYazicilar = null!;
    private Button btnTasarla = null!;
    private Button btnOnizle = null!;
    private Button btnYeniSablon = null!;
    private Button btnTestBaski = null!;
    private CheckBox chkOnizle = null!;
    private Label lblDurum = null!;

    private NotifyIcon _notifyIcon = null!;
    private bool _gercektenCik = false;
    public bool BaslangictaGizle { get; set; } = false;

    public AnaForm(IYaziciServisi yaziciServisi, IYaziciYoneticisi yaziciYoneticisi)
    {
        _yaziciServisi = yaziciServisi;
        _yaziciYoneticisi = yaziciYoneticisi;

        ArayuzuHazirla();
        VerileriYukle();
    }

    private void ArayuzuHazirla()
    {
        Text = "FastReport Barkod & Şablon Yönetim Paneli";
        Size = new Size(600, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Başlık
        var lblBaslik = new Label
        {
            Text = "🏥 Barkod & A4 Şablon Tasarım Merkezi",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(56, 189, 248),
            Location = new Point(20, 18),
            AutoSize = true
        };
        Controls.Add(lblBaslik);

        var lblAltBaslik = new Label
        {
            Text = "Şablonları tasarlayabilir, ekranda önizleyebilir veya yazdırma testi yapabilirsiniz.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(22, 48),
            AutoSize = true
        };
        Controls.Add(lblAltBaslik);

        // Şablon Seçimi Grubu
        var lblSablon = new Label { Text = "📋 Düzenlenecek / Basılacak Şablon:", Location = new Point(22, 85), AutoSize = true };
        Controls.Add(lblSablon);

        cmbSablonlar = new ComboBox
        {
            Location = new Point(24, 110),
            Width = 380,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        cmbSablonlar.SelectedIndexChanged += (s, e) => GuncelleYaziciOnerisi();
        Controls.Add(cmbSablonlar);

        btnYeniSablon = new Button
        {
            Text = "➕ Yeni Ekle",
            Location = new Point(415, 108),
            Width = 145,
            Height = 30,
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnYeniSablon.Click += BtnYeniSablon_Click;
        Controls.Add(btnYeniSablon);

        // Hedef Yazıcı Grubu
        var lblYazici = new Label { Text = "🖨️ Hedef Yazıcı (Zebra Barkod veya Brother A4):", Location = new Point(22, 150), AutoSize = true };
        Controls.Add(lblYazici);

        cmbYazicilar = new ComboBox
        {
            Location = new Point(24, 175),
            Width = 536,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(cmbYazicilar);

        // Önizleme Seçeneği Kutucuğu
        chkOnizle = new CheckBox
        {
            Text = "🔍 Test basarken önce ekranda önizle (FastReport Preview)",
            Location = new Point(24, 215),
            AutoSize = true,
            ForeColor = Color.FromArgb(147, 197, 253),
            Cursor = Cursors.Hand
        };
        Controls.Add(chkOnizle);

        // Butonlar
        btnTasarla = new Button
        {
            Text = "🎨 Şablonu Tasarla",
            Location = new Point(24, 250),
            Width = 210,
            Height = 44,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnTasarla.Click += BtnTasarla_Click;
        Controls.Add(btnTasarla);

        btnOnizle = new Button
        {
            Text = "🔍 Önizle",
            Location = new Point(244, 250),
            Width = 150,
            Height = 44,
            BackColor = Color.FromArgb(99, 102, 241),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnOnizle.Click += BtnOnizle_Click;
        Controls.Add(btnOnizle);

        btnTestBaski = new Button
        {
            Text = "⚡ Test Bas",
            Location = new Point(404, 250),
            Width = 156,
            Height = 44,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnTestBaski.Click += BtnTestBaski_Click;
        Controls.Add(btnTestBaski);

        // Durum Çubuğu
        lblDurum = new Label
        {
            Text = "🟢 Ajan Aktif: Port 5055 (TCP) ve Port 5000 (SignalR Hub) arka planda dinliyor.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(52, 211, 153),
            Location = new Point(22, 325),
            AutoSize = true
        };
        Controls.Add(lblDurum);

        // Windows Bildirim Alanı (System Tray) Simge ve Menüsü
        BildirimAlaniniHazirla();
    }

    private void VerileriYukle()
    {
        // 1. Şablonları Doldur
        cmbSablonlar.Items.Clear();
        foreach (var s in _yaziciServisi.MevcutSablonlariGetir())
        {
            cmbSablonlar.Items.Add(s);
        }
        if (cmbSablonlar.Items.Count > 0) cmbSablonlar.SelectedIndex = 0;

        // 2. Yazıcıları Doldur
        cmbYazicilar.Items.Clear();
        foreach (var y in _yaziciYoneticisi.YukluYazicilariGetir())
        {
            cmbYazicilar.Items.Add(y);
        }
        if (cmbYazicilar.Items.Count > 0) cmbYazicilar.SelectedIndex = 0;
    }

    private void GuncelleYaziciOnerisi()
    {
        string seciliSablon = cmbSablonlar.SelectedItem?.ToString() ?? "";
        bool a4Mu = seciliSablon.Contains("a4", StringComparison.OrdinalIgnoreCase) ||
                    seciliSablon.Contains("dosya", StringComparison.OrdinalIgnoreCase) ||
                    seciliSablon.Contains("rapor", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < cmbYazicilar.Items.Count; i++)
        {
            string yazici = cmbYazicilar.Items[i]?.ToString() ?? "";
            if (a4Mu && (yazici.Contains("brother", StringComparison.OrdinalIgnoreCase) ||
                         yazici.Contains("laser", StringComparison.OrdinalIgnoreCase) ||
                         yazici.Contains("pdf", StringComparison.OrdinalIgnoreCase)))
            {
                cmbYazicilar.SelectedIndex = i;
                break;
            }
            else if (!a4Mu && (yazici.Contains("zebra", StringComparison.OrdinalIgnoreCase) ||
                              yazici.Contains("zd", StringComparison.OrdinalIgnoreCase) ||
                              yazici.Contains("barkod", StringComparison.OrdinalIgnoreCase)))
            {
                cmbYazicilar.SelectedIndex = i;
                break;
            }
        }
    }

    private void BtnTasarla_Click(object? sender, EventArgs e)
    {
        string? secili = cmbSablonlar.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(secili)) return;

        string tamYol = Path.Combine(_yaziciServisi.SablonlarKlasoru, secili);
        if (!File.Exists(tamYol))
        {
            MessageBox.Show($"Şablon dosyası bulunamadı: {tamYol}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            using var report = new Report();
            report.Load(tamYol);
            report.Design();

            // Tasarımcı kapandıktan sonra dosya tarihini güncelle ki ağdaki diğer bilgisayarlar otomatik çeksin!
            File.SetLastWriteTimeUtc(tamYol, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"FastReport Tasarımcısı açılırken hata:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnYeniSablon_Click(object? sender, EventArgs e)
    {
        using var prompt = new Form
        {
            Width = 420,
            Height = 210,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Yeni Barkod / A4 Şablonu Ekle",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White
        };

        var lbl = new Label { Left = 20, Top = 15, Text = "Şablon Adı (Örn: serum_etiketi veya recete_a4):", AutoSize = true };
        var textBox = new TextBox { Left = 20, Top = 40, Width = 360, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
        var chkA4 = new CheckBox { Left = 20, Top = 75, Text = "Bu bir A4 Normal Sayfa Şablonudur (İşaretsizse Termal Barkod)", AutoSize = true };
        var confirmation = new Button { Text = "Oluştur", Left = 260, Width = 120, Top = 115, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

        prompt.Controls.Add(lbl);
        prompt.Controls.Add(textBox);
        prompt.Controls.Add(chkA4);
        prompt.Controls.Add(confirmation);
        prompt.AcceptButton = confirmation;

        if (prompt.ShowDialog(this) == DialogResult.OK)
        {
            string ad = textBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ad))
            {
                bool basarili = _yaziciServisi.YeniSablonOlustur(ad, chkA4.Checked);
                if (basarili)
                {
                    VerileriYukle();
                    cmbSablonlar.SelectedItem = ad.EndsWith(".frx") ? ad : ad + ".frx";
                    BtnTasarla_Click(sender, e);
                }
            }
        }
    }

    private void BtnOnizle_Click(object? sender, EventArgs e)
    {
        string? sablon = cmbSablonlar.SelectedItem?.ToString();
        string? yazici = cmbYazicilar.SelectedItem?.ToString();

        var testParametreler = new System.Collections.Generic.Dictionary<string, string>
        {
            { "HastaAdi", "Ahmet Yılmaz (Önizleme)" },
            { "BarkodNo", "869012345" },
            { "ProtokolNo", "P-98765" },
            { "Bolum", "Acil Servis / Laboratuvar" },
            { "Tarih", DateTime.Now.ToString("dd.MM.yyyy HH:mm") }
        };

        var sonuc = _yaziciServisi.RaporOnizle("TEST-ONIZLE", yazici, testParametreler, sablon);
        if (!sonuc.Basarili)
        {
            MessageBox.Show(sonuc.Mesaj, "Önizleme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BtnTestBaski_Click(object? sender, EventArgs e)
    {
        string? sablon = cmbSablonlar.SelectedItem?.ToString();
        string? yazici = cmbYazicilar.SelectedItem?.ToString();

        var testParametreler = new System.Collections.Generic.Dictionary<string, string>
        {
            { "HastaAdi", "Test Hasta (Panelden)" },
            { "BarkodNo", "869099901" },
            { "ProtokolNo", "P-TEST" },
            { "Bolum", "Panel Test" },
            { "Tarih", DateTime.Now.ToString("dd.MM.yyyy HH:mm") }
        };

        if (chkOnizle.Checked)
        {
            var sonucOnizle = _yaziciServisi.RaporOnizle("TEST-999", yazici, testParametreler, sablon);
            if (!sonucOnizle.Basarili)
            {
                MessageBox.Show(sonucOnizle.Mesaj, "Önizleme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        var sonuc = _yaziciServisi.BarkodBas("TEST-999", yazici, testParametreler, sablon);

        if (sonuc.Basarili)
        {
            MessageBox.Show(sonuc.Mesaj, "Baskı Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(sonuc.Mesaj, "Baskı Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BildirimAlaniniHazirla()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "FastReport Barkod & Şablon Ajanı",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        var itemGoster = new ToolStripMenuItem("🏥 Paneli Aç", null, (s, e) => FormuGosterVeOdakla());
        itemGoster.Font = new Font(itemGoster.Font, FontStyle.Bold);

        var itemCikis = new ToolStripMenuItem("❌ Tamamen Kapat (Çıkış)", null, (s, e) =>
        {
            _gercektenCik = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Environment.Exit(0);
        });

        menu.Items.Add(itemGoster);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itemCikis);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (s, e) => FormuGosterVeOdakla();
    }

    public void FormuGosterVeOdakla()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(FormuGosterVeOdakla));
            return;
        }

        BaslangictaGizle = false;
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        BringToFront();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (BaslangictaGizle && !IsHandleCreated)
        {
            CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_gercektenCik && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(1500, "Barkod Ajanı", "Ajan arka planda çalışmaya devam ediyor. Açmak için saatin yanındaki simgeye tıklayabilirsiniz.", ToolTipIcon.Info);
        }
        else
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.OnFormClosing(e);
        }
    }
}
