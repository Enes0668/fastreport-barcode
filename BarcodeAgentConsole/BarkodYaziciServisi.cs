using System;
using System.IO;
using FastReport;
using FastReport.Data;

namespace BarcodeAgentConsole;

public class BarkodYaziciServisi
{
    private readonly string _sablonYolu;
    private readonly string _postgresConnectionString;
    public string VarsayilanYazici { get; set; }

    public BarkodYaziciServisi(string varsayilanYazici, string postgresConnString, string? ozelSablonYolu = null)
    {
        VarsayilanYazici = varsayilanYazici;
        _postgresConnectionString = postgresConnString;

        // Akıllı Ortak Şablon Yolu Tespiti:
        // 1. Verilen özel yol varsa ve dosya mevcutsa onu al
        if (!string.IsNullOrWhiteSpace(ozelSablonYolu))
        {
            string tamYol = Path.IsPathRooted(ozelSablonYolu) 
                ? ozelSablonYolu 
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ozelSablonYolu));

            if (File.Exists(tamYol))
                _sablonYolu = tamYol;
            else
                _sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
        }
        else
        {
            _sablonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "etiket_sablonu.frx");
        }

        // FastReport motoruna PostgreSQL bağlantı sürücüsünü kaydet
        FastReport.Utils.RegisteredObjects.AddConnection(typeof(PostgresDataConnection));
    }

    /// <summary>
    /// TEK VE ANA BASKI METODU:
    /// C# tarafında HastaAdi, Bolum, BarkodNo gibi verilerle uğraşılmaz!
    /// C# sadece IstekId'yi şablona verir; FastReport içindeki SQL sorgusu
    /// PostgreSQL'e gidip o isteğe ait tüm verileri dinamik çeker ve basar.
    /// </summary>
    public bool BarkodBas(string istekId, string? ozelYazici = null)
    {
        if (string.IsNullOrWhiteSpace(istekId))
        {
            LogMesaj("[UYARI] Boş istek ID ile baskı yapılamaz!", ConsoleColor.Yellow);
            return false;
        }

        string hedefYazici = !string.IsNullOrWhiteSpace(ozelYazici) ? ozelYazici : VarsayilanYazici;

        try
        {
            if (!File.Exists(_sablonYolu))
            {
                LogMesaj($"[HATA] Şablon dosyası bulunamadı: {_sablonYolu}", ConsoleColor.Red);
                return false;
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

                LogMesaj($"[BAŞARILI] İstek #{istekId} -> PostgreSQL üzerinden çekildi ve '{hedefYazici}' yazıcısına basıldı.", ConsoleColor.Green);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMesaj($"[HATA] Baskı başarısız (İstek #{istekId}): {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }



    private static void LogMesaj(string mesaj, ConsoleColor renk)
    {
        var eskiRenk = Console.ForegroundColor;
        Console.ForegroundColor = renk;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {mesaj}");
        Console.ForegroundColor = eskiRenk;
    }
}
