using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FastReport;
using FastReport.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Services;

public class BarkodYaziciServisi : IYaziciServisi
{
    private readonly string _sablonlarKlasoru;
    private readonly string _sablonYolu;
    private readonly string _postgresConnectionString;
    private readonly IConfiguration _config;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;
    private readonly ILogger<BarkodYaziciServisi> _logger;

    public string VarsayilanYazici { get; set; }
    public string SablonYolu => _sablonYolu;
    public string SablonlarKlasoru => _sablonlarKlasoru;

    public List<string> MevcutSablonlariGetir()
    {
        var liste = new List<string>();
        try
        {
            if (Directory.Exists(_sablonlarKlasoru))
            {
                foreach (var dosya in Directory.GetFiles(_sablonlarKlasoru, "*.frx"))
                {
                    liste.Add(Path.GetFileName(dosya));
                }
            }
        }
        catch { }

        if (!liste.Any(s => s.Equals(Path.GetFileName(_sablonYolu), StringComparison.OrdinalIgnoreCase)) && File.Exists(_sablonYolu))
        {
            liste.Insert(0, Path.GetFileName(_sablonYolu));
        }

        return liste.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public long SablonZamanDamgasiniGetir(string? ozelSablonAdi = null)
    {
        try
        {
            string yol = HedefSablonYolunuBul(ozelSablonAdi);
            if (File.Exists(yol))
            {
                return File.GetLastWriteTimeUtc(yol).Ticks;
            }
        }
        catch { }
        return 0;
    }

    public Dictionary<string, long> SablonKatalogunuGetir()
    {
        var katalog = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var sablonAdi in MevcutSablonlariGetir())
        {
            katalog[sablonAdi] = SablonZamanDamgasiniGetir(sablonAdi);
        }
        return katalog;
    }

    public string SablonIceriginiGetir(string? ozelSablonAdi = null)
    {
        try
        {
            string yol = HedefSablonYolunuBul(ozelSablonAdi);
            if (File.Exists(yol))
            {
                return File.ReadAllText(yol, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şablon içeriği okunurken hata oluştu: {Sablon}", ozelSablonAdi);
        }
        return string.Empty;
    }

    public bool SablonuGuncelle(string yeniFrxXmlIcerik, long yeniZamanDamgasi, string? ozelSablonAdi = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(yeniFrxXmlIcerik)) return false;

            string yol = HedefSablonYolunuBul(ozelSablonAdi, olusturulsunMu: true);
            File.WriteAllText(yol, yeniFrxXmlIcerik, Encoding.UTF8);
            if (yeniZamanDamgasi > 0)
            {
                File.SetLastWriteTimeUtc(yol, new DateTime(yeniZamanDamgasi, DateTimeKind.Utc));
            }

            _logger.LogInformation("🔄 [ŞABLON GÜNCELLENDİ] '{Sablon}' şablonu diske kaydedildi! ({Yol})", Path.GetFileName(yol), yol);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ağdan gelen şablon kaydedilirken hata oluştu: {Sablon}", ozelSablonAdi);
            return false;
        }
    }

    public bool YeniSablonOlustur(string sablonAdi, bool a4Modu = false)
    {
        try
        {
            if (!sablonAdi.EndsWith(".frx", StringComparison.OrdinalIgnoreCase))
                sablonAdi += ".frx";

            string hedef = Path.Combine(_sablonlarKlasoru, sablonAdi);
            if (File.Exists(hedef)) return true;

            using (var rep = new Report())
            {
                var page = new ReportPage
                {
                    Name = "Page1",
                    PaperWidth = a4Modu ? 210 : 100,
                    PaperHeight = a4Modu ? 297 : 50,
                    LeftMargin = a4Modu ? 10 : 2,
                    RightMargin = a4Modu ? 10 : 2,
                    TopMargin = a4Modu ? 10 : 2,
                    BottomMargin = a4Modu ? 10 : 2
                };

                var band = new DataBand
                {
                    Name = "Data1",
                    Height = FastReport.Utils.Units.Millimeters * (a4Modu ? 40 : 46)
                };
                page.Bands.Add(band);

                var txtBaslik = new TextObject
                {
                    Name = "txtBaslik",
                    Bounds = new System.Drawing.RectangleF(0, 0, FastReport.Utils.Units.Millimeters * (a4Modu ? 190 : 96), 25),
                    Text = a4Modu ? "[HASTANE RAPORU / REÇETE]" : "[HastaAdi]",
                    HorzAlign = HorzAlign.Center,
                    Font = new System.Drawing.Font("Arial", a4Modu ? 12 : 10, System.Drawing.FontStyle.Bold)
                };
                band.Objects.Add(txtBaslik);

                var barcode = new FastReport.Barcode.BarcodeObject
                {
                    Name = "Barcode1",
                    Bounds = new System.Drawing.RectangleF(FastReport.Utils.Units.Millimeters * 10, 30, FastReport.Utils.Units.Millimeters * 80, 60),
                    Barcode = new FastReport.Barcode.Barcode128(),
                    Expression = "[BarkodNo]"
                };
                band.Objects.Add(barcode);

                rep.Parameters.Add(new FastReport.Data.Parameter("IstekId") { DataType = typeof(string), Value = "504" });
                rep.Parameters.Add(new FastReport.Data.Parameter("BarkodNo") { DataType = typeof(string), Value = "869012345" });
                rep.Parameters.Add(new FastReport.Data.Parameter("HastaAdi") { DataType = typeof(string), Value = "Test Hasta" });
                rep.Parameters.Add(new FastReport.Data.Parameter("ProtokolNo") { DataType = typeof(string), Value = "P-504" });
                rep.Parameters.Add(new FastReport.Data.Parameter("Bolum") { DataType = typeof(string), Value = "Poliklinik" });

                rep.Pages.Add(page);
                rep.Save(hedef);
            }

            _logger.LogInformation("✨ Yeni şablon oluşturuldu: {Ad}", sablonAdi);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yeni şablon oluşturulamadı: {Ad}", sablonAdi);
            return false;
        }
    }

