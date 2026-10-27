using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BarcodeAgentConsole.Hubs;
using BarcodeAgentConsole.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarcodeAgentConsole.Workers;

/// <summary>
/// Arka planda kesintisiz çalışan, gömülü SignalR Hub'ı ve yerel ağdaki (LAN) diğer Agent'larla
/// yazıcı paylaşımını yöneten, gelen baskı emirlerini karşılayıp yazıcı servisine yönlendiren ana işçi servis (Worker).
/// </summary>
public class BarkodAjanWorker : BackgroundService
{
    private readonly ILogger<BarkodAjanWorker> _logger;
    private readonly IYaziciServisi _yaziciServisi;
    private readonly IYaziciYoneticisi _yaziciYoneticisi;
    private readonly IHubContext<YerelBarkodHub> _yerelHubContext;
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
        IHubContext<YerelBarkodHub> yerelHubContext,
        IConfiguration config)
    {
        _logger = logger;
        _yaziciServisi = yaziciServisi;
        _yaziciYoneticisi = yaziciYoneticisi;
        _yerelHubContext = yerelHubContext;
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
                _ajanAdi = i == 0 ? Environment.MachineName : $"{Environment.MachineName}-{(char)('A' + i)}";

                _logger.LogInformation("🚀 [AJAN BAŞLATILDI] Bilgisayar: '{AjanAdi}', Yerel Port: {Port}", _ajanAdi, _aktifTcpPort);

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

            // 1. İŞLEM: Karşı bilgisayar bizdeki güncel .frx şablonunu istiyorsa:
            if (istek.IslemTuru == "SablonTalep")
            {
                string sablonIcerik = _yaziciServisi.SablonIceriginiGetir();
                long zamanDamgasi = _yaziciServisi.SablonZamanDamgasiniGetir();

                var sablonPaketi = new
                {
                    ZamanDamgasi = zamanDamgasi,
                    Icerik = sablonIcerik
                };

                await writer.WriteLineAsync(JsonSerializer.Serialize(sablonPaketi));
                _logger.LogInformation("📤 [ŞABLON GÖNDERİLDİ] {Gonderen} ajanı bizdeki güncel .frx şablonunu indirdi.", istek.GonderenAjan);
                return;
            }

            // 2. İŞLEM: Normal Ağ Baskı İsteği:
            _logger.LogInformation("📥 [AĞDAN BASKI GELDİ] Gönderen: {Gonderen} -> İstek #{IstekId}, Hedef Yazıcı: '{Yazici}'",
                istek.GonderenAjan, istek.IstekId, istek.HedefYazici);

            var sonuc = _yaziciServisi.BarkodBas(istek.IstekId, istek.HedefYazici, istek.Parametreler);

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
    /// 2. KADEME - ADIM 2: Her 2 saniyede bir kendi IP adresini ve bağlı yazıcılarını yerel ağa (UDP) fırlatır
    /// ve kapanan (sinyali kesilen) diğer bilgisayarların yazıcılarını (7 sn timeout ile) listeden temizler.
    /// </summary>
    private async Task PeriyodikYaziciFirlatmaDongusuAsync(CancellationToken stoppingToken)
    {
        using var udpSender = new UdpClient();
        udpSender.EnableBroadcast = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Kapanan veya bağlantısı kopan bilgisayarların yazıcılarını temizle (Cache'de kalmasın!)
                if (_yaziciYoneticisi.ZamanAsiminaUgrayanAgYazicilariniTemizle(TimeSpan.FromSeconds(6)))
                {
                    await YazicilariWebEBildirAsync();
                }

                // 2. Kendi yazıcılarımızı (Paylaşım açıksa) ağa fırlat; paylaşım kapalıysa boş liste fırlat ki karşı taraf anında silsin
                var gonderilecekYazicilar = _yaziciYoneticisi.UdpPaylasimAcik
                    ? _yaziciYoneticisi.YerelYazicilariGetir()
                    : new System.Collections.Generic.List<string>();

                var paket = new AjanPaylasimPaketi
                {
                    AjanAdi = _ajanAdi,
                    IpAdresi = _yerelIp,
                    TcpPort = _aktifTcpPort,
                    Yazicilar = gonderilecekYazicilar,
                    SablonZamanDamgasi = _yaziciServisi.SablonZamanDamgasiniGetir()
                };

                byte[] veri = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(paket));

                // Yerel ağdaki (LAN) diğer bilgisayarlara Broadcast olarak fırlat
                await udpSender.SendAsync(veri, veri.Length, new IPEndPoint(IPAddress.Broadcast, _udpBroadcastPort));

                // Aynı bilgisayarda test yaparken diğer pencerelere (5055, 5056, 5057) doğrudan fırlat
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

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
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
                udpListener.ExclusiveAddressUse = false;
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

                // 1. ŞABLON SENKRONİZASYONU: Karşı bilgisayarda bizden daha yeni bir şablon var mı?
                long bizdekiZaman = _yaziciServisi.SablonZamanDamgasiniGetir();
                if (paket.SablonZamanDamgasi > bizdekiZaman)
                {
                    _ = Task.Run(() => AgdakiGuncelSablonuIndirAsync(paket.IpAdresi, paket.TcpPort, paket.AjanAdi, paket.SablonZamanDamgasi));
                }

                // 2. YAZICI LİSTESİ: Diğer bilgisayardan gelen yazıcıları listeye ekle
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

    /// <summary>
    /// Ağdaki başka bir bilgisayarda (örn. tasarımcının kaydettiği PC'de) daha yeni bir .frx şablonu algılandığında,
    /// TCP üzerinden o şablonu kayıpsız indirir ve yerel klasöründeki etiket_sablonu.frx dosyasını günceller.
    /// </summary>
    private async Task AgdakiGuncelSablonuIndirAsync(string hedefIp, int hedefPort, string kaynakAjan, long yeniZamanDamgasi)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(hedefIp, hedefPort);

            using var stream = tcpClient.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var talepPaketi = new AgBaskiIstekPaketi
            {
                GonderenAjan = _ajanAdi,
                IslemTuru = "SablonTalep"
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(talepPaketi));

            string? gelenJson = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(gelenJson)) return;

            using var doc = JsonDocument.Parse(gelenJson);
            if (doc.RootElement.TryGetProperty("Icerik", out var icerikElem))
            {
                string xmlIcerik = icerikElem.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(xmlIcerik))
                {
                    bool guncellendi = _yaziciServisi.SablonuGuncelle(xmlIcerik, yeniZamanDamgasi);
                    if (guncellendi)
                    {
                        _logger.LogInformation("✨ [ŞABLON EŞİTLENDİ] {KaynakAjan} bilgisayarındaki yeni .frx şablonu bu bilgisayara otomatik eşitlendi!", kaynakAjan);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Şablon senkronizasyonu denenirken uyarı: {Hata}", ex.Message);
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

            // Seçilen yazıcı "[AjanAdi | IP:Port]" şeklinde bu pencerenin kendisine aitse,
            // emri diğer bilgisayarın (veya diğer pencerenin) TCP üzerinden yönlendirmesini bekle.
            if (hedef.StartsWith($"[{_ajanAdi} | {_yerelIp}:{_aktifTcpPort}]"))
            {
                return;
            }

            _logger.LogInformation("Web'den 'BarkodYazdirGelismi' emri alındı -> İstek #{IstekId}, Hedef: '{Yazici}'",
                emir.IstekId, hedef);

            var sonuc = _yaziciServisi.BarkodBas(emir.IstekId, emir.HedefYazici, emir.Parametreler);
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
        var yazicilar = _yaziciYoneticisi.YukluYazicilariGetir();

        // 1. Agent'ın kendi içindeki Gömülü (Yerel) SignalR Hub'a bağlı tüm HBYS web ekranlarına anında bildir:
        try
        {
            await _yerelHubContext.Clients.All.SendAsync("YazicilarAlindi", new
            {
                connectionId = _ajanAdi,
                yazicilar = yazicilar,
                yerelYaziciYokModu = _yaziciYoneticisi.YerelYaziciYokSimulasyonu,
                udpPaylasimAcik = _yaziciYoneticisi.UdpPaylasimAcik,
                zaman = DateTime.Now.ToString("HH:mm:ss")
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Yerel Hub'a yazıcı listesi bildirilirken uyarı.");
        }

        // 2. Dış bir Hub'a bağlıysa oraya da bildir:
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected) return;

        try
        {
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
