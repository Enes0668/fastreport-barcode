using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Services;

public class YaziciYoneticisi : IYaziciYoneticisi
{
    private readonly ILogger<YaziciYoneticisi> _logger;

    // Ağdaki diğer Agent'lardan gelen yazıcıları ve son görülme (Heartbeat) zamanlarını tutar.
    // Anahtar: Görünen Ad -> örn: "[PC-A | 192.168.1.91:5055] ZDesigner ZD220"
    private readonly ConcurrentDictionary<string, (string AjanAdi, string Ip, int Port, string GercekYaziciAdi, DateTime SonGorulme)> _agYazicilari = new();

    public bool YerelYaziciYokSimulasyonu { get; set; } = false;
    public bool UdpPaylasimAcik { get; set; } = true;

    public YaziciYoneticisi(ILogger<YaziciYoneticisi> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sadece bu bilgisayara doğrudan bağlı olan yerel yazıcıları getirir.
    /// Eğer bu bilgisayar "B Bilgisayarı (Yazıcısız İstemci)" modundaysa boş liste döner!
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
                yazicilar.Add(p);
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
    /// Belirli bir süre (örn. 7 saniye) boyunca UDP sinyali gelmeyen (kapanan) bilgisayarların yazıcılarını listeden siler (Cache'de kalmasını önler).
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
            if (!string.IsNullOrWhiteSpace(varsayilan))
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
            _logger.LogInformation("İlk bulunan yazıcı seçildi: '{Yazici}'", printer);
            return printer;
        }

        _logger.LogWarning("Sistemde kurulu herhangi bir yazıcı bulunamadı!");
        return string.Empty;
    }
}