    public BarkodYaziciServisi(
        IConfiguration config,
        IYaziciYoneticisi yaziciYoneticisi,
        ILogger<BarkodYaziciServisi> logger)
    {
        _config = config;
        _logger = logger;
        _yaziciYoneticisi = yaziciYoneticisi;
        _postgresConnectionString = config["ConnectionStrings:PostgreSql"] ?? string.Empty;

        string anaDizin = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        _sablonlarKlasoru = Path.Combine(anaDizin, "Sablonlar");
        Directory.CreateDirectory(_sablonlarKlasoru);

        string configYazici = config["Printer:Name"] ?? string.Empty;
        VarsayilanYazici = yaziciYoneticisi.VarsayilanYaziciyiGetir(configYazici);

        string? ozelSablonYolu = config["TemplatePath"] ?? "../etiket_sablonu.frx";
        _sablonYolu = SablonYolunuBelirle(ozelSablonYolu);

        // Varsayılan 3 şablonu (Etiket, Hasta Bilekliği, A4 Sonuç Raporu) klasörde yoksa otomatik oluştur:
        VarsayilanTemelSablonlariYukle();

        // FastReport motoruna PostgreSQL bağlantı sürücüsünü kaydet
        try
        {
            FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgresDataConnection kaydı yapılırken uyarı oluştu.");
        }

        _logger.LogInformation("BarkodYaziciServisi hazır. Şablonlar Klasörü: '{Klasor}', Varsayılan: '{Sablon}'",
            _sablonlarKlasoru, Path.GetFileName(_sablonYolu));
    }

    private void VarsayilanTemelSablonlariYukle()
    {
        // 1. kan_tupu_sablonu.frx (Termal barkod yazıcı için küçük tüp etiketi)
        string kanTupu = Path.Combine(_sablonlarKlasoru, "kan_tupu_sablonu.frx");
        if (!File.Exists(kanTupu))
        {
            if (File.Exists(_sablonYolu)) File.Copy(_sablonYolu, kanTupu, overwrite: true);
            else YeniSablonOlustur("kan_tupu_sablonu.frx", a4Modu: false);
        }

        // 2. hasta_bileklik_sablonu.frx (Termal barkod yazıcı için bileklik)
        string hastaBileklik = Path.Combine(_sablonlarKlasoru, "hasta_bileklik_sablonu.frx");
        if (!File.Exists(hastaBileklik))
        {
            YeniSablonOlustur("hasta_bileklik_sablonu.frx", a4Modu: false);
        }

        // 3. dosya_barkod_a4.frx (Normal lazer / A4 yazıcı için sonuç & dosya raporu)
        string dosyaA4 = Path.Combine(_sablonlarKlasoru, "dosya_barkod_a4.frx");
        if (!File.Exists(dosyaA4))
        {
            YeniSablonOlustur("dosya_barkod_a4.frx", a4Modu: true);
        }
    }

    private string HedefSablonYolunuBul(string? ozelSablonAdi, bool olusturulsunMu = false)
    {
        if (string.IsNullOrWhiteSpace(ozelSablonAdi))
            return _sablonYolu;

        if (!ozelSablonAdi.EndsWith(".frx", StringComparison.OrdinalIgnoreCase))
            ozelSablonAdi += ".frx";

        string klasordeki = Path.Combine(_sablonlarKlasoru, ozelSablonAdi);
        if (File.Exists(klasordeki) || olusturulsunMu)
            return klasordeki;

        string kokteki = Path.Combine(Path.GetDirectoryName(_sablonYolu) ?? AppContext.BaseDirectory, ozelSablonAdi);
        if (File.Exists(kokteki))
            return kokteki;

        return klasordeki;
    }

