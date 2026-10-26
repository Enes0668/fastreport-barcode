using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarcodeAgentConsole.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Hubs;

/// <summary>
/// 2. YÖNTEM (GÖMÜLÜ SIGNALR HUB):
/// Merkezi bir sunucuya ihtiyaç duymadan, Agent'ın çalıştığı bilgisayarda (localhost:5000/barkodHub)
/// doğrudan açılan yerel SignalR Hub.
/// HBYS web sayfasındaki "Barkod Bas" butonu doğrudan buraya bağlanıp IstekId ve parametreleri gönderir.
/// </summary>
public class YerelBarkodHub : Hub
{
    private readonly IYaziciServisi _yaziciServisi;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;
    private readonly ILogger<YerelBarkodHub> _logger;

    public YerelBarkodHub(
        IYaziciServisi yaziciServisi,
        IYaziciYoneticisi yaziciYoneticisi,
        ILogger<YerelBarkodHub> logger)
    {
        _yaziciServisi = yaziciServisi;
        _yaziciYoneticisi = yaziciYoneticisi;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🌐 [YEREL HUB] Web tarayıcısı / HBYS istemcisi bağlandı (ConnectionId: {Id})", Context.ConnectionId);

        // Bağlanan web ekranına anında bu bilgisayardaki + ağdan (UDP) gelen tüm yazıcıları gönder
        var yazicilar = _yaziciYoneticisi.YukluYazicilariGetir();
        await Clients.Caller.SendAsync("YazicilarAlindi", new
        {
            connectionId = Context.ConnectionId,
            yazicilar = yazicilar,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// HBYS Web sayfasından SADECE İstek ID gönderildiğinde tetiklenir.
    /// </summary>
    public async Task BarkodYazdir(string istekId)
    {
        _logger.LogInformation("📥 [YEREL HUB] Web'den 'BarkodYazdir' emri alındı -> İstek #{IstekId}", istekId);
        var sonuc = _yaziciServisi.BarkodBas(istekId);

        await Clients.All.SendAsync("BaskiSonucuAlindi", new BarkodSonucModel
        {
            IstekId = istekId,
            Basarili = sonuc.Basarili,
            Mesaj = sonuc.Mesaj,
            Yazici = _yaziciServisi.VarsayilanYazici,
            Tarih = DateTime.Now
        });
    }

    /// <summary>
    /// HBYS Web sayfasından İstek ID, Hedef Yazıcı (örn. A bilgisayarının yazıcısı) ve
    /// opsiyonel şablon parametreleri birlikte gönderildiğinde tetiklenir.
    /// </summary>
    public async Task BarkodYazdirGelismi(BarkodEmirModel emir)
    {
        string hedef = emir.HedefYazici ?? _yaziciServisi.VarsayilanYazici;
        _logger.LogInformation("📥 [YEREL HUB] Web'den 'BarkodYazdirGelismi' emri alındı -> İstek #{IstekId}, Hedef: '{Yazici}'",
            emir.IstekId, hedef);

        var sonuc = _yaziciServisi.BarkodBas(emir.IstekId, emir.HedefYazici, emir.Parametreler);

        await Clients.All.SendAsync("BaskiSonucuAlindi", new BarkodSonucModel
        {
            IstekId = emir.IstekId,
            Basarili = sonuc.Basarili,
            Mesaj = sonuc.Mesaj,
            Yazici = hedef,
            Tarih = DateTime.Now
        });
    }

    /// <summary>
    /// Web ekranı güncel yazıcı listesini istediğinde tetiklenir.
    /// </summary>
    public async Task YazicilariGetir()
    {
        var yazicilar = _yaziciYoneticisi.YukluYazicilariGetir();
        await Clients.All.SendAsync("YazicilarAlindi", new
        {
            connectionId = Context.ConnectionId,
            yazicilar = yazicilar,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });
    }

    /// <summary>
    /// Geriye dönük uyumluluk: Başka bir istemci YazicilariBildir çağırırsa tüm ekranlara yayınla.
    /// </summary>
    public async Task YazicilariBildir(List<string> yazicilar)
    {
        await Clients.All.SendAsync("YazicilarAlindi", new
        {
            connectionId = Context.ConnectionId,
            yazicilar = yazicilar,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });
    }

    /// <summary>
    /// Geriye dönük uyumluluk: Baskı sonucu bildirildiğinde tüm ekranlara yayınla.
    /// </summary>
    public async Task BarkodSonucBildir(BarkodSonucModel sonuc)
    {
        await Clients.All.SendAsync("BaskiSonucuAlindi", sonuc);
    }
}
