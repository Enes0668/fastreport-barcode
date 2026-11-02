using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

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

    public event Action? AnindaTaramaIstendi;
    public void AgTaramasiTetikle() => AnindaTaramaIstendi?.Invoke();

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

    #region WinSpool + SetupAPI P/Invoke (Anlık Fiziksel USB & Çevrimiçi Kontrolü)

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

    private static readonly Guid GUID_DEVINTERFACE_USBPRINT = new("28d78fad-5a12-11d1-ae5b-0000f803a8c2");
    private const uint DIGCF_PRESENT = 0x02;
    private const uint DIGCF_DEVICEINTERFACE = 0x10;
    private const int KEY_READ = 0x20019;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiOpenDeviceInterfaceRegKey(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, uint reserved, int samDesired);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

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
    /// Windows USB çekirdek sürücüsüne (usbprint.sys) doğrudan sorarak ŞU SANİYE fiziksel olarak
    /// kablosu takılı ve açık olan USB yazıcı portlarını (örn: USB001) 0.5 milisaniyede döndürür.
    /// Windows Print Spooler'ın 5-15 saniyelik gecikmesini tamamen ortadan kaldırır!
    /// </summary>
    private static HashSet<string> AktifFizikselUsbPortlariniGetir(out bool sorguBasarili)
    {
        var aktifPortlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sorguBasarili = false;

        try
        {
            Guid guid = GUID_DEVINTERFACE_USBPRINT;
            IntPtr hDevInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
                return aktifPortlar;

            sorguBasarili = true;
            try
            {
                uint idx = 0;
                while (true)
                {
                    var did = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref guid, idx, ref did))
                        break;

                    IntPtr hKey = SetupDiOpenDeviceInterfaceRegKey(hDevInfo, ref did, 0, KEY_READ);
                    if (hKey != IntPtr.Zero && hKey != new IntPtr(-1))
                    {
                        try
                        {
                            using var regKey = RegistryKey.FromHandle(new SafeRegistryHandle(hKey, ownsHandle: false));
                            string baseName = regKey.GetValue("Base Name")?.ToString() ?? "USB";
                            if (regKey.GetValue("Port Number") is int portNum)
                            {
                                aktifPortlar.Add($"{baseName}{portNum:D3}");
                            }
                        }
                        finally
                        {
                            RegCloseKey(hKey);
                        }
                    }

                    idx++;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }
        catch
        {
            sorguBasarili = false;
        }

        return aktifPortlar;
    }

    private static bool UsbYaziciPortuMu(string portAdi)
    {
        if (string.IsNullOrWhiteSpace(portAdi) || portAdi.Length < 4)
            return false;

        if (!portAdi.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
            return false;

        for (int i = 3; i < portAdi.Length; i++)
        {
            if (!char.IsDigit(portAdi[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Hem Windows Print Spooler durumunu hem de doğrudan fiziksel USB portunu (USB001 vb.) kontrol eder.
    /// USB kablosu çekildiği milisaniyede Spooler'ın uyanmasını beklemeden anında FALSE döner!
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

                    if (offline)
                        return false;

                    // Yazıcı USB portuna (örn: USB001) bağlıysa, doğrudan fiziksel USB donanım varlığını kontrol et!
                    if (!string.IsNullOrWhiteSpace(info.pPortName))
                    {
                        var usbPortlar = info.pPortName
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Where(UsbYaziciPortuMu)
                            .ToList();

                        if (usbPortlar.Count > 0)
                        {
                            var canliUsbPortlar = AktifFizikselUsbPortlariniGetir(out bool usbSorguBasarili);
                            if (usbSorguBasarili && !usbPortlar.Any(p => canliUsbPortlar.Contains(p)))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
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

        if (!string.IsNullOrWhiteSpace(configYazici) && GecerliYaziciMi(configYazici))
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