    /// <summary>
    /// TEK VE ANA BASKI METODU:
    /// İstenen şablonu (Kan Tüpü, Bileklik, A4 Dosya) ilgili yazıcıya (Zebra veya Brother) gönderir.
    /// </summary>
    public (bool Basarili, string Mesaj) BarkodBas(string istekId, string? ozelYazici = null, Dictionary<string, string>? parametreler = null, string? sablonAdi = null)
    {
        if (string.IsNullOrWhiteSpace(istekId))
        {
            string msg = "Boş istek ID ile baskı yapılamaz!";
            _logger.LogWarning("{Mesaj}", msg);
            return (false, msg);
        }

        string hedefYazici = !string.IsNullOrWhiteSpace(ozelYazici)
            ? ozelYazici
            : _yaziciYoneticisi.VarsayilanYaziciyiGetir(_config["Printer:Name"]);

        if (string.IsNullOrWhiteSpace(hedefYazici))
        {
            string msg = "Takılı ve çevrimiçi bir fiziksel yazıcı bulunamadı! Lütfen yazıcı bağlantısını veya yazıcı seçimini kontrol edin.";
            _logger.LogWarning("{Mesaj}", msg);
            return (false, msg);
        }

        // 2. KADEME KONTROLÜ: Seçilen yazıcı ağdaki diğer bilgisayara (örn. PC-A) mı ait?
        if (_yaziciYoneticisi.AgYazicisiMi(hedefYazici, out string hedefIp, out int hedefPort, out string gercekYaziciAdi))
        {
            var agSonuc = AgdakiBilgisayaraBaskiGonder(istekId, hedefIp, hedefPort, gercekYaziciAdi, parametreler, sablonAdi);
            if (!agSonuc.Basarili)
            {
                // Karşı yazıcı çıkarılmışsa listeyi de anında yenilemesi için taramayı tetikle
                _yaziciYoneticisi.AgTaramasiTetikle();
            }
            return agSonuc;
        }

        // Ağ yazıcısı formatında ("[PC-A | ...]") olduğu halde artık aktif ağ listesinde yoksa (USB sökülmüş / PC kapanmışsa) anında reddet!
        if (hedefYazici.StartsWith('['))
        {
            string msg = $"Seçilen ağ yazıcısı ({hedefYazici}) artık takılı değil veya çevrimdışı! Baskı iptal edildi.";
            _logger.LogWarning("{Mesaj}", msg);
            return (false, msg);
        }

        // CANLI FİZİKSEL BAĞLANTI KONTROLÜ:
        // USB kablosu 1 milisaniye önce çekilmiş olsa bile Windows sahte kuyruğuna (Bekleyen İşler) atmasını ENGELLE!
        if (!_yaziciYoneticisi.GecerliYaziciMi(hedefYazici))
        {
            string msg = $"'{hedefYazici}' yazıcısının USB kablosu çıkarılmış veya yazıcı çevrimdışı! Baskı kuyruğa alınmadan iptal edildi.";
            _logger.LogWarning("❌ [BASKI ENGELLENDİ] {Mesaj}", msg);
            return (false, msg);
        }

        try
        {
            string aktifSablonYolu = HedefSablonYolunuBul(sablonAdi);
            if (!File.Exists(aktifSablonYolu))
            {
                string msg = $"Şablon dosyası bulunamadı: {aktifSablonYolu}";
                _logger.LogError("{Mesaj}", msg);
                return (false, msg);
            }

            using (Report report = new Report())
            {
                report.Load(aktifSablonYolu);

                // 1. PostgreSQL bağlantı dizesini şablona enjekte et (varsa)
                if (!string.IsNullOrWhiteSpace(_postgresConnectionString))
                {
                    foreach (DataConnectionBase conn in report.Dictionary.Connections)
                    {
                        if (conn is PostgresDataConnection || conn.GetType().Name.Contains("Postgres"))
                            conn.ConnectionString = _postgresConnectionString;
                    }
                }

                // 2. Şablondaki parametreleri dinamik/parametrik olarak ayarla
                SablonParametreleriniUygula(report, istekId, parametreler);

                // 3. Raporu hazırla (SQL sorgusu burada çalışır ve satırlar çekilir)
                report.Prepare();

                // 4. Hedef yazıcıya sessizce bas
                if (!string.IsNullOrWhiteSpace(hedefYazici))
                    report.PrintSettings.Printer = hedefYazici;

                report.PrintSettings.ShowDialog = false;
                report.PrintPrepared();

                string msg = $"İstek #{istekId} başarıyla '{Path.GetFileName(aktifSablonYolu)}' şablonuyla '{hedefYazici}' yazıcısına gönderildi.";
                _logger.LogInformation("{Mesaj}", msg);
                return (true, msg);
            }
        }
        catch (Exception ex)
        {
            string msg = $"Baskı başarısız (İstek #{istekId}): {ex.Message}";
            _logger.LogError(ex, "{Mesaj}", msg);
            return (false, msg);
        }
    }

