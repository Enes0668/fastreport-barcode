using System;
using System.IO;
using FastReport;
using FastReport.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Services;

public class BarkodYaziciServisi : IYaziciServisi
{
    private readonly string _sablonYolu;
    private readonly string _postgresConnectionString;
    private readonly ILogger<BarkodYaziciServisi> _logger;

    public string VarsayilanYazici { get; set; }

    public BarkodYaziciServisi(
        IConfiguration config,
        IYaziciYoneticisi yaziciYoneticisi,
        ILogger<BarkodYaziciServisi> logger)
    {
        _logger = logger;
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
    /// C# tarafında HastaAdi, Bolum, BarkodNo gibi verilerle uğraşılmaz!
    /// C# sadece IstekId'yi şablona verir; FastReport içindeki SQL sorgusu
    /// PostgreSQL'e gidip o isteğe ait tüm verileri dinamik çeker ve basar.
    /// </summary>
    public (bool Basarili, string Mesaj) BarkodBas(string istekId, string? ozelYazici = null)
    {
        if (string.IsNullOrWhiteSpace(istekId))
        {
            string msg = "Boş istek ID ile baskı yapılamaz!";
            _logger.LogWarning("{Mesaj}", msg);
            return (false, msg);
        }

        string hedefYazici = !string.IsNullOrWhiteSpace(ozelYazici) ? ozelYazici : VarsayilanYazici;

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

                // 2. Şablondaki @IstekId parametresini ayarla
                report.SetParameterValue("IstekId", istekId);

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
