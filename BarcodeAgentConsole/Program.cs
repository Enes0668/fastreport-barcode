using System;
using System.Drawing.Printing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace BarcodeAgentConsole;

internal class Program
{
    private static HubConnection? _hubConnection;
    private static BarkodYaziciServisi? _yaziciServisi;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "FastReport Barkod Baskı Ajanı (Background Agent)";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine("        🏥 FASTREPORT BARKOD BASKI AJANI (BACKGROUND WORKER)      ");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        // 1. Konfigürasyonu yükle
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        string hubUrl = config["SignalR:HubUrl"] ?? "http://localhost:5000/barkodHub";
        string configYazici = config["Printer:Name"] ?? string.Empty;
        string postgresConn = config["ConnectionStrings:PostgreSql"] ?? string.Empty;

        // 2. Yazıcıyı OTOMATİK belirle (Konsolda soru sormaz, kullanıcı beklemez!)
        string secilenYazici = OtomatikYaziciBelirle(configYazici);

        // 3. Baskı Servisini Başlat
        _yaziciServisi = new BarkodYaziciServisi(secilenYazici, postgresConn);

        // 4. SignalR Dinleyicisini Başlat
        await SignalRBaslatAsync(hubUrl);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [AKTİF] Ajan arka planda çalışıyor.");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [HEDEF YAZICI] '{secilenYazici}'");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DİNLENİYOR] SignalR Hub: '{hubUrl}'");
        Console.ResetColor();
        Console.WriteLine("------------------------------------------------------------------");

        // 5. Arka planda kesintisiz çalışma (Kullanıcı girişi beklemeden servisi canlı tutar)
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("\n[KAPANIYOR] Baskı ajanı kapatıldı.");
        }
        finally
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }

    /// <summary>
    /// Hiçbir kullanıcı etkileşimi olmadan yazıcıyı otomatik belirler:
    /// 1. appsettings.json içindeki yazıcı
    /// 2. Windows'un varsayılan kurulu yazıcısı
    /// 3. Sistemdeki ilk kurulu yazıcı
    /// </summary>
    private static string OtomatikYaziciBelirle(string configYazici)
    {
        // 1. Config dosyasında tanımlı yazıcı varsa onu al
        if (!string.IsNullOrWhiteSpace(configYazici))
        {
            Console.WriteLine($"✓ appsettings.json dosyasından okundu: '{configYazici}'");
            return configYazici;
        }

        // 2. Yoksa Windows'un varsayılan yazıcısını al
        try
        {
            var printDoc = new PrintDocument();
            string varsayilan = printDoc.PrinterSettings.PrinterName;
            if (!string.IsNullOrWhiteSpace(varsayilan))
            {
                Console.WriteLine($"✓ Windows varsayılan yazıcısı otomatik seçildi: '{varsayilan}'");
                return varsayilan;
            }
        }
        catch { }

        // 3. O da bulunamazsa sistemdeki ilk kurulu yazıcıyı al
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            Console.WriteLine($"✓ İlk kurulu yazıcı otomatik seçildi: '{printer}'");
            return printer;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("⚠️ Sistemde kurulu yazıcı bulunamadı!");
        Console.ResetColor();
        return string.Empty;
    }

    /// <summary>
    /// SignalR bağlantısını kurar ve arka planda gelen baskı emirlerini dinler.
    /// </summary>
    private static async Task SignalRBaslatAsync(string hubUrl)
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // 1. TEKLİ BARKOD EMRİ
            _hubConnection.On<BarkodIstekModel>("BarkodYazdir", (istek) =>
            {
                _yaziciServisi?.TekliBarkodBas(istek);
            });

            // 2. N ADET TOPLU BARKOD EMRİ
            _hubConnection.On<List<BarkodIstekModel>>("BarkodYazdirToplu", (istekListesi) =>
            {
                _yaziciServisi?.TopluBarkodBas(istekListesi);
            });

            // 3. POSTGRESQL SORGUSUYLA BARKOD EMRİ
            _hubConnection.On<string>("PostgresBarkodYazdir", (istekId) =>
            {
                _yaziciServisi?.PostgresIleBarkodBas(istekId);
            });

            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [UYARI] SignalR henüz hazır değil ({ex.Message}). Arka planda otomatik tekrar bağlanacak.");
            Console.ResetColor();
        }
    }
}
