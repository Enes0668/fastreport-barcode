using System;
using System.Threading;
using System.Threading.Tasks;
using BarcodeAgentConsole.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Workers;

/// <summary>
/// Arka planda kesintisiz çalışan, SignalR soket bağlantısını yöneten ve gelen
/// baskı emirlerini karşılayıp yazıcı servisine yönlendiren ana işçi servis (Worker).
/// </summary>
public class BarkodAjanWorker : BackgroundService
{
    private readonly ILogger<BarkodAjanWorker> _logger;
    private readonly IYaziciServisi _yaziciServisi;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;
    private readonly string _hubUrl;
    private HubConnection? _hubConnection;

    public BarkodAjanWorker(
        ILogger<BarkodAjanWorker> logger,
        IYaziciServisi yaziciServisi,
        IYaziciYoneticisi yaziciYoneticisi,
        IConfiguration config)
    {
        _logger = logger;
        _yaziciServisi = yaziciServisi;
        _yaziciYoneticisi = yaziciYoneticisi;
        _hubUrl = config["SignalR:HubUrl"] ?? "http://localhost:5000/barkodHub";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Barkod Baskı Ajanı Worker başlatıldı. Dinlenen Hub: '{HubUrl}'", _hubUrl);

        await SignalRBaslatAsync(stoppingToken);

        // Worker iptal edilene (uygulama kapatılana) kadar arka planda canlı kalır
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Barkod Baskı Ajanı durduruluyor...");
        }
    }

    private async Task SignalRBaslatAsync(CancellationToken stoppingToken)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect(new[] {
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR bağlantısı koptu, yeniden bağlanılıyor... ({Hata})", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR bağlantısı tekrar sağlandı. (ConnectionId: {Id})", connectionId);
            _ = YazicilariWebEBildirAsync();
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning("SignalR bağlantısı kapandı. ({Hata})", error?.Message);
            return Task.CompletedTask;
        };

        // 1. Web'den SADECE İstek ID (örn: "504" veya "100") geldiğinde çalışan dinleyici:
        _hubConnection.On<string>("BarkodYazdir", async (istekId) =>
        {
            _logger.LogInformation("Web'den 'BarkodYazdir' emri alındı -> İstek #{IstekId}", istekId);
            var sonuc = _yaziciServisi.BarkodBas(istekId);
            await SonucBildirAsync(istekId, sonuc.Basarili, sonuc.Mesaj, _yaziciServisi.VarsayilanYazici);
        });

        // 2. Web'den hem İstek ID hem de özel hedef yazıcı adı birlikte gelirse:
        _hubConnection.On<BarkodEmirModel>("BarkodYazdirGelismi", async (emir) =>
        {
            string hedef = emir.HedefYazici ?? _yaziciServisi.VarsayilanYazici;
            _logger.LogInformation("Web'den 'BarkodYazdirGelismi' emri alındı -> İstek #{IstekId}, Hedef: '{Yazici}'",
                emir.IstekId, hedef);

            var sonuc = _yaziciServisi.BarkodBas(emir.IstekId, emir.HedefYazici);
            await SonucBildirAsync(emir.IstekId, sonuc.Basarili, sonuc.Mesaj, hedef);
        });

        // 3. Web ekranı "Bana güncel yazıcılarını tekrar gönder" dediğinde:
        _hubConnection.On("YazicilariGetir", async () =>
        {
            await YazicilariWebEBildirAsync();
        });

        try
        {
            await _hubConnection.StartAsync(stoppingToken);
            _logger.LogInformation("SignalR Hub bağlantısı başarıyla kuruldu! (Hub: {HubUrl})", _hubUrl);

            // Bağlantı kurulur kurulmaz masadaki tüm yazıcıları Web'e bildir (Web'deki dropdown dolsun)
            await YazicilariWebEBildirAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SignalR sunucusuna ilk bağlantı kurulamadı ({Hata}). Arka planda otomatik tekrar denenecek.", ex.Message);
        }
    }

    private async Task YazicilariWebEBildirAsync()
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return;

        try
        {
            var yazicilar = _yaziciYoneticisi.YukluYazicilariGetir();
            await _hubConnection.InvokeAsync("YazicilariBildir", yazicilar);
            _logger.LogInformation("{Sayi} adet yazıcı Web sistemine bildirildi.", yazicilar.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yazıcı listesi Web'e iletilemedi.");
        }
    }

    private async Task SonucBildirAsync(string istekId, bool basarili, string mesaj, string yazici)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return;

        try
        {
            var model = new BarkodSonucModel
            {
                IstekId = istekId,
                Basarili = basarili,
                Mesaj = mesaj,
                Yazici = yazici,
                Tarih = DateTime.Now
            };

            await _hubConnection.InvokeAsync("BarkodSonucBildir", model);
            _logger.LogInformation("[ACK İletildi] İstek #{IstekId} sonucu Web'e gönderildi (Başarılı: {Basarili})",
                istekId, basarili);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Baskı sonucu Web'e iletilemedi.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
