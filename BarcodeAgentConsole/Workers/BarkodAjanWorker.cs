using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
    private readonly int _baslangicTcpPort;

    private HubConnection? _hubConnection;
    private TcpListener? _tcpListener;
    private int _aktifTcpPort;
    private string _ajanAdi = "PC-A";
    private string _yerelIp = "127.0.0.1";
    private List<string> _oncekiYerelYazicilar = new();

    // Daha önce keşfedilen aktif Agent IP'lerini hafızada tutar (Öncelikli 10ms tarama ve anında bildirim için)
    private readonly ConcurrentDictionary<string, int> _bilinenAjanlar = new();
    private readonly SemaphoreSlim _taramaTetikleyici = new(0, 1);

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
        _baslangicTcpPort = int.TryParse(config["AgPaylasim:BaslangicTcpPort"], out int tPort) ? tPort : 5055;

        _yaziciYoneticisi.AnindaTaramaIstendi += TetikleAnindaTarama;
    }

    private void TetikleAnindaTarama()
    {
        try
        {
            if (_taramaTetikleyici.CurrentCount == 0)
            {
                _taramaTetikleyici.Release();
            }
        }
        catch (SemaphoreFullException) { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _yerelIp = YerelIpAdresiniBul();
        _oncekiYerelYazicilar = _yaziciYoneticisi.YerelYazicilariGetir();

        // 1. Kademe 2: Yerel Ağ (LAN) TCP Baskı Dinleyicisini, Hızlı Donanım İzleyicisini ve Hızlı Ağ Taramasını Başlat
        if (_agPaylasimAktif)
        {
            TcpBaskiDinleyicisiniBaslat(stoppingToken);
            _ = HizliDonanimTakipDongusuAsync(stoppingToken);
            _ = PeriyodikTcpAgTaramaDongusuAsync(stoppingToken);
        }

        _logger.LogInformation("🚀 Barkod Baskı Ajanı [{AjanAdi}] başlatıldı. Gerçek Ağ IP: {Ip}:{Port}, Hub: '{HubUrl}'",
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

    private AjanPaylasimPaketi KendiPaylasimPaketiniOlustur()
    {
        return new AjanPaylasimPaketi
        {
            AjanAdi = _ajanAdi,
            IpAdresi = _yerelIp,
            TcpPort = _aktifTcpPort,
            Yazicilar = _yaziciYoneticisi.AgPaylasimAcik ? _yaziciYoneticisi.YerelYazicilariGetir() : new List<string>(),
            SablonZamanDamgasi = _yaziciServisi.SablonZamanDamgasiniGetir()
        };
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

            // 2. İŞLEM (ÇİFT YÖNLÜ ANINDA CİHAZ KEŞFİ): Karşı bilgisayar bizdeki yazıcıları soruyorsa:
            if (istek.IslemTuru == "YazicilariVer")
            {
                // Karşı taraf kendi yazıcı paketini de gönderdiyse beklemeden ANINDA listemize ekle!
                if (istek.GonderenPaket != null &&
                    !(istek.GonderenPaket.AjanAdi == _ajanAdi && istek.GonderenPaket.TcpPort == _aktifTcpPort))
                {
                    string? karsiIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.MapToIPv4().ToString();
                    if (!string.IsNullOrWhiteSpace(karsiIp))
                    {
                        istek.GonderenPaket.IpAdresi = karsiIp;
                        int karsiPort = istek.GonderenPaket.TcpPort > 0 ? istek.GonderenPaket.TcpPort : _baslangicTcpPort;
                        if (karsiIp != "127.0.0.1")
                        {
                            _bilinenAjanlar[karsiIp] = karsiPort;
                        }
                    }

                    if (_yaziciYoneticisi.AgYazicilariniGuncelle(istek.GonderenPaket))
                    {
                        _logger.LogInformation("⚡ [ANINDA EŞLEŞME] {Ajan} ({Ip}:{Port}) bağlandı -> {Sayi} yazıcı anında eklendi!",
                            istek.GonderenPaket.AjanAdi, istek.GonderenPaket.IpAdresi, istek.GonderenPaket.TcpPort, istek.GonderenPaket.Yazicilar.Count);
                        _ = YazicilariWebEBildirAsync();
                    }

                    long bizdekiZaman = _yaziciServisi.SablonZamanDamgasiniGetir();
                    if (istek.GonderenPaket.SablonZamanDamgasi > bizdekiZaman && !string.IsNullOrWhiteSpace(istek.GonderenPaket.IpAdresi))
                    {
                        string sIp = istek.GonderenPaket.IpAdresi;
                        int sPort = istek.GonderenPaket.TcpPort;
                        string sAjan = istek.GonderenPaket.AjanAdi;
                        long sZaman = istek.GonderenPaket.SablonZamanDamgasi;
                        _ = Task.Run(() => AgdakiGuncelSablonuIndirAsync(sIp, sPort, sAjan, sZaman));
                    }
                }

                var paket = KendiPaylasimPaketiniOlustur();
                await writer.WriteLineAsync(JsonSerializer.Serialize(paket));
                return;
            }

            // 3. İŞLEM: Normal Ağ Baskı İsteği:
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
    /// Her 1.5 saniyede bir yerel USB portlarını yoklar. Yazıcı takıldığı veya çıkarıldığı saniye
    /// hem kendi Web arayüzünü hem de ağdaki bilinen diğer bilgisayarları ANINDA günceller!
    /// </summary>
    private async Task HizliDonanimTakipDongusuAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1500, stoppingToken);

                var guncelYerel = _yaziciYoneticisi.YerelYazicilariGetir();
                if (!_oncekiYerelYazicilar.SequenceEqual(guncelYerel))
                {
                    _oncekiYerelYazicilar = guncelYerel;
                    _logger.LogInformation("🔌 [DONANIM GÜNCELLEMESİ] Yerel yazıcı takıldı veya çıkarıldı. Anlık aktif: {Sayi} adet.", guncelYerel.Count);
                    await YazicilariWebEBildirAsync();

                    // Bilinen tüm ajanlara değişikliği milisaniyeler içinde bildir!
                    if (!_bilinenAjanlar.IsEmpty)
                    {
                        using var sem = new SemaphoreSlim(20);
                        var bildirimler = _bilinenAjanlar.ToArray()
                            .Select(kvp => TekilIpTcpTaraAsync(kvp.Key, kvp.Value, sem, stoppingToken));
                        await Task.WhenAll(bildirimler);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Donanım takip uyarısı: {Hata}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 2 AŞAMALI YILDIRIM HIZINDA TCP AĞ TARAMASI:
    /// 1. Aşama (20-50ms): Önce Windows ARP tablosundaki aktif cihazları ve bilinen ajanları tarar -> Yazıcılar ANINDA düşer!
    /// 2. Aşama (~1 sn): Geri kalan IP'leri 35'erli kontrollü paketlerle (Windows bağlantı kuyruğunu tıkamadan) tarar.
    /// </summary>
    private async Task PeriyodikTcpAgTaramaDongusuAsync(CancellationToken stoppingToken)
    {
        // Açılışta beklemeden (100ms içinde) ilk taramayı başlat!
        await Task.Delay(100, stoppingToken);
        bool ilkTarama = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var aktifYerelIpler = AktifYerelIpAdresleriniBul();
                if (aktifYerelIpler.Count > 0 && _yerelIp != aktifYerelIpler[0])
                {
                    _yerelIp = aktifYerelIpler[0];
                    Console.Title = $"FastReport Barkod Ajanı - {_ajanAdi} ({_yerelIp}:{_aktifTcpPort})";
                }

                using var semaphore = new SemaphoreSlim(35);

                // Aynı bilgisayarda test amaçlı açılmış 2. / 3. konsol pencereleri varsa (5055, 5056, 5057) anında kontrol et
                var yerelPortGorevleri = Enumerable.Range(_baslangicTcpPort, 3)
                    .Where(p => p != _aktifTcpPort)
                    .Select(p => TekilIpTcpTaraAsync("127.0.0.1", p, semaphore, stoppingToken));
                await Task.WhenAll(yerelPortGorevleri);

                // Fiziksel yerel ağ alt ağlarını tara (VirtualBox 192.168.56.1 gibi sanal ağlar elenmiştir)
                foreach (string yerelIp in aktifYerelIpler)
                {
                    if (!yerelIp.Contains('.')) continue;

                    string ipOnEk = yerelIp.Substring(0, yerelIp.LastIndexOf('.') + 1); // Örn: "192.168.1."

                    if (ilkTarama)
                    {
                        _logger.LogInformation("⚡ [HIZLI AĞ KEŞFİ] Gerçek ağ ({OnEk}1-254) taranıyor...", ipOnEk);
                    }

                    // AŞAMA 1: Bilinen Ajanlar + Windows ARP Önbelleğindeki Aktif Cihazlar (20-50 milisaniyede sonuç verir!)
                    var oncelikliIpler = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var bilinenIp in _bilinenAjanlar.Keys)
                    {
                        if (bilinenIp.StartsWith(ipOnEk, StringComparison.Ordinal) && bilinenIp != yerelIp)
                            oncelikliIpler.Add(bilinenIp);
                    }
                    foreach (var arpIp in ArpTablosundakiIpleriGetir(ipOnEk))
                    {
                        if (arpIp != yerelIp)
                            oncelikliIpler.Add(arpIp);
                    }

                    if (oncelikliIpler.Count > 0)
                    {
                        var oncelikliGorevler = oncelikliIpler
                            .Select(ip => TekilIpTcpTaraAsync(ip, _baslangicTcpPort, semaphore, stoppingToken));
                        await Task.WhenAll(oncelikliGorevler);
                    }

                    // AŞAMA 2: Kalan IP adreslerini Windows yarı-açık bağlantı kuyruğunu boğmadan (35'erli gruplarla) hızla tara
                    var kalanGorevler = Enumerable.Range(1, 254)
                        .Select(i => $"{ipOnEk}{i}")
                        .Where(ip => ip != yerelIp && !oncelikliIpler.Contains(ip))
                        .Select(hedefIp => TekilIpTcpTaraAsync(hedefIp, _baslangicTcpPort, semaphore, stoppingToken));

                    await Task.WhenAll(kalanGorevler);
                }

                ilkTarama = false;

                // Kapanan bilgisayarların yazıcılarını temizle (30 saniye boyunca cevap vermeyenler)
                if (_yaziciYoneticisi.ZamanAsiminaUgrayanAgYazicilariniTemizle(TimeSpan.FromSeconds(30)))
                {
                    await YazicilariWebEBildirAsync();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("TCP ağ taraması döngüsü uyarısı: {Hata}", ex.Message);
            }

            // 5 saniye bekle VEYA Web arayüzünden "Yenile / Yeni Bağlantı" sinyali gelirse ANINDA uyan!
            try
            {
                await _taramaTetikleyici.WaitAsync(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TekilIpTcpTaraAsync(string hedefIp, int hedefPort, SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            if (!IPAddress.TryParse(hedefIp, out var ipAddr))
                return;

            using var tcpClient = new TcpClient();
            // ÖNEMLİ: 260ms zamanlayıcı tüm 254 IP için aynı anda değil, SADECE sıra bu IP'ye geldiğinde başlar!
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            connectCts.CancelAfter(260);

            await tcpClient.ConnectAsync(ipAddr, hedefPort, connectCts.Token);

            using var ioCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            ioCts.CancelAfter(2000);

            using var stream = tcpClient.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // Karşı Agent'a hem "Yazıcılarını ver" de hem de BİZİM yazıcılarımızı aynı anda ilet!
            var talep = new AgBaskiIstekPaketi
            {
                GonderenAjan = _ajanAdi,
                IslemTuru = "YazicilariVer",
                GonderenPaket = KendiPaylasimPaketiniOlustur()
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(talep).AsMemory(), ioCts.Token);

            string? jsonCevap = await reader.ReadLineAsync(ioCts.Token);
            if (string.IsNullOrWhiteSpace(jsonCevap)) return;

            var paket = JsonSerializer.Deserialize<AjanPaylasimPaketi>(jsonCevap);
            if (paket == null) return;

            if (paket.AjanAdi == _ajanAdi && paket.TcpPort == _aktifTcpPort)
                return;

            // Karşı bilgisayarda sanal adaptör (VirtualBox vb.) olsa bile, ulaştığımız gerçek IP'yi esas al!
            paket.IpAdresi = hedefIp;
            if (hedefIp != "127.0.0.1")
            {
                _bilinenAjanlar[hedefIp] = hedefPort;
            }

            bool yeniYaziciVar = _yaziciYoneticisi.AgYazicilariniGuncelle(paket);
            if (yeniYaziciVar)
            {
                _logger.LogInformation("🎯 [CİHAZ BULUNDU] {Ajan} ({Ip}:{Port}) -> {Sayi} yazıcı anında eklendi!",
                    paket.AjanAdi, paket.IpAdresi, paket.TcpPort, paket.Yazicilar.Count);

                await YazicilariWebEBildirAsync();
            }

            // Şablon kontrolü de yap
            long bizdekiZaman = _yaziciServisi.SablonZamanDamgasiniGetir();
            if (paket.SablonZamanDamgasi > bizdekiZaman)
            {
                _ = Task.Run(() => AgdakiGuncelSablonuIndirAsync(paket.IpAdresi, paket.TcpPort, paket.AjanAdi, paket.SablonZamanDamgasi));
            }
        }
        catch
        {
            // Port kapalı veya zaman aşımı -> sessizce devam et
        }
        finally
        {
            semaphore.Release();
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

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

    /// <summary>
    /// Windows ARP önbelleğindeki aktif yerel ağ IP'lerini < 1 milisaniyede okur.
    /// Böylece ağdaki açık bilgisayarlar 1..254 sırasını beklemeden ilk 20ms içinde yakalanır!
    /// </summary>
    private static List<string> ArpTablosundakiIpleriGetir(string ipOnEk)
    {
        var ipler = new List<string>();
        int bytesNeeded = 0;
        try
        {
            GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);
            if (bytesNeeded <= 0) return ipler;

            bytesNeeded += 256;
            IntPtr buffer = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (GetIpNetTable(buffer, ref bytesNeeded, false) == 0)
                {
                    int entries = Marshal.ReadInt32(buffer, 0);
                    for (int i = 0; i < entries; i++)
                    {
                        int rowOffset = 4 + (i * 24);
                        int dwType = Marshal.ReadInt32(buffer, rowOffset + 20);
                        // 3 = Dynamic, 4 = Static (2 = Invalid)
                        if (dwType != 3 && dwType != 4) continue;

                        uint dwAddr = (uint)Marshal.ReadInt32(buffer, rowOffset + 16);
                        string ip = new IPAddress(dwAddr).ToString();

                        if (ip.StartsWith(ipOnEk, StringComparison.Ordinal) &&
                            !ip.EndsWith(".255", StringComparison.Ordinal) &&
                            !ip.EndsWith(".0", StringComparison.Ordinal))
                        {
                            if (!ipler.Contains(ip))
                                ipler.Add(ip);
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // ARP okunamazsa standart tarama zaten devrede
        }

        return ipler;
    }

    /// <summary>
    /// VirtualBox (192.168.56.1), Hyper-V, WSL, VMware gibi sanal adaptörleri eleyerek
    /// bilgisayarın gerçekten modeme/ağa bağlı olduğu fiziksel IPv4 adreslerini bulur.
    /// </summary>
    private static List<string> AktifYerelIpAdresleriniBul()
    {
        var sonuc = new List<string>();

        // 1. İşletim sisteminin gerçek dış ağ çıkışı için kullandığı birincil IP'yi bul (Ağa paket göndermez, 0.01ms sürer)
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                string ip = endPoint.Address.ToString();
                if (!string.IsNullOrWhiteSpace(ip) && ip != "127.0.0.1" && !ip.StartsWith("169.254.") && ip != "192.168.56.1")
                {
                    sonuc.Add(ip);
                }
            }
        }
        catch { }

        // 2. Fiziksel ağ kartlarını kontrol et (Gateway'i olan gerçek Wi-Fi / Ethernet kartlarına öncelik ver)
        try
        {
            string[] sanalKelimeler = { "virtual", "vmware", "vbox", "hyper-v", "vethernet", "wsl", "docker", "loopback", "tunnel", "tap-", "vpn", "zerotier", "tailscale" };

            var fizikselGatewayli = new List<string>();
            var fizikselGatewaysiz = new List<string>();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                string adAciklama = $"{ni.Name} {ni.Description}".ToLowerInvariant();
                if (sanalKelimeler.Any(k => adAciklama.Contains(k))) continue;

                var ipProps = ni.GetIPProperties();
                bool gatewayVar = ipProps.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.Equals(IPAddress.Any));

                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = ua.Address.ToString();
                    if (ip == "127.0.0.1" || ip.StartsWith("169.254.") || ip == "192.168.56.1") continue;

                    if (gatewayVar)
                        fizikselGatewayli.Add(ip);
                    else
                        fizikselGatewaysiz.Add(ip);
                }
            }

            foreach (var ip in fizikselGatewayli)
                if (!sonuc.Contains(ip)) sonuc.Add(ip);

            if (sonuc.Count == 0)
            {
                foreach (var ip in fizikselGatewaysiz)
                    if (!sonuc.Contains(ip)) sonuc.Add(ip);
            }
        }
        catch { }

        return sonuc;
    }

    private static string YerelIpAdresiniBul()
    {
        return AktifYerelIpAdresleriniBul().FirstOrDefault() ?? "127.0.0.1";
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
                agPaylasimAcik = _yaziciYoneticisi.AgPaylasimAcik,
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
        _yaziciYoneticisi.AnindaTaramaIstendi -= TetikleAnindaTarama;
        _tcpListener?.Stop();
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
