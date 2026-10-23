using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BarcodeAgentConsole.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Workers;

/// <summary>
/// Arka planda kesintisiz çalışan, SignalR soket bağlantısını yöneten,
/// yerel ağdaki (LAN) diğer Agent'larla yazıcı paylaşımı yapan ve gelen
/// baskı emirlerini karşılayıp yazıcı servisine yönlendiren ana işçi servis (Worker).
/// </summary>
public class BarkodAjanWorker : BackgroundService
{
    private readonly ILogger<BarkodAjanWorker> _logger;
    private readonly IYaziciServisi _yaziciServisi;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;
    private readonly string _hubUrl;
    private readonly bool _agPaylasimAktif;
    private readonly int _udpBroadcastPort;
    private readonly int _baslangicTcpPort;

    private HubConnection? _hubConnection;
    private TcpListener? _tcpListener;
    private int _aktifTcpPort;
    private string _ajanAdi = "PC-A";
    private string _yerelIp = "127.0.0.1";

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
        _agPaylasimAktif = bool.TryParse(config["AgPaylasim:Aktif"], out bool aktif) ? aktif : true;
        _udpBroadcastPort = int.TryParse(config["AgPaylasim:UdpPort"], out int uPort) ? uPort : 5050;
        _baslangicTcpPort = int.TryParse(config["AgPaylasim:BaslangicTcpPort"], out int tPort) ? tPort : 5055;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _yerelIp = YerelIpAdresiniBul();

        // 1. Kademe 2: Yerel Ağ (LAN) TCP Baskı Dinleyicisini ve UDP Paylaşımını Başlat
        if (_agPaylasimAktif)
        {
            TcpBaskiDinleyicisiniBaslat(stoppingToken);
            _ = UdpYaziciDinleyicisiniBaslatAsync(stoppingToken);
            _ = PeriyodikYaziciFirlatmaDongusuAsync(stoppingToken);
        }

        _logger.LogInformation("Barkod Baskı Ajanı [{AjanAdi}] başlatıldı. IP: {Ip}:{Port}, Hub: '{HubUrl}'",
            _ajanAdi, _yerelIp, _aktifTcpPort, _hubUrl);

        // 2. Web / SignalR Bağlantısını Başlat
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

