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

        // 2. Yazıcıyı otomatik belirle
        string secilenYazici = OtomatikYaziciBelirle(configYazici);

        // 3. Baskı Servisini Başlat
        _yaziciServisi = new BarkodYaziciServisi(secilenYazici, postgresConn);

        // 4. SignalR Dinleyicisini Başlat
        await SignalRBaslatAsync(hubUrl);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [AKTİF] Ajan arka planda çalışıyor.");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [VARSAYILAN YAZICI] '{secilenYazici}'");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DİNLENİYOR] SignalR Hub: '{hubUrl}'");
        Console.ResetColor();
        Console.WriteLine("------------------------------------------------------------------");

        // 5. Arka planda kesintisiz çalışma
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

    private static string OtomatikYaziciBelirle(string configYazici)
    {
        if (!string.IsNullOrWhiteSpace(configYazici))
            return configYazici;

        try
        {
            var printDoc = new PrintDocument();
            string varsayilan = printDoc.PrinterSettings.PrinterName;
            if (!string.IsNullOrWhiteSpace(varsayilan))
                return varsayilan;
        }
        catch { }

        foreach (string printer in PrinterSettings.InstalledPrinters)
            return printer;

        return string.Empty;
    }

    private static async Task SignalRBaslatAsync(string hubUrl)
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Web'den SADECE İstek ID (örn: "504" veya "100") geldiğinde çalışan ana dinleyici:
            _hubConnection.On<string>("BarkodYazdir", (istekId) =>
            {
                _yaziciServisi?.BarkodBas(istekId);
            });

            // Web'den hem İstek ID hem de hedef yazıcı adı birlikte gelirse:
            _hubConnection.On<BarkodEmirModel>("BarkodYazdirGelismi", (emir) =>
            {
                _yaziciServisi?.BarkodBas(emir.IstekId, emir.HedefYazici);
            });

            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [UYARI] SignalR henüz hazır değil ({ex.Message}). Arka planda otomatik tekrar denenecek.");
            Console.ResetColor();
        }
    }
}
