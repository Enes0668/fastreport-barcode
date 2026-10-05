using System;
using System.Collections.Generic;
using System.Drawing.Printing;
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
        Console.Title = "FastReport Barkod Baskı Ajanı (Console)";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine("    🏥 FASTREPORT BARKOD & ETİKET BASKI AJANI (CONSOLE ENGINE)   ");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        // 1. Konfigürasyonu yükle
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        string hubUrl = config["SignalR:HubUrl"] ?? "http://localhost:5000/barkodHub";
        string configYazici = config["Printer:Name"] ?? string.Empty;
        string postgresConn = config["ConnectionStrings:PostgreSql"] ?? string.Empty;

        // 2. Yazıcı Seçimi (Konsol üzerinden dinamik)
        string secilenYazici = YaziciSec(configYazici);

        // 3. Baskı Servisini Başlat
        _yaziciServisi = new BarkodYaziciServisi(secilenYazici, postgresConn);

        // 4. SignalR Dinleyicisini Başlat
        await SignalRBaslatAsync(hubUrl);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Komutlar: [T] Manuel Test Baskısı Gönder | [C] Ekranı Temizle | [Q] Çıkış");
        Console.ResetColor();
        Console.WriteLine("------------------------------------------------------------------");

        // 5. Konsol Dinleme Döngüsü
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Q)
            {
                Console.WriteLine("\nUygulama kapatılıyor...");
                if (_hubConnection != null)
                    await _hubConnection.StopAsync();
                break;
            }
            else if (key.Key == ConsoleKey.T)
            {
                ManuelTestBaskisiYap();
            }
            else if (key.Key == ConsoleKey.C)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[AKTİF] Yazıcı: '{_yaziciServisi.HedefYazici}' | SignalR: '{hubUrl}'");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Bilgisayardaki yazıcıları konsolda listeler ve seçim yaptırır.
    /// </summary>
    private static string YaziciSec(string configYazici)
    {
        var yazicilar = new List<string>();
        foreach (string p in PrinterSettings.InstalledPrinters)
            yazicilar.Add(p);

        if (yazicilar.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("⚠️ Sistemde kurulu hiçbir yazıcı bulunamadı!");
            Console.ResetColor();
            return string.Empty;
        }

        // Eğer config dosyasında yazıcı adı varsa ve sistemde mevcutsa otomatik seç:
        if (!string.IsNullOrWhiteSpace(configYazici) && yazicilar.Contains(configYazici))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ appsettings.json'daki yazıcı otomatik seçildi: '{configYazici}'");
            Console.ResetColor();
            return configYazici;
        }

        string varsayilan = string.Empty;
        try
        {
            varsayilan = new PrintDocument().PrinterSettings.PrinterName;
        }
        catch { }

        Console.WriteLine("\nKurulu Yazıcılar:");
        int secimIndex = 0;
        for (int i = 0; i < yazicilar.Count; i++)
        {
            bool isDefault = yazicilar[i].Equals(varsayilan, StringComparison.OrdinalIgnoreCase);
            if (isDefault) secimIndex = i;

            Console.WriteLine($"  [{i + 1}] {yazicilar[i]} {(isDefault ? "(Windows Varsayılan)" : "")}");
        }

        Console.Write($"\nHedef barkod yazıcısını seçin [Varsayılan: {secimIndex + 1}]: ");
        string? giris = Console.ReadLine();

        if (int.TryParse(giris, out int secilenNo) && secilenNo >= 1 && secilenNo <= yazicilar.Count)
        {
            secimIndex = secilenNo - 1;
        }

        string secilen = yazicilar[secimIndex];
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Hedef Yazıcı Belirlendi: '{secilen}'\n");
        Console.ResetColor();
        return secilen;
    }

    /// <summary>
    /// SignalR bağlantısını kurar ve dışarıdan gelen emirleri dinler.
    /// </summary>
    private static async Task SignalRBaslatAsync(string hubUrl)
    {
        try
        {
            Console.Write($"📡 SignalR Hub'a bağlanılıyor ({hubUrl})... ");

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

            // 3. POSTGRESQL SORGUSUYLA N ADET BARKOD EMRİ
            _hubConnection.On<string>("PostgresBarkodYazdir", (istekId) =>
            {
                _yaziciServisi?.PostgresIleBarkodBas(istekId);
            });

            await _hubConnection.StartAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[BAĞLANDI]");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[BEKLEMEDE] Hub henüz aktif değil ({ex.Message}). Arka planda otomatik yeniden denenecek.");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Konsoldan 'T' tuşuna basıldığında test amaçlı N adet barkod basar.
    /// </summary>
    private static void ManuelTestBaskisiYap()
    {
        Console.WriteLine("\n[TEST] 3 adet kan tüpü etiketi üretiliyor...");
        var testListesi = new List<BarkodIstekModel>
        {
            new() { BarkodNo = "869012345001", HastaAdi = "Ahmet Yılmaz", Bolum = "Dahiliye (Kırmızı Tüp)", ProtokolNo = "2026-98451" },
            new() { BarkodNo = "869012345002", HastaAdi = "Ahmet Yılmaz", Bolum = "Hemogram (Mor Tüp)", ProtokolNo = "2026-98451" },
            new() { BarkodNo = "869012345003", HastaAdi = "Ahmet Yılmaz", Bolum = "Sedimantasyon", ProtokolNo = "2026-98451" }
        };

        _yaziciServisi?.TopluBarkodBas(testListesi);
    }
}
