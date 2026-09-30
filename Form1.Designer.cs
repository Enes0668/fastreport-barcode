namespace FastReportBarcodeApp;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Button btnTasarla;
    private System.Windows.Forms.Button btnYazdirTekli;
    private System.Windows.Forms.Button btnYazdirToplu;
    private System.Windows.Forms.TextBox txtBarkod;
    private System.Windows.Forms.TextBox txtUrunAdi;
    private System.Windows.Forms.TextBox txtFiyat;
    private System.Windows.Forms.Label lblBarkod;
    private System.Windows.Forms.Label lblUrunAdi;
    private System.Windows.Forms.Label lblFiyat;
    private System.Windows.Forms.GroupBox grpTekli;
    private System.Windows.Forms.Label lblDurum;

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
        btnTasarla = new Button();
        btnYazdirTekli = new Button();
        btnYazdirToplu = new Button();
        txtBarkod = new TextBox();
        txtUrunAdi = new TextBox();
        txtFiyat = new TextBox();
        lblBarkod = new Label();
        lblUrunAdi = new Label();
        lblFiyat = new Label();
        grpTekli = new GroupBox();
        lblDurum = new Label();
        grpTekli.SuspendLayout();
        SuspendLayout();
        // 
        // btnTasarla
        // 
        btnTasarla.BackColor = Color.LightSkyBlue;
        btnTasarla.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnTasarla.Location = new Point(30, 25);
        btnTasarla.Name = "btnTasarla";
        btnTasarla.Size = new Size(420, 50);
        btnTasarla.TabIndex = 2;
        btnTasarla.Text = "🎨 Sürükle-Bırak Etiket Tasarımcısını Aç";
        btnTasarla.UseVisualStyleBackColor = false;
        btnTasarla.Click += btnTasarla_Click;
        // 
        // btnYazdirTekli
        // 
        btnYazdirTekli.BackColor = Color.LightGreen;
        btnYazdirTekli.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnYazdirTekli.Location = new Point(120, 150);
        btnYazdirTekli.Name = "btnYazdirTekli";
        btnYazdirTekli.Size = new Size(260, 38);
        btnYazdirTekli.TabIndex = 6;
        btnYazdirTekli.Text = "🖨️ Tekli Etiket Bas (Önizle / Yazdır)";
        btnYazdirTekli.UseVisualStyleBackColor = false;
        btnYazdirTekli.Click += btnYazdirTekli_Click;
        // 
        // btnYazdirToplu
        // 
        btnYazdirToplu.BackColor = Color.Khaki;
        btnYazdirToplu.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnYazdirToplu.Location = new Point(30, 315);
        btnYazdirToplu.Name = "btnYazdirToplu";
        btnYazdirToplu.Size = new Size(420, 45);
        btnYazdirToplu.TabIndex = 1;
        btnYazdirToplu.Text = "📦 Toplu Etiket Bas (10 Farklı Ürün)";
        btnYazdirToplu.UseVisualStyleBackColor = false;
        btnYazdirToplu.Click += btnYazdirToplu_Click;
        // 
        // txtBarkod
        // 
        txtBarkod.Location = new Point(120, 32);
        txtBarkod.Name = "txtBarkod";
        txtBarkod.Size = new Size(260, 27);
        txtBarkod.TabIndex = 1;
        txtBarkod.Text = "8690123456789";
        // 
        // txtUrunAdi
        // 
        txtUrunAdi.Location = new Point(120, 72);
        txtUrunAdi.Name = "txtUrunAdi";
        txtUrunAdi.Size = new Size(260, 27);
        txtUrunAdi.TabIndex = 3;
        txtUrunAdi.Text = "Kablosuz Klavye & Mouse Seti";
        // 
        // txtFiyat
        // 
        txtFiyat.Location = new Point(120, 112);
        txtFiyat.Name = "txtFiyat";
        txtFiyat.Size = new Size(260, 27);
        txtFiyat.TabIndex = 5;
        txtFiyat.Text = "750.00";
        // 
        // lblBarkod
        // 
        lblBarkod.AutoSize = true;
        lblBarkod.Location = new Point(20, 35);
        lblBarkod.Name = "lblBarkod";
        lblBarkod.Size = new Size(83, 20);
        lblBarkod.TabIndex = 0;
        lblBarkod.Text = "Barkod No:";
        // 
        // lblUrunAdi
        // 
        lblUrunAdi.AutoSize = true;
        lblUrunAdi.Location = new Point(20, 75);
        lblUrunAdi.Name = "lblUrunAdi";
        lblUrunAdi.Size = new Size(70, 20);
        lblUrunAdi.TabIndex = 2;
        lblUrunAdi.Text = "Ürün Adı:";
        // 
        // lblFiyat
        // 
        lblFiyat.AutoSize = true;
        lblFiyat.Location = new Point(20, 115);
        lblFiyat.Name = "lblFiyat";
        lblFiyat.Size = new Size(72, 20);
        lblFiyat.TabIndex = 4;
        lblFiyat.Text = "Fiyat (TL):";
        // 
        // grpTekli
        // 
        grpTekli.Controls.Add(lblBarkod);
        grpTekli.Controls.Add(txtBarkod);
        grpTekli.Controls.Add(lblUrunAdi);
        grpTekli.Controls.Add(txtUrunAdi);
        grpTekli.Controls.Add(lblFiyat);
        grpTekli.Controls.Add(txtFiyat);
        grpTekli.Controls.Add(btnYazdirTekli);
        grpTekli.Location = new Point(30, 100);
        grpTekli.Name = "grpTekli";
        grpTekli.Size = new Size(420, 200);
        grpTekli.TabIndex = 0;
        grpTekli.TabStop = false;
        grpTekli.Text = "Tekli Etiket Bilgileri";
        // 
        // lblDurum
        // 
        lblDurum.AutoSize = true;
        lblDurum.ForeColor = Color.DarkSlateGray;
        lblDurum.Location = new Point(30, 380);
        lblDurum.Name = "lblDurum";
        lblDurum.Size = new Size(271, 20);
        lblDurum.TabIndex = 0;
        lblDurum.Text = "Durum: Hazır. Şablon: etiket_sablonu.frx";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(483, 420);
        Controls.Add(lblDurum);
        Controls.Add(btnYazdirToplu);
        Controls.Add(grpTekli);
        Controls.Add(btnTasarla);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "FastReport Barkod & Etiket Sistemi";
        grpTekli.ResumeLayout(false);
        grpTekli.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