    /// <summary>
    /// 2. KADEME - ADIM 1: Diğer bilgisayardan (örn. PC-B'den) gelen yazdırma isteklerini dinleyen TCP portu açar.
    /// Aynı bilgisayarda iki konsol açılırsa 1. pencere 5055 (PC-A), 2. pencere otomatik 5056 (PC-B) olur.
    /// </summary>
    private void TcpBaskiDinleyicisiniBaslat(CancellationToken stoppingToken)
    {
        for (int i = 0; i < 10; i++)
        {
            int denenenPort = _baslangicTcpPort + i;
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, denenenPort);
                _tcpListener.Start();
                _aktifTcpPort = denenenPort;
                _ajanAdi = $"PC-{(char)('A' + i)}";
                Console.Title = $"FastReport Barkod Ajanı - {_ajanAdi} ({_yerelIp}:{_aktifTcpPort})";
                break;
            }
            catch (SocketException)
            {
                // Bu port doluysa (örn. aynı PC'de PC-A açıksa) bir sonraki portu (PC-B) dene
            }
        }

        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested && _tcpListener != null)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(stoppingToken);
                    _ = Task.Run(() => GelenAgBaskiIsteginiIsleAsync(client), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("TCP dinleyici uyarısı: {Hata}", ex.Message);
                }
            }
        }, stoppingToken);
    }

    private async Task GelenAgBaskiIsteginiIsleAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            string? json = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(json)) return;

            var istek = JsonSerializer.Deserialize<AgBaskiIstekPaketi>(json);
            if (istek == null) return;

            _logger.LogInformation("📥 [AĞDAN BASKI GELDİ] Gönderen: {Gonderen} -> İstek #{IstekId}, Hedef Yazıcı: '{Yazici}'",
                istek.GonderenAjan, istek.IstekId, istek.HedefYazici);

            var sonuc = _yaziciServisi.BarkodBas(istek.IstekId, istek.HedefYazici);

            var yanit = new BarkodSonucModel
            {
                IstekId = istek.IstekId,
                Basarili = sonuc.Basarili,
                Mesaj = $"{_ajanAdi} üzerinden basıldı: {sonuc.Mesaj}",
                Yazici = istek.HedefYazici,
                Tarih = DateTime.Now
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(yanit));
        }
    }

    /// <summary>
    /// 2. KADEME - ADIM 2: Her 5 saniyede bir kendi IP adresini ve bağlı yazıcılarını yerel ağa (UDP) fırlatır.
    /// </summary>
    private async Task PeriyodikYaziciFirlatmaDongusuAsync(CancellationToken stoppingToken)
    {
        using var udpSender = new UdpClient();
        udpSender.EnableBroadcast = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var paket = new AjanPaylasimPaketi
                {
                    AjanAdi = _ajanAdi,
                    IpAdresi = _yerelIp,
                    TcpPort = _aktifTcpPort,
                    Yazicilar = _yaziciYoneticisi.YerelYazicilariGetir()
                };

                byte[] veri = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(paket));

                // 1. Yerel ağdaki (LAN) diğer bilgisayarlara Broadcast olarak fırlat
                await udpSender.SendAsync(veri, veri.Length, new IPEndPoint(IPAddress.Broadcast, _udpBroadcastPort));

                // 2. Aynı bilgisayarda test yaparken diğer pencerelere (5055, 5056, 5057) doğrudan fırlat
                for (int i = 0; i < 4; i++)
                {
                    int hedefPort = _baslangicTcpPort + i;
                    if (hedefPort != _aktifTcpPort)
                    {
                        await udpSender.SendAsync(veri, veri.Length, new IPEndPoint(IPAddress.Loopback, hedefPort));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("UDP yazıcı fırlatma uyarısı: {Hata}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    /// <summary>
    /// 2. KADEME - ADIM 3: Ağdaki diğer Agent'lardan fırlatılan yazıcı paketlerini yakalar ve listeye ekler.
    /// </summary>
    private async Task UdpYaziciDinleyicisiniBaslatAsync(CancellationToken stoppingToken)
    {
        // Hem genel Broadcast portunu (5050) hem de aynı PC testi için kendi portunu dinle
        _ = DinleUdpPortAsync(_udpBroadcastPort, true, stoppingToken);
        _ = DinleUdpPortAsync(_aktifTcpPort, false, stoppingToken);
        await Task.CompletedTask;
    }

    private async Task DinleUdpPortAsync(int port, bool reuseAddress, CancellationToken stoppingToken)
    {
        try
        {
            using var udpListener = new UdpClient();
            if (reuseAddress)
            {
                udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            while (!stoppingToken.IsCancellationRequested)
            {
                var sonuc = await udpListener.ReceiveAsync(stoppingToken);
                string json = Encoding.UTF8.GetString(sonuc.Buffer);
                var paket = JsonSerializer.Deserialize<AjanPaylasimPaketi>(json);

                if (paket == null) continue;

                // Kendi gönderdiğimiz paketi yoksay
                if (paket.TcpPort == _aktifTcpPort && (paket.IpAdresi == _yerelIp || paket.IpAdresi == "127.0.0.1"))
                    continue;

                // Diğer bilgisayardan gelen yazıcıları listeye ekle
                bool yeniYaziciEklendi = _yaziciYoneticisi.AgYazicilariniGuncelle(paket);
                if (yeniYaziciEklendi)
                {
                    // Yeni yazıcı bulunduğunda Web arayüzündeki dropdown'ı anında güncelle!
                    await YazicilariWebEBildirAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal kapanış
        }
        catch (Exception ex)
        {
            _logger.LogWarning("UDP Dinleyici ({Port}) uyarısı: {Hata}", port, ex.Message);
        }
    }

    private static string YerelIpAdresiniBul()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            return ip?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
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

            // Aynı bilgisayarda test ederken: Seçilen yazıcı "[PC-A | ...]" şeklinde bir ağ yazıcısıysa
            // ve bu pencerenin kendisi PC-A ise, emri PC-B'nin TCP üzerinden yönlendirmesini bekle.
            if (hedef.StartsWith($"[{_ajanAdi} |"))
            {
                return;
            }

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
        _tcpListener?.Stop();
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
