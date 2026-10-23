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

    // Ağdaki diğer Agent'lardan gelen yazıcıları tutar.
    // Anahtar: Görünen Ad -> örn: "[PC-A | 127.0.0.1:5055] Zebra ZD220"
    private readonly ConcurrentDictionary<string, (string Ip, int Port, string GercekYaziciAdi)> _agYazicilari = new();

    public YaziciYoneticisi(ILogger<YaziciYoneticisi> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sadece bu bilgisayara doğrudan bağlı olan yerel yazıcıları getirir.
    /// </summary>
    public List<string> YerelYazicilariGetir()
    {
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
    /// Yeni bir yazıcı eklendiyse true döner.
    /// </summary>
    public bool AgYazicilariniGuncelle(AjanPaylasimPaketi paket)
    {
        bool degisiklikOldu = false;

        foreach (var yazici in paket.Yazicilar)
        {
            string gorunenAd = $"[{paket.AjanAdi} | {paket.IpAdresi}:{paket.TcpPort}] {yazici}";
            if (_agYazicilari.TryAdd(gorunenAd, (paket.IpAdresi, paket.TcpPort, yazici)))
            {
                degisiklikOldu = true;
                _logger.LogInformation("🌐 Ağdan yeni yazıcı algılandı: {GorunenAd}", gorunenAd);
            }
        }

        return degisiklikOldu;
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
