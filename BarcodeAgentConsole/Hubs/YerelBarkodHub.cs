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

        // Bağlanan web ekranına anında bu bilgisayardaki + ağdan (TCP) gelen tüm yazıcıları gönder ve hızlı tarama tetikle
        var yazicilar = _yaziciYoneticisi.YukluYazicilariGetir();
        var sablonlar = _yaziciServisi.MevcutSablonlariGetir();
        await Clients.Caller.SendAsync("YazicilarAlindi", new
        {
            connectionId = Context.ConnectionId,
            yazicilar = yazicilar,
            sablonlar = sablonlar,
            yerelYaziciYokModu = _yaziciYoneticisi.YerelYaziciYokSimulasyonu,
            agPaylasimAcik = _yaziciYoneticisi.AgPaylasimAcik,
            ilkTaramaTamamlandi = _yaziciYoneticisi.IlkAgTaramasiTamamlandi,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });

        _yaziciYoneticisi.AgTaramasiTetikle();

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
    /// HBYS Web sayfasından İstek ID, Hedef Yazıcı (örn. A bilgisayarının yazıcısı),
    /// opsiyonel şablon parametreleri ve OnizlemeGoster bayrağı birlikte gönderildiğinde tetiklenir.
    /// </summary>
    public async Task BarkodYazdirGelismi(BarkodEmirModel emir)
    {
        string hedef = emir.HedefYazici ?? _yaziciServisi.VarsayilanYazici;
        _logger.LogInformation("📥 [YEREL HUB] Web'den 'BarkodYazdirGelismi' emri alındı -> İstek #{IstekId}, Hedef: '{Yazici}', Şablon: '{Sablon}', Önizleme: {Onizle}",
            emir.IstekId, hedef, emir.SablonAdi ?? "Varsayılan", emir.OnizlemeGoster);

        (bool Basarili, string Mesaj) sonuc;

        if (emir.OnizlemeGoster)
        {
            sonuc = _yaziciServisi.RaporOnizle(emir.IstekId, emir.HedefYazici, emir.Parametreler, emir.SablonAdi);
        }
        else
        {
            sonuc = _yaziciServisi.BarkodBas(emir.IstekId, emir.HedefYazici, emir.Parametreler, emir.SablonAdi);
        }

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
    /// HBYS Web sayfasından doğrudan ekranda FastReport Önizleme Penceresini açmak için çağrılır.
    /// </summary>
    public async Task BarkodOnizle(BarkodEmirModel emir)
    {
        emir.OnizlemeGoster = true;
        await BarkodYazdirGelismi(emir);
    }

    /// <summary>
    /// Web ekranı güncel yazıcı ve şablon listesini istediğinde tetiklenir.
    /// </summary>
    public async Task YazicilariGetir()
    {
        _yaziciYoneticisi.AgTaramasiTetikle();
        var yazicilar = _yaziciYoneticisi.YukluYazicilariGetir();
        var sablonlar = _yaziciServisi.MevcutSablonlariGetir();
        await Clients.All.SendAsync("YazicilarAlindi", new
        {
            connectionId = Context.ConnectionId,
            yazicilar = yazicilar,
            sablonlar = sablonlar,
            yerelYaziciYokModu = _yaziciYoneticisi.YerelYaziciYokSimulasyonu,
            agPaylasimAcik = _yaziciYoneticisi.AgPaylasimAcik,
            ilkTaramaTamamlandi = _yaziciYoneticisi.IlkAgTaramasiTamamlandi,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });
    }

    /// <summary>
    /// Tek bilgisayarda A ve B bilgisayarlarını test ederken:
    /// - "YerelYaziciYokModu" (Bu bilgisayarda fiziksel yazıcı yokmuş gibi davran)
    /// - "agPaylasimAcik" (Bu bilgisayarın yazıcılarını ağdaki diğer bilgisayarlara paylaş/kapat)
    /// ayarlarını canlı olarak değiştirir.
    /// </summary>
    public async Task TestModuDegistir(bool yerelYaziciYokModu, bool agPaylasimAcik)
    {
        _yaziciYoneticisi.YerelYaziciYokSimulasyonu = yerelYaziciYokModu;
        _yaziciYoneticisi.AgPaylasimAcik = agPaylasimAcik;

        _logger.LogInformation("🔧 [TEST MODU GÜNCELLENDİ] YerelYaziciYok={Yok}, AgPaylasimAcik={Paylasim}",
            yerelYaziciYokModu, agPaylasimAcik);

        await YazicilariGetir();
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
