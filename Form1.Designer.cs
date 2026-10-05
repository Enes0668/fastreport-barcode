namespace FastReportBarcodeApp;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Label lblSeciliYazici;
    private System.Windows.Forms.ComboBox cmbYazicilar;
    private System.Windows.Forms.Button btnTasarla;
    private System.Windows.Forms.GroupBox grpHizliEkle;
    private System.Windows.Forms.TextBox txtBarkod;
    private System.Windows.Forms.TextBox txtBaslik;
    private System.Windows.Forms.TextBox txtAltYazi;
    private System.Windows.Forms.TextBox txtProtokol;
    private System.Windows.Forms.Label lblBarkod;
    private System.Windows.Forms.Label lblBaslik;
    private System.Windows.Forms.Label lblAltYazi;
    private System.Windows.Forms.Label lblProtokol;
    private System.Windows.Forms.Button btnSatirEkle;
    private System.Windows.Forms.Button btnPostgresGetir;
    private System.Windows.Forms.Button btnSecileniSil;
    private System.Windows.Forms.Button btnListeyiTemizle;
    private System.Windows.Forms.DataGridView dgvBarkodlar;
    private System.Windows.Forms.DataGridViewTextBoxColumn colBarkod;
    private System.Windows.Forms.DataGridViewTextBoxColumn colBaslik;
    private System.Windows.Forms.DataGridViewTextBoxColumn colAltYazi;
    private System.Windows.Forms.DataGridViewTextBoxColumn colProtokol;
    private System.Windows.Forms.Button btnTumunuYazdir;
    private System.Windows.Forms.Label lblListeSayisi;
    private System.Windows.Forms.Label lblDurum;
    private System.Windows.Forms.Label lblSignalRStatus;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblSeciliYazici = new Label();
        cmbYazicilar = new ComboBox();
        btnTasarla = new Button();
        grpHizliEkle = new GroupBox();
        lblBarkod = new Label();
        txtBarkod = new TextBox();
        lblBaslik = new Label();
        txtBaslik = new TextBox();
        lblAltYazi = new Label();
        txtAltYazi = new TextBox();
        lblProtokol = new Label();
        txtProtokol = new TextBox();
        btnSatirEkle = new Button();
        btnPostgresGetir = new Button();
        dgvBarkodlar = new DataGridView();
        colBarkod = new DataGridViewTextBoxColumn();
        colBaslik = new DataGridViewTextBoxColumn();
        colAltYazi = new DataGridViewTextBoxColumn();
        colProtokol = new DataGridViewTextBoxColumn();
        btnSecileniSil = new Button();
        btnListeyiTemizle = new Button();
        btnTumunuYazdir = new Button();
        lblListeSayisi = new Label();
        lblDurum = new Label();
        lblSignalRStatus = new Label();
        grpHizliEkle.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvBarkodlar).BeginInit();
        SuspendLayout();
        // 
        // lblSeciliYazici
        // 
        lblSeciliYazici.AutoSize = true;
        lblSeciliYazici.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblSeciliYazici.Location = new Point(20, 15);
        lblSeciliYazici.Name = "lblSeciliYazici";
        lblSeciliYazici.Size = new Size(160, 21);
        lblSeciliYazici.TabIndex = 0;
        lblSeciliYazici.Text = "🖨️ Hedef Barkod Yazıcısı:";
        // 
        // cmbYazicilar
        // 
        cmbYazicilar.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbYazicilar.Font = new Font("Segoe UI", 10F);
        cmbYazicilar.FormattingEnabled = true;
        cmbYazicilar.Location = new Point(190, 12);
        cmbYazicilar.Name = "cmbYazicilar";
        cmbYazicilar.Size = new Size(420, 31);
        cmbYazicilar.TabIndex = 1;
        // 
        // btnTasarla
        // 
        btnTasarla.BackColor = Color.LightSkyBlue;
        btnTasarla.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnTasarla.Location = new Point(625, 10);
        btnTasarla.Name = "btnTasarla";
        btnTasarla.Size = new Size(195, 35);
        btnTasarla.TabIndex = 2;
        btnTasarla.Text = "🎨 Şablon Tasarla (.frx)";
        btnTasarla.UseVisualStyleBackColor = false;
        btnTasarla.Click += btnTasarla_Click;
        // 
        // grpHizliEkle
        // 
        grpHizliEkle.Controls.Add(lblBarkod);
        grpHizliEkle.Controls.Add(txtBarkod);
        grpHizliEkle.Controls.Add(lblBaslik);
        grpHizliEkle.Controls.Add(txtBaslik);
        grpHizliEkle.Controls.Add(lblAltYazi);
        grpHizliEkle.Controls.Add(txtAltYazi);
        grpHizliEkle.Controls.Add(lblProtokol);
        grpHizliEkle.Controls.Add(txtProtokol);
        grpHizliEkle.Controls.Add(btnSatirEkle);
        grpHizliEkle.Controls.Add(btnPostgresGetir);
        grpHizliEkle.Location = new Point(20, 55);
        grpHizliEkle.Name = "grpHizliEkle";
        grpHizliEkle.Size = new Size(800, 115);
        grpHizliEkle.TabIndex = 3;
        grpHizliEkle.TabStop = false;
        grpHizliEkle.Text = "Yeni Barkod Yazısı / Satırı Tanımla (N Adet Ekleyebilirsiniz)";
        // 
        // lblBarkod
        // 
        lblBarkod.AutoSize = true;
        lblBarkod.Location = new Point(15, 25);
        lblBarkod.Name = "lblBarkod";
        lblBarkod.Size = new Size(78, 20);
        lblBarkod.TabIndex = 0;
        lblBarkod.Text = "Barkod No:";
        // 
        // txtBarkod
        // 
        txtBarkod.Location = new Point(15, 48);
        txtBarkod.Name = "txtBarkod";
        txtBarkod.Size = new Size(130, 27);
        txtBarkod.TabIndex = 1;
        // 
        // lblBaslik
        // 
        lblBaslik.AutoSize = true;
        lblBaslik.Location = new Point(155, 25);
        lblBaslik.Name = "lblBaslik";
        lblBaslik.Size = new Size(130, 20);
        lblBaslik.TabIndex = 2;
        lblBaslik.Text = "Üst Yazı / Hasta Adı:";
        // 
        // txtBaslik
        // 
        txtBaslik.Location = new Point(155, 48);
        txtBaslik.Name = "txtBaslik";
        txtBaslik.Size = new Size(180, 27);
        txtBaslik.TabIndex = 3;
        // 
        // lblAltYazi
        // 
        lblAltYazi.AutoSize = true;
        lblAltYazi.Location = new Point(345, 25);
        lblAltYazi.Name = "lblAltYazi";
        lblAltYazi.Size = new Size(140, 20);
        lblAltYazi.TabIndex = 4;
        lblAltYazi.Text = "Alt Yazı / Tüp - Bölüm:";
        // 
        // txtAltYazi
        // 
        txtAltYazi.Location = new Point(345, 48);
        txtAltYazi.Name = "txtAltYazi";
        txtAltYazi.Size = new Size(180, 27);
        txtAltYazi.TabIndex = 5;
        // 
        // lblProtokol
        // 
        lblProtokol.AutoSize = true;
        lblProtokol.Location = new Point(535, 25);
        lblProtokol.Name = "lblProtokol";
        lblProtokol.Size = new Size(91, 20);
        lblProtokol.TabIndex = 6;
        lblProtokol.Text = "Protokol No:";
        // 
        // txtProtokol
        // 
        txtProtokol.Location = new Point(535, 48);
        txtProtokol.Name = "txtProtokol";
        txtProtokol.Size = new Size(110, 27);
        txtProtokol.TabIndex = 7;
        // 
        // btnSatirEkle
        // 
        btnSatirEkle.BackColor = Color.LightGreen;
        btnSatirEkle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnSatirEkle.Location = new Point(655, 30);
        btnSatirEkle.Name = "btnSatirEkle";
        btnSatirEkle.Size = new Size(135, 35);
        btnSatirEkle.TabIndex = 8;
        btnSatirEkle.Text = "➕ Listeye Ekle";
        btnSatirEkle.UseVisualStyleBackColor = false;
        btnSatirEkle.Click += btnSatirEkle_Click;
        // 
        // btnPostgresGetir
        // 
        btnPostgresGetir.BackColor = Color.LightCyan;
        btnPostgresGetir.Font = new Font("Segoe UI", 8.5F);
        btnPostgresGetir.Location = new Point(655, 70);
        btnPostgresGetir.Name = "btnPostgresGetir";
        btnPostgresGetir.Size = new Size(135, 32);
        btnPostgresGetir.TabIndex = 9;
        btnPostgresGetir.Text = "🐘 DB'den Getir";
        btnPostgresGetir.UseVisualStyleBackColor = false;
        btnPostgresGetir.Click += btnPostgresGetir_Click;
        // 
        // dgvBarkodlar
        // 
        dgvBarkodlar.AllowUserToAddRows = false;
        dgvBarkodlar.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvBarkodlar.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvBarkodlar.Columns.AddRange(new DataGridViewColumn[] { colBarkod, colBaslik, colAltYazi, colProtokol });
        dgvBarkodlar.Location = new Point(20, 180);
        dgvBarkodlar.MultiSelect = false;
        dgvBarkodlar.Name = "dgvBarkodlar";
        dgvBarkodlar.RowHeadersWidth = 35;
        dgvBarkodlar.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvBarkodlar.Size = new Size(800, 260);
        dgvBarkodlar.TabIndex = 4;
        // 
        // colBarkod
        // 
        colBarkod.HeaderText = "Barkod No";
        colBarkod.Name = "colBarkod";
        // 
        // colBaslik
        // 
        colBaslik.HeaderText = "Üst Yazı / Hasta Adı";
        colBaslik.Name = "colBaslik";
        // 
        // colAltYazi
        // 
        colAltYazi.HeaderText = "Alt Yazı / Açıklama";
        colAltYazi.Name = "colAltYazi";
        // 
        // colProtokol
        // 
        colProtokol.HeaderText = "Protokol No";
        colProtokol.Name = "colProtokol";
        // 
        // btnSecileniSil
        // 
        btnSecileniSil.BackColor = Color.MistyRose;
        btnSecileniSil.Location = new Point(20, 448);
        btnSecileniSil.Name = "btnSecileniSil";
        btnSecileniSil.Size = new Size(120, 32);
        btnSecileniSil.TabIndex = 5;
        btnSecileniSil.Text = "🗑️ Seçileni Sil";
        btnSecileniSil.UseVisualStyleBackColor = false;
        btnSecileniSil.Click += btnSecileniSil_Click;
        // 
        // btnListeyiTemizle
        // 
        btnListeyiTemizle.BackColor = Color.WhiteSmoke;
        btnListeyiTemizle.Location = new Point(148, 448);
        btnListeyiTemizle.Name = "btnListeyiTemizle";
        btnListeyiTemizle.Size = new Size(120, 32);
        btnListeyiTemizle.TabIndex = 6;
        btnListeyiTemizle.Text = "🧹 Tümünü Temizle";
        btnListeyiTemizle.UseVisualStyleBackColor = false;
        btnListeyiTemizle.Click += btnListeyiTemizle_Click;
        // 
        // lblListeSayisi
        // 
        lblListeSayisi.AutoSize = true;
        lblListeSayisi.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblListeSayisi.ForeColor = Color.Navy;
        lblListeSayisi.Location = new Point(290, 453);
        lblListeSayisi.Name = "lblListeSayisi";
        lblListeSayisi.Size = new Size(187, 21);
        lblListeSayisi.TabIndex = 7;
        lblListeSayisi.Text = "📋 Barkod Sayısı: 0 Adet";
        // 
        // btnTumunuYazdir
        // 
        btnTumunuYazdir.BackColor = Color.MediumSeaGreen;
        btnTumunuYazdir.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnTumunuYazdir.ForeColor = Color.White;
        btnTumunuYazdir.Location = new Point(530, 445);
        btnTumunuYazdir.Name = "btnTumunuYazdir";
        btnTumunuYazdir.Size = new Size(290, 42);
        btnTumunuYazdir.TabIndex = 8;
        btnTumunuYazdir.Text = "🖨️ Listedeki N Adet Barkodu Yazdır";
        btnTumunuYazdir.UseVisualStyleBackColor = false;
        btnTumunuYazdir.Click += btnTumunuYazdir_Click;
        // 
        // lblDurum
        // 
        lblDurum.AutoSize = true;
        lblDurum.ForeColor = Color.DarkSlateGray;
        lblDurum.Location = new Point(20, 495);
        lblDurum.Name = "lblDurum";
        lblDurum.Size = new Size(95, 20);
        lblDurum.TabIndex = 9;
        lblDurum.Text = "Durum: Hazır.";
        // 
        // lblSignalRStatus
        // 
        lblSignalRStatus.AutoSize = true;
        lblSignalRStatus.ForeColor = Color.DarkGreen;
        lblSignalRStatus.Location = new Point(480, 495);
        lblSignalRStatus.Name = "lblSignalRStatus";
        lblSignalRStatus.Size = new Size(251, 20);
        lblSignalRStatus.TabIndex = 10;
        lblSignalRStatus.Text = "📡 Arka Plan Dinleyici: SignalR Aktif";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(840, 525);
        Controls.Add(lblSignalRStatus);
        Controls.Add(lblDurum);
        Controls.Add(btnTumunuYazdir);
        Controls.Add(lblListeSayisi);
        Controls.Add(btnListeyiTemizle);
        Controls.Add(btnSecileniSil);
        Controls.Add(dgvBarkodlar);
        Controls.Add(grpHizliEkle);
        Controls.Add(btnTasarla);
        Controls.Add(cmbYazicilar);
        Controls.Add(lblSeciliYazici);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "FastReport Çoklu Barkod Masası & Baskı Sistemi";
        grpHizliEkle.ResumeLayout(false);
        grpHizliEkle.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)dgvBarkodlar).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