    /// <summary>
    /// Şablondaki tüm parametreleri tamamen dinamik Key-Value mantığıyla doldurur:
    /// 1) Önce appsettings.json -> SablonParametreleri altındaki tüm Key-Value çiftlerini okur,
    /// 2) Sonra istekten gelen dinamik parametreler (gelenParametreler) varsa üzerine yazar,
    /// 3) Son olarak döngüyle şablondaki eşleşen tüm parametrelere değerlerini atar.
    /// </summary>
    private void SablonParametreleriniUygula(Report report, string istekId, Dictionary<string, string>? gelenParametreler)
    {
        report.SetParameterValue("IstekId", istekId);

        var tumParametreler = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. appsettings.json -> "SablonParametreleri" altındaki tüm Key'leri dinamik olarak oku
        var configBolumu = _config.GetSection("SablonParametreleri");
        foreach (var child in configBolumu.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                tumParametreler[child.Key] = child.Value.Replace("{IstekId}", istekId);
            }
        }

        // 2. İstekle birlikte dışarıdan gönderilen Key-Value çiftleri varsa ekle veya güncelle
        if (gelenParametreler != null)
        {
            foreach (var kvp in gelenParametreler)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                {
                    tumParametreler[kvp.Key] = kvp.Value;
                }
            }
        }

        // 3. Şablonda tanımlı olan tüm eşleşen parametrelere değerlerini ata
        foreach (var kvp in tumParametreler)
        {
            if (report.Parameters.FindByName(kvp.Key) != null)
            {
                report.SetParameterValue(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// 2. KADEME: Seçilen yazıcı başka bir bilgisayardaki Agent'a (örn. PC-A) bağlı olduğunda,
    /// yazdırma emrini yerel ağ (TCP) üzerinden o bilgisayarın IP ve Portuna iletir.
    /// </summary>
    private (bool Basarili, string Mesaj) AgdakiBilgisayaraBaskiGonder(
        string istekId,
        string hedefIp,
        int hedefPort,
        string gercekYaziciAdi,
        Dictionary<string, string>? parametreler = null,
        string? sablonAdi = null)
    {
        try
        {
            _logger.LogInformation("📤 [AĞ YÖNLENDİRME] İstek #{IstekId} -> {Ip}:{Port} adresindeki '{Yazici}' yazıcısına (Şablon: {Sablon}) gönderiliyor...",
                istekId, hedefIp, hedefPort, gercekYaziciAdi, sablonAdi ?? "Varsayılan");

            using var tcpClient = new TcpClient();
            tcpClient.Connect(hedefIp, hedefPort);

            using var stream = tcpClient.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var istekPaketi = new AgBaskiIstekPaketi
            {
                GonderenAjan = Environment.MachineName,
                IstekId = istekId,
                HedefYazici = gercekYaziciAdi,
                SablonAdi = sablonAdi,
                Parametreler = parametreler
            };

            string jsonIstek = JsonSerializer.Serialize(istekPaketi);
            writer.WriteLine(jsonIstek);

            string? jsonYanit = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(jsonYanit))
            {
                var sonuc = JsonSerializer.Deserialize<BarkodSonucModel>(jsonYanit);
                if (sonuc != null)
                {
                    _logger.LogInformation("📥 [AĞ YANITI ALINDI] ({Ip}:{Port}): {Mesaj}", hedefIp, hedefPort, sonuc.Mesaj);
                    return (sonuc.Basarili, $"[Ağ Üzerinden ({hedefIp}:{hedefPort})] {sonuc.Mesaj}");
                }
            }

            return (true, $"İstek #{istekId} ağdaki ({hedefIp}:{hedefPort}) '{gercekYaziciAdi}' yazıcısına iletildi.");
        }
        catch (Exception ex)
        {
            string hata = $"Ağdaki bilgisayara ({hedefIp}:{hedefPort}) ulaşılamadı: {ex.Message}";
            _logger.LogError(ex, "{Hata}", hata);
            return (false, hata);
        }
    }

    private static string SablonYolunuBelirle(string? ozelSablonYolu)
    {
        string anaDizin = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        if (!string.IsNullOrWhiteSpace(ozelSablonYolu))
        {
            string tamYol = Path.IsPathRooted(ozelSablonYolu)
                ? ozelSablonYolu
                : Path.GetFullPath(Path.Combine(anaDizin, ozelSablonYolu));

            if (File.Exists(tamYol))
                return tamYol;
        }

        string yerelSablon = Path.Combine(anaDizin, "etiket_sablonu.frx");
        return yerelSablon;
    }
}
