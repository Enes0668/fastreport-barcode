namespace FastReportBarcodeApp;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Label lblSeciliYazici;
    private System.Windows.Forms.ComboBox cmbYazicilar;
    private System.Windows.Forms.Button btnTasarla;
    private System.Windows.Forms.Button btnYazdirTekli;
    private System.Windows.Forms.Button btnYazdirToplu;
    private System.Windows.Forms.Button btnSignalRSimule;
    private System.Windows.Forms.TextBox txtBarkod;
    private System.Windows.Forms.TextBox txtHastaAdi;
    private System.Windows.Forms.TextBox txtProtokolNo;
    private System.Windows.Forms.TextBox txtBolum;
    private System.Windows.Forms.Label lblBarkod;
    private System.Windows.Forms.Label lblHastaAdi;
    private System.Windows.Forms.Label lblProtokolNo;
    private System.Windows.Forms.Label lblBolum;
    private System.Windows.Forms.GroupBox grpTekli;
    private System.Windows.Forms.GroupBox grpSignalR;
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
        btnYazdirTekli = new Button();
        btnYazdirToplu = new Button();
        btnSignalRSimule = new Button();
        txtBarkod = new TextBox();
        txtHastaAdi = new TextBox();
        txtProtokolNo = new TextBox();
        txtBolum = new TextBox();
        lblBarkod = new Label();
        lblHastaAdi = new Label();
        lblProtokolNo = new Label();
        lblBolum = new Label();
        grpTekli = new GroupBox();
        grpSignalR = new GroupBox();
        lblDurum = new Label();
        lblSignalRStatus = new Label();
        grpTekli.SuspendLayout();
        grpSignalR.SuspendLayout();
        SuspendLayout();
        // 
        // lblSeciliYazici
        // 
        lblSeciliYazici.AutoSize = true;
        lblSeciliYazici.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblSeciliYazici.Location = new Point(30, 15);
        lblSeciliYazici.Name = "lblSeciliYazici";
        lblSeciliYazici.Size = new Size(160, 20);
        lblSeciliYazici.TabIndex = 0;
        lblSeciliYazici.Text = "🖨️ Hedef Yazıcı Seçin:";
        // 
        // cmbYazicilar
        // 
        cmbYazicilar.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbYazicilar.Font = new Font("Segoe UI", 9.5F);
        cmbYazicilar.FormattingEnabled = true;
        cmbYazicilar.Location = new Point(30, 38);
        cmbYazicilar.Name = "cmbYazicilar";
        cmbYazicilar.Size = new Size(440, 29);
        cmbYazicilar.TabIndex = 1;
        // 
        // btnTasarla
        // 
        btnTasarla.BackColor = Color.LightSkyBlue;
        btnTasarla.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnTasarla.Location = new Point(30, 80);
        btnTasarla.Name = "btnTasarla";
        btnTasarla.Size = new Size(440, 42);
        btnTasarla.TabIndex = 2;
        btnTasarla.Text = "🎨 Sürükle-Bırak Etiket Tasarımcısını Aç";
        btnTasarla.UseVisualStyleBackColor = false;
        btnTasarla.Click += btnTasarla_Click;
        // 
        // grpTekli
        // 
        grpTekli.Controls.Add(lblBarkod);
        grpTekli.Controls.Add(txtBarkod);
        grpTekli.Controls.Add(lblHastaAdi);
        grpTekli.Controls.Add(txtHastaAdi);
        grpTekli.Controls.Add(lblProtokolNo);
        grpTekli.Controls.Add(txtProtokolNo);
        grpTekli.Controls.Add(lblBolum);
        grpTekli.Controls.Add(txtBolum);
        grpTekli.Controls.Add(btnYazdirTekli);
        grpTekli.Location = new Point(30, 135);
        grpTekli.Name = "grpTekli";
        grpTekli.Size = new Size(440, 225);
        grpTekli.TabIndex = 3;
        grpTekli.TabStop = false;
        grpTekli.Text = "Manuel Test Alanı (Tekli Hasta Etiketi)";
        // 
        // lblBarkod
        // 
        lblBarkod.AutoSize = true;
        lblBarkod.Location = new Point(20, 30);
        lblBarkod.Name = "lblBarkod";
        lblBarkod.Size = new Size(83, 20);
        lblBarkod.TabIndex = 0;
        lblBarkod.Text = "Barkod No:";
        // 
        // txtBarkod
        // 
        txtBarkod.Location = new Point(130, 27);
        txtBarkod.Name = "txtBarkod";
        txtBarkod.Size = new Size(280, 27);
        txtBarkod.TabIndex = 1;
        txtBarkod.Text = string.Empty;
        // 
        // lblHastaAdi
        // 
        lblHastaAdi.AutoSize = true;
        lblHastaAdi.Location = new Point(20, 66);
        lblHastaAdi.Name = "lblHastaAdi";
        lblHastaAdi.Size = new Size(76, 20);
        lblHastaAdi.TabIndex = 2;
        lblHastaAdi.Text = "Hasta Adı:";
        // 
        // txtHastaAdi
        // 
        txtHastaAdi.Location = new Point(130, 63);
        txtHastaAdi.Name = "txtHastaAdi";
        txtHastaAdi.Size = new Size(280, 27);
        txtHastaAdi.TabIndex = 3;
        txtHastaAdi.Text = string.Empty;
        // 
        // lblProtokolNo
        // 
        lblProtokolNo.AutoSize = true;
        lblProtokolNo.Location = new Point(20, 102);
        lblProtokolNo.Name = "lblProtokolNo";
        lblProtokolNo.Size = new Size(91, 20);
        lblProtokolNo.TabIndex = 4;
        lblProtokolNo.Text = "Protokol No:";
        // 
        // txtProtokolNo
        // 
        txtProtokolNo.Location = new Point(130, 99);
        txtProtokolNo.Name = "txtProtokolNo";
        txtProtokolNo.Size = new Size(280, 27);
        txtProtokolNo.TabIndex = 5;
        txtProtokolNo.Text = string.Empty;
        // 
        // lblBolum
        // 
        lblBolum.AutoSize = true;
        lblBolum.Location = new Point(20, 138);
        lblBolum.Name = "lblBolum";
        lblBolum.Size = new Size(55, 20);
        lblBolum.TabIndex = 6;
        lblBolum.Text = "Bölüm:";
        // 
        // txtBolum
        // 
        txtBolum.Location = new Point(130, 135);
        txtBolum.Name = "txtBolum";
        txtBolum.Size = new Size(280, 27);
        txtBolum.TabIndex = 7;
        txtBolum.Text = string.Empty;
        // 
        // btnYazdirTekli
        // 
        btnYazdirTekli.BackColor = Color.LightGreen;
        btnYazdirTekli.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnYazdirTekli.Location = new Point(130, 172);
        btnYazdirTekli.Name = "btnYazdirTekli";
        btnYazdirTekli.Size = new Size(280, 38);
        btnYazdirTekli.TabIndex = 8;
        btnYazdirTekli.Text = "🖨️ Manuel Önizle / Yazdır";
        btnYazdirTekli.UseVisualStyleBackColor = false;
        btnYazdirTekli.Click += btnYazdirTekli_Click;
        // 
        // grpSignalR
        // 
        grpSignalR.Controls.Add(lblSignalRStatus);
        grpSignalR.Controls.Add(btnSignalRSimule);
        grpSignalR.Location = new Point(30, 370);
        grpSignalR.Name = "grpSignalR";
        grpSignalR.Size = new Size(440, 110);
        grpSignalR.TabIndex = 4;
        grpSignalR.TabStop = false;
        grpSignalR.Text = "🌐 Web & SignalR Entegrasyon Modu";
        // 
        // lblSignalRStatus
        // 
        lblSignalRStatus.AutoSize = true;
        lblSignalRStatus.ForeColor = Color.DarkGreen;
        lblSignalRStatus.Location = new Point(20, 28);
        lblSignalRStatus.Name = "lblSignalRStatus";
        lblSignalRStatus.Size = new Size(320, 20);
        lblSignalRStatus.TabIndex = 0;
        lblSignalRStatus.Text = "📡 Durum: SignalR dinleme hazır (Web emri bekleniyor)";
        // 
        // btnSignalRSimule
        // 
        btnSignalRSimule.BackColor = Color.Khaki;
        btnSignalRSimule.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnSignalRSimule.Location = new Point(20, 56);
        btnSignalRSimule.Name = "btnSignalRSimule";
        btnSignalRSimule.Size = new Size(400, 42);
        btnSignalRSimule.TabIndex = 1;
        btnSignalRSimule.Text = "⚡ Web'den N Adet Barkod Emri Simüle Et";
        btnSignalRSimule.UseVisualStyleBackColor = false;
        btnSignalRSimule.Click += btnSignalRSimule_Click;
        // 
        // btnYazdirToplu
        // 
        btnYazdirToplu.BackColor = Color.WhiteSmoke;
        btnYazdirToplu.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        btnYazdirToplu.Location = new Point(30, 490);
        btnYazdirToplu.Name = "btnYazdirToplu";
        btnYazdirToplu.Size = new Size(440, 35);
        btnYazdirToplu.TabIndex = 5;
        btnYazdirToplu.Text = "📦 Toplu Etiket Bas (Test Listesi)";
        btnYazdirToplu.UseVisualStyleBackColor = false;
        btnYazdirToplu.Click += btnYazdirToplu_Click;
        // 
        // lblDurum
        // 
        lblDurum.AutoSize = true;
        lblDurum.ForeColor = Color.DarkSlateGray;
        lblDurum.Location = new Point(30, 535);
        lblDurum.Name = "lblDurum";
        lblDurum.Size = new Size(271, 20);
        lblDurum.TabIndex = 6;
        lblDurum.Text = "Durum: Hazır. Şablon: etiket_sablonu.frx";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(500, 570);
        Controls.Add(lblDurum);
        Controls.Add(btnYazdirToplu);
        Controls.Add(grpSignalR);
        Controls.Add(grpTekli);
        Controls.Add(btnTasarla);
        Controls.Add(cmbYazicilar);
        Controls.Add(lblSeciliYazici);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "HBYS Barkod Ajanı & Baskı Sistemi";
        grpTekli.ResumeLayout(false);
        grpTekli.PerformLayout();
        grpSignalR.ResumeLayout(false);
        grpSignalR.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
