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
    private readonly string _sablonYolu;
    private readonly string _postgresConnectionString;
    private readonly IConfiguration _config;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;
    private readonly ILogger<BarkodYaziciServisi> _logger;

    public string VarsayilanYazici { get; set; }
    public string SablonYolu => _sablonYolu;

    public long SablonZamanDamgasiniGetir()
    {
        try
        {
            if (File.Exists(_sablonYolu))
            {
                return File.GetLastWriteTimeUtc(_sablonYolu).Ticks;
            }
        }
        catch { }
        return 0;
    }

    public string SablonIceriginiGetir()
    {
        try
        {
            if (File.Exists(_sablonYolu))
            {
                return File.ReadAllText(_sablonYolu, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şablon içeriği okunurken hata oluştu: {Yol}", _sablonYolu);
        }
        return string.Empty;
    }

    public bool SablonuGuncelle(string yeniFrxXmlIcerik, long yeniZamanDamgasi)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(yeniFrxXmlIcerik)) return false;

            File.WriteAllText(_sablonYolu, yeniFrxXmlIcerik, Encoding.UTF8);
            if (yeniZamanDamgasi > 0)
            {
                File.SetLastWriteTimeUtc(_sablonYolu, new DateTime(yeniZamanDamgasi, DateTimeKind.Utc));
            }

            _logger.LogInformation("🔄 [ŞABLON GÜNCELLENDİ] Ağdan gelen yeni .frx şablonu başarıyla diske kaydedildi! ({Yol})", _sablonYolu);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ağdan gelen şablon kaydedilirken hata oluştu: {Yol}", _sablonYolu);
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

        string configYazici = config["Printer:Name"] ?? string.Empty;
        VarsayilanYazici = yaziciYoneticisi.VarsayilanYaziciyiGetir(configYazici);

        string? ozelSablonYolu = config["TemplatePath"] ?? "../etiket_sablonu.frx";
        _sablonYolu = SablonYolunuBelirle(ozelSablonYolu);

        // FastReport motoruna PostgreSQL bağlantı sürücüsünü kaydet
        try
        {
            FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgresDataConnection kaydı yapılırken uyarı oluştu.");
        }

        _logger.LogInformation("BarkodYaziciServisi hazır. Şablon: '{Sablon}', Varsayılan Yazıcı: '{Yazici}'",
            _sablonYolu, VarsayilanYazici);
    }

    /// <summary>
    /// TEK VE ANA BASKI METODU:
    /// Eğer seçilen yazıcı yerel ağdaki başka bir Agent'a (örn. A bilgisayarına) aitse,
    /// baskı emrini ve parametreleri ağ üzerinden o bilgisayara yönlendirir.
    /// Yerel yazıcı ise doğrudan FastReport ile bu bilgisayardan basar.
    /// </summary>
    public (bool Basarili, string Mesaj) BarkodBas(string istekId, string? ozelYazici = null, Dictionary<string, string>? parametreler = null)
    {
        if (string.IsNullOrWhiteSpace(istekId))
        {
            string msg = "Boş istek ID ile baskı yapılamaz!";
            _logger.LogWarning("{Mesaj}", msg);
            return (false, msg);
        }

        string hedefYazici = !string.IsNullOrWhiteSpace(ozelYazici) ? ozelYazici : VarsayilanYazici;

        // 2. KADEME KONTROLÜ: Seçilen yazıcı ağdaki diğer bilgisayara (örn. PC-A) mı ait?
        if (_yaziciYoneticisi.AgYazicisiMi(hedefYazici, out string hedefIp, out int hedefPort, out string gercekYaziciAdi))
        {
            return AgdakiBilgisayaraBaskiGonder(istekId, hedefIp, hedefPort, gercekYaziciAdi, parametreler);
        }

        try
        {
            if (!File.Exists(_sablonYolu))
            {
                string msg = $"Şablon dosyası bulunamadı: {_sablonYolu}";
                _logger.LogError("{Mesaj}", msg);
                return (false, msg);
            }

            using (Report report = new Report())
            {
                report.Load(_sablonYolu);

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

                string msg = $"İstek #{istekId} başarıyla '{hedefYazici}' yazıcısına gönderildi.";
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
        Dictionary<string, string>? parametreler = null)
    {
        try
        {
            _logger.LogInformation("📤 [AĞ YÖNLENDİRME] İstek #{IstekId} -> {Ip}:{Port} adresindeki '{Yazici}' yazıcısına gönderiliyor...",
                istekId, hedefIp, hedefPort, gercekYaziciAdi);

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
        if (!string.IsNullOrWhiteSpace(ozelSablonYolu))
        {
            string tamYol = Path.IsPathRooted(ozelSablonYolu)
                ? ozelSablonYolu
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ozelSablonYolu));

            if (File.Exists(tamYol))
                return tamYol;
        }

        string yerelSablon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
        return yerelSablon;
    }
}
