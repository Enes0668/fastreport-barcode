using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Services;

public class YaziciYoneticisi : IYaziciYoneticisi
{
    private readonly ILogger<YaziciYoneticisi> _logger;

    // Ağdaki diğer Agent'lardan gelen yazıcıları ve son görülme (Heartbeat) zamanlarını tutar.
    // Anahtar: Görünen Ad -> örn: "[PC-A | 192.168.1.91:5055] ZDesigner ZD220"
    private readonly ConcurrentDictionary<string, (string AjanAdi, string Ip, int Port, string GercekYaziciAdi, DateTime SonGorulme)> _agYazicilari = new();

    public bool YerelYaziciYokSimulasyonu { get; set; } = false;
    public bool AgPaylasimAcik { get; set; } = true;
    public bool SanalYazicilariFiltrele { get; set; } = true;
    public bool SadeceCevrimiciYazicilar { get; set; } = true;

    private static readonly string[] SanalYaziciKelimeleri = new[]
    {
        "pdf",
        "onenote",
        "fax",
        "xps",
        "document writer",
        "root print queue",
        "send to",
        "snagit",
        "anydesk"
    };

    public YaziciYoneticisi(ILogger<YaziciYoneticisi> logger, IConfiguration config)
    {
        _logger = logger;
        if (bool.TryParse(config["Printer:SanalYazicilariFiltrele"], out bool filtre))
        {
            SanalYazicilariFiltrele = filtre;
        }

        if (bool.TryParse(config["Printer:SadeceCevrimiciYazicilar"], out bool cevrimici))
        {
            SadeceCevrimiciYazicilar = cevrimici;
        }
    }

    public bool GecerliYaziciMi(string yaziciAdi)
    {
        if (string.IsNullOrWhiteSpace(yaziciAdi)) return false;

        // 1. Sanal yazıcı kontrolü (PDF, Fax, OneNote)
        if (SanalYazicilariFiltrele)
        {
            string kucuk = yaziciAdi.ToLowerInvariant();
            if (SanalYaziciKelimeleri.Any(s => kucuk.Contains(s)))
            {
                return false;
            }
        }

        // 2. Anlık Çevrimiçi/Fiziksel Bağlantı kontrolü (USB kablosu takılı mı?)
        if (SadeceCevrimiciYazicilar)
        {
            if (!YaziciCevrimiciMi(yaziciAdi))
            {
                return false;
            }
        }

        return true;
    }

