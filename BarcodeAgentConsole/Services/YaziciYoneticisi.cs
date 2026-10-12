using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Services;

public class YaziciYoneticisi : IYaziciYoneticisi
{
    private readonly ILogger<YaziciYoneticisi> _logger;

    public YaziciYoneticisi(ILogger<YaziciYoneticisi> logger)
    {
        _logger = logger;
    }

    public List<string> YukluYazicilariGetir()
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
            _logger.LogError(ex, "Yazıcı listesi taranırken hata oluştu.");
        }

        return yazicilar;
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
