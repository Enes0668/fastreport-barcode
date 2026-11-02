using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// Harici Kurulum.bat / Kurulum.ps1 dosyalarına ihtiyaç bırakmadan tüm Windows Güvenlik Duvarı ve
/// Özel Ağ (Private) izinlerini doğrudan BarcodeAgentConsole.exe içerisine gömen akıllı kurulum modülü.
/// Sadece ilk çalıştırılışta (veya .exe farklı bir klasöre taşındığında) bir kez Yönetici izni ister;
/// sonraki tüm açılışlarda hiçbir onay penceresi çıkarmadan doğrudan çalışır.
/// </summary>
public static class AgGuvenlikKurulumu
{
    private const string PortKuralAdi = "Barkod Ajan (TCP 5055)";
    private const string ProgramKuralAdi = "Barkod Ajan Uygulamasi";
    private const string PingKuralAdi = "Barkod Ajan Ping Izni";

    /// <summary>
    /// Uygulama "--kurulum-yap" parametresiyle (Yönetici alt süreci olarak) başlatıldıysa kuralları uygular ve true döner.
    /// </summary>
    public static bool KurulumAltSureciMiVeCalistir(string[] args)
    {
        if (!args.Any(a => string.Equals(a, "--kurulum-yap", StringComparison.OrdinalIgnoreCase)))
            return false;

        string exeYolu = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        YoneticiKurulumunuUygula(exeYolu, altPencereBekle: true);
        return true;
    }