    #region WinSpool P/Invoke (Yazıcı Çevrimiçi/Takılı Durumu)

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_INFO_2
    {
        public string pServerName;
        public string pPrinterName;
        public string pShareName;
        public string pPortName;
        public string pDriverName;
        public string pComment;
        public string pLocation;
        public IntPtr pDevMode;
        public string pSepFile;
        public string pPrintProcessor;
        public string pDatatype;
        public string pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    private const uint PRINTER_ATTRIBUTE_WORK_OFFLINE = 0x00000400;
    private const uint PRINTER_STATUS_OFFLINE = 0x00000080;
    private const uint PRINTER_STATUS_NOT_AVAILABLE = 0x00001000;

    /// <summary>
    /// Windows Print Spooler servisine doğrudan sorarak yazıcının fiziksel olarak takılı ve açık olup olmadığını kontrol eder.
    /// USB fişi çekilmişse veya yazıcı kapalıysa FALSE döner.
    /// </summary>
    public bool YaziciCevrimiciMi(string yaziciAdi)
    {
        try
        {
            if (!OpenPrinter(yaziciAdi, out IntPtr hPrinter, IntPtr.Zero))
            {
                return false;
            }

            try
            {
                GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int bytesNeeded);
                if (bytesNeeded <= 0) return true;

                IntPtr pInfo = Marshal.AllocHGlobal(bytesNeeded);
                try
                {
                    if (!GetPrinter(hPrinter, 2, pInfo, bytesNeeded, out _))
                    {
                        return true;
                    }

                    var info = Marshal.PtrToStructure<PRINTER_INFO_2>(pInfo);
                    bool offline = (info.Attributes & PRINTER_ATTRIBUTE_WORK_OFFLINE) != 0 ||
                                   (info.Status & PRINTER_STATUS_OFFLINE) != 0 ||
                                   (info.Status & PRINTER_STATUS_NOT_AVAILABLE) != 0;

                    return !offline;
                }
                finally
                {
                    Marshal.FreeHGlobal(pInfo);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }
        catch
        {
            return true;
        }
    }

    #endregion

    /// <summary>
    /// Sadece bu bilgisayara doğrudan bağlı olan yerel yazıcıları getirir.
    /// Eğer bu bilgisayar "B Bilgisayarı (Yazıcısız İstemci)" modundaysa boş liste döner!
    /// PDF, OneNote, Fax gibi sanal yazıcılar otomatik filtrelenir.
    /// </summary>
    public List<string> YerelYazicilariGetir()
    {
        if (YerelYaziciYokSimulasyonu)
        {
            return new List<string>();
        }

        var yazicilar = new List<string>();
        try
        {
            foreach (string p in PrinterSettings.InstalledPrinters)
            {
                if (GecerliYaziciMi(p))
                {
                    yazicilar.Add(p);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yerel yazıcı listesi taranırken hata oluştu.");
        }

        return yazicilar;
    }

    /// <summary>
    /// Hem bu bilgisayardaki yerel yazıcıları hem de ağdaki diğer bilgisayarlardan (örn. A bilgisayarından)
    /// fırlatılan paylaşımlı yazıcıları birleştirerek Web ekranına döndürür.
    /// </summary>
    public List<string> YukluYazicilariGetir()
    {
        var tumYazicilar = YerelYazicilariGetir();

        foreach (var agYazicisi in _agYazicilari.Keys.OrderBy(k => k))
        {
            if (!tumYazicilar.Contains(agYazicisi))
            {
                tumYazicilar.Add(agYazicisi);
            }
        }

        return tumYazicilar;
    }

    /// <summary>
    /// Ağdaki başka bir Agent'tan (örn. A bilgisayarından) gelen yazıcı paketini hafızaya kaydeder.
    /// Eğer karşı taraf paylaşımı kapattıysa (boş liste gönderdiyse) o bilgisayara ait ağ yazıcılarını anında siler!
    /// </summary>
    public bool AgYazicilariniGuncelle(AjanPaylasimPaketi paket)
    {
        bool degisiklikOldu = false;
        DateTime simdi = DateTime.Now;

        // Eğer karşı bilgisayar paylaşımı kapattıysa veya yazıcısı söküldüyse, listeden düşür:
        var guncelGorunenAdlar = new HashSet<string>(
            paket.Yazicilar.Select(y => $"[{paket.AjanAdi} | {paket.IpAdresi}:{paket.TcpPort}] {y}")
        );

        foreach (var mevcut in _agYazicilari.Where(x => x.Value.AjanAdi == paket.AjanAdi).ToList())
        {
            if (!guncelGorunenAdlar.Contains(mevcut.Key))
            {
                if (_agYazicilari.TryRemove(mevcut.Key, out _))
                {
                    degisiklikOldu = true;
                    _logger.LogWarning("🔌 Ağ yazıcısı paylaşımdan kaldırıldı: {GorunenAd}", mevcut.Key);
                }
            }
        }

        // Gelen güncel yazıcıları ekle veya SonGorulme (Heartbeat) zamanını yenile:
        foreach (var yazici in paket.Yazicilar)
        {
            string gorunenAd = $"[{paket.AjanAdi} | {paket.IpAdresi}:{paket.TcpPort}] {yazici}";
            if (_agYazicilari.TryGetValue(gorunenAd, out var eski))
            {
                _agYazicilari[gorunenAd] = (paket.AjanAdi, paket.IpAdresi, paket.TcpPort, yazici, simdi);
            }
            else if (_agYazicilari.TryAdd(gorunenAd, (paket.AjanAdi, paket.IpAdresi, paket.TcpPort, yazici, simdi)))
            {
                degisiklikOldu = true;
                _logger.LogInformation("🌐 Ağdan yeni yazıcı algılandı: {GorunenAd}", gorunenAd);
            }
        }

        return degisiklikOldu;
    }

    /// <summary>
    /// Belirli bir süre (örn. 45 saniye) boyunca TCP sinyali gelmeyen (kapanan) bilgisayarların yazıcılarını listeden siler (Cache'de kalmasını önler).
    /// </summary>
    public bool ZamanAsiminaUgrayanAgYazicilariniTemizle(TimeSpan zamanAsimi)
    {
        bool silinenOldu = false;
        DateTime esikZaman = DateTime.Now - zamanAsimi;

        foreach (var kvp in _agYazicilari.ToList())
        {
            if (kvp.Value.SonGorulme < esikZaman)
            {
                if (_agYazicilari.TryRemove(kvp.Key, out _))
                {
                    silinenOldu = true;
                    _logger.LogWarning("⏱️ Ağ yazıcısı bağlantısı koptu (Timeout - Silindi): {GorunenAd}", kvp.Key);
                }
            }
        }

        return silinenOldu;
    }

    public bool AjaninYazicilariniKaldir(string ajanAdi)
    {
        bool silinenOldu = false;
        foreach (var kvp in _agYazicilari.Where(x => x.Value.AjanAdi == ajanAdi).ToList())
        {
            if (_agYazicilari.TryRemove(kvp.Key, out _))
            {
                silinenOldu = true;
            }
        }
        return silinenOldu;
    }

    /// <summary>
    /// Seçilen yazıcının yerel mi yoksa ağdaki diğer bilgisayara (Agent'a) mı ait olduğunu kontrol eder.
    /// </summary>
    public bool AgYazicisiMi(string? yaziciAdi, out string ipAdresi, out int port, out string gercekYaziciAdi)
    {
        ipAdresi = string.Empty;
        port = 0;
        gercekYaziciAdi = string.Empty;

        if (string.IsNullOrWhiteSpace(yaziciAdi))
            return false;

        if (_agYazicilari.TryGetValue(yaziciAdi, out var hedef))
        {
            ipAdresi = hedef.Ip;
            port = hedef.Port;
            gercekYaziciAdi = hedef.GercekYaziciAdi;
            return true;
        }

        return false;
    }

    public string VarsayilanYaziciyiGetir(string? configYazici = null)
    {
        if (YerelYaziciYokSimulasyonu)
        {
            // B bilgisayarında yerel yazıcı yoksa, varsa ilk ağ yazıcısını döndür, yoksa boş döndür
            return _agYazicilari.Keys.FirstOrDefault() ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(configYazici))
        {
            _logger.LogInformation("Konfigürasyondan belirlenen yazıcı kullanılıyor: '{Yazici}'", configYazici);
            return configYazici;
        }

        try
        {
            var printDoc = new PrintDocument();
            string varsayilan = printDoc.PrinterSettings.PrinterName;
            if (!string.IsNullOrWhiteSpace(varsayilan) && GecerliYaziciMi(varsayilan))
            {
                _logger.LogInformation("Sistem varsayılan yazıcısı algılandı: '{Yazici}'", varsayilan);
                return varsayilan;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sistem varsayılan yazıcısı okunamadı.");
        }

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            if (GecerliYaziciMi(printer))
            {
                _logger.LogInformation("İlk bulunan geçerli yazıcı seçildi: '{Yazici}'", printer);
                return printer;
            }
        }

        _logger.LogWarning("Sistemde kurulu herhangi bir yazıcı bulunamadı!");
        return string.Empty;
    }
}