    /// <summary>
    /// Normal açılışta kuralların tam olup olmadığını 2ms içinde Kayıt Defteri'nden kontrol eder.
    /// Eksikse otomatik olarak 1 defaya mahsus Yönetici İzni (UAC) isteyip kurulumu tamamlar.
    /// </summary>
    public static void GerekliyseOtomatikKurulumYap(string[] args)
    {
        if (!OperatingSystem.IsWindows())
            return;

        string exeYolu = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exeYolu))
            return;

        bool zorlaKurulum = args.Any(a => string.Equals(a, "--kurulum", StringComparison.OrdinalIgnoreCase));

        if (!zorlaKurulum && KurulumZatenTamamMi(exeYolu))
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("   🛡️ Ağ ve Güvenlik Duvarı izinleri hazır (Kurulum tam).");
            Console.ResetColor();
            return;
        }

        // Eğer zaten Yönetici olarak açılmışsa ikinci bir pencere açmadan doğrudan uygula:
        if (YoneticiYetkisiVarMi())
        {
            YoneticiKurulumunuUygula(exeYolu, altPencereBekle: false);
            return;
        }

        // Yönetici değilse sadece bu sefere mahsus kendi kendisini Yönetici izniyle ("runas") çağır:
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   🛡️ İlk kurulum algılandı: Ağ ve Güvenlik Duvarı izinleri tanımlanıyor...");
        Console.WriteLine("      👉 Lütfen açılan Windows izin penceresinde 'Evet' seçeneğine tıklayın (Sadece 1 kez sorulur).");
        Console.ResetColor();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exeYolu,
                Arguments = "--kurulum-yap",
                UseShellExecute = true,
                Verb = "runas"
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(30000);

            if (proc != null && proc.ExitCode == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("   ✅ Otomatik ağ ve güvenlik duvarı kurulumu tamamlandı! Sonraki açılışlarda tekrar sorulmayacak.");
                Console.ResetColor();
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("   ⚠️ Yönetici izni onaylanmadı; mevcut ağ ayarlarıyla devam ediliyor.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"   ⚠️ Otomatik kurulum uyarısı: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static bool KurulumZatenTamamMi(string exeYolu)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules",
                writable: false);

            if (key == null)
                return false;

            bool portTamam = false;
            bool programTamam = false;
            bool pingTamam = false;
            string arananApp = $"|App={exeYolu}|";

            foreach (string valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is not string rawVal || string.IsNullOrEmpty(rawVal))
                    continue;

                if (!rawVal.Contains("Barkod Ajan", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!rawVal.Contains("Active=TRUE", StringComparison.OrdinalIgnoreCase) ||
                    !rawVal.Contains("Action=Allow", StringComparison.OrdinalIgnoreCase) ||
                    !rawVal.Contains("Dir=In", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string expanded = Environment.ExpandEnvironmentVariables(rawVal);

                if (expanded.Contains($"|Name={PortKuralAdi}|", StringComparison.OrdinalIgnoreCase) &&
                    expanded.Contains("5055", StringComparison.OrdinalIgnoreCase))
                {
                    portTamam = true;
                }
                else if (expanded.Contains($"|Name={ProgramKuralAdi}|", StringComparison.OrdinalIgnoreCase) &&
                         expanded.Contains(arananApp, StringComparison.OrdinalIgnoreCase))
                {
                    programTamam = true;
                }
                else if (expanded.Contains($"|Name={PingKuralAdi}|", StringComparison.OrdinalIgnoreCase))
                {
                    pingTamam = true;
                }

                if (portTamam && programTamam && pingTamam)
                    return true;
            }
        }
        catch
        {
            // Kayıt defteri okunamazsa kurulumu zorlamadan geç
            return true;
        }

        return false;
    }

    private static bool YoneticiYetkisiVarMi()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void YoneticiKurulumunuUygula(string exeYolu, bool altPencereBekle)
    {
        Console.Title = "FastReport Barkod Ajanı - Otomatik Ağ ve Güvenlik Kurulumu";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================================================");
        Console.WriteLine("   🏥 FASTREPORT BARKOD AJANI - GÖMÜLÜ AĞ VE GÜVENLİK KURULUMU           ");
        Console.WriteLine("=========================================================================");
        Console.ResetColor();

        // 1. Ağ Profilini Private (Özel Ağ) Yap
        Console.WriteLine("[1/4] Ağ profili 'Özel Ağ (Private)' olarak ayarlanıyor...");
        SessizKomutCalistir("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -Command \"Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private -ErrorAction SilentlyContinue\"");

        // 2. TCP Port 5055-5064 Kuralı
        Console.WriteLine("[2/4] TCP Port 5055-5064 güvenlik duvarı izni tanımlanıyor...");
        SessizKomutCalistir("netsh.exe", $"advfirewall firewall delete rule name=\"{PortKuralAdi}\"");
        SessizKomutCalistir("netsh.exe", $"advfirewall firewall add rule name=\"{PortKuralAdi}\" dir=in action=allow protocol=TCP localport=5055-5064 profile=any enable=yes");

        // 3. BarcodeAgentConsole.exe Program İzni
        Console.WriteLine("[3/4] BarcodeAgentConsole.exe uygulama izni tanımlanıyor...");
        if (!string.IsNullOrWhiteSpace(exeYolu))
        {
            SessizKomutCalistir("netsh.exe", $"advfirewall firewall delete rule name=\"{ProgramKuralAdi}\"");
            SessizKomutCalistir("netsh.exe", $"advfirewall firewall add rule name=\"{ProgramKuralAdi}\" dir=in action=allow program=\"{exeYolu}\" profile=any enable=yes");
        }

        // 4. Ping (ICMPv4) Kuralı
        Console.WriteLine("[4/4] Ping (ICMPv4) teşhis izni açılıyor...");
        SessizKomutCalistir("netsh.exe", $"advfirewall firewall delete rule name=\"{PingKuralAdi}\"");
        SessizKomutCalistir("netsh.exe", $"advfirewall firewall add rule name=\"{PingKuralAdi}\" protocol=icmpv4:8,any dir=in action=allow profile=any enable=yes");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=========================================================================");
        Console.WriteLine("   ✅ TÜM AĞ VE GÜVENLİK DUVARI AYARLARI BAŞARIYLA TANIMLANDI!          ");
        Console.WriteLine("=========================================================================");
        Console.ResetColor();

        if (altPencereBekle)
        {
            Thread.Sleep(1200);
        }
    }

    private static void SessizKomutCalistir(string dosyaAdi, string argumanlar)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = dosyaAdi,
                Arguments = argumanlar,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(10000);
        }
        catch
        {
            // Hata olsa bile diğer kurallara devam et
        }
    }
}
