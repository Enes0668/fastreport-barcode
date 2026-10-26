using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BarcodeAgentConsole;
using BarcodeAgentConsole.Hubs;
using BarcodeAgentConsole.Services;
using BarcodeAgentConsole.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "FastReport Barkod Baskı Ajanı (Gömülü SignalR Hub + LAN Paylaşım)";

var builder = WebApplication.CreateBuilder(args);

// 1. Gömülü SignalR ve CORS Servislerini Kaydet (2. YÖNTEM: Merkezi Sunucu Yok, Hub Agent'ın İçinde!)
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

// 2. Yazıcı ve FastReport Bağımlılıklarını Kaydet (Dependency Injection)
builder.Services.AddSingleton<IYaziciYoneticisi, YaziciYoneticisi>();
builder.Services.AddSingleton<IYaziciServisi, BarkodYaziciServisi>();

// 3. Arka Plan İşçisini Kaydet (UDP Yazıcı Paylaşımı ve TCP Ağ Baskı Dinleyicisi)
builder.Services.AddHostedService<BarkodAjanWorker>();

// Boş yerel HTTP/SignalR portunu otomatik belirle (Varsayılan: 5000, doluysa 5001, 5002...)
int baslangicHubPort = int.TryParse(builder.Configuration["SignalR:YerelHubPort"], out int p) ? p : 5000;
int aktifHubPort = BosPortBul(baslangicHubPort);

var app = builder.Build();

app.UseCors();
app.Urls.Add($"http://0.0.0.0:{aktifHubPort}");

// 4. Agent'ın Kendi İçindeki SignalR Hub Endpoint'i
app.MapHub<YerelBarkodHub>("/barkodHub");

// 5. HBYS Web Sayfasından Doğrudan REST/HTTP veya SignalR ile Tetikleme Endpoint'i
app.MapPost("/api/yazdir", async (IHubContext<YerelBarkodHub> hubContext, IYaziciServisi yaziciServisi, BarkodEmirModel emir) =>
{
    if (string.IsNullOrWhiteSpace(emir.IstekId))
        return Results.BadRequest(new { basarili = false, mesaj = "İstek ID boş olamaz!" });

    string hedef = emir.HedefYazici ?? yaziciServisi.VarsayilanYazici;
    var sonuc = yaziciServisi.BarkodBas(emir.IstekId, emir.HedefYazici, emir.Parametreler);

    var ack = new BarkodSonucModel
    {
        IstekId = emir.IstekId,
        Basarili = sonuc.Basarili,
        Mesaj = sonuc.Mesaj,
        Yazici = hedef,
        Tarih = DateTime.Now
    };

    await hubContext.Clients.All.SendAsync("BaskiSonucuAlindi", ack);
    return Results.Ok(new { basarili = sonuc.Basarili, mesaj = sonuc.Mesaj });
});

app.MapGet("/api/yazicilari-yenile", async (IHubContext<YerelBarkodHub> hubContext, IYaziciYoneticisi yaziciYoneticisi) =>
{
    var yazicilar = yaziciYoneticisi.YukluYazicilariGetir();
    await hubContext.Clients.All.SendAsync("YazicilarAlindi", new
    {
        connectionId = "YerelAjan",
        yazicilar = yazicilar,
        zaman = DateTime.Now.ToString("HH:mm:ss")
    });
    return Results.Ok(new { basarili = true, mesaj = "Yazıcı listesi güncellendi." });
});

// 6. Gömülü HBYS Simülasyon / Test Ekranı (Merkezi sunucu olmadan doğrudan Agent üzerinden açılır)
app.MapGet("/", () => Results.Content(YerelDashboardHtml.Content, "text/html; charset=utf-8"));

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=========================================================================");
Console.WriteLine("   🏥 FASTREPORT BARKOD BASKI AJANI (GÖMÜLÜ SIGNALR + AĞ PAYLAŞIMI)     ");
Console.WriteLine($"   📡 Gömülü SignalR Hub : http://localhost:{aktifHubPort}/barkodHub");
Console.WriteLine($"   🌐 HBYS Test Ekranı   : http://localhost:{aktifHubPort}");
Console.WriteLine("=========================================================================");
Console.ResetColor();

await app.RunAsync();

static int BosPortBul(int baslangicPort)
{
    for (int i = 0; i < 10; i++)
    {
        int port = baslangicPort + i;
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return port;
        }
        catch (SocketException)
        {
            // Port doluysa bir sonrakini dene
        }
    }
    return baslangicPort;
}

public static class YerelDashboardHtml
{
    public const string Content = """
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Gömülü Barkod Ajanı - HBYS Baskı Paneli</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
    <style>
        :root {
            --bg: #0f172a; --card: #1e293b; --border: #334155;
            --primary: #3b82f6; --primary-hover: #2563eb;
            --success: #10b981; --danger: #ef4444; --warning: #f59e0b;
            --text: #f8fafc; --text-muted: #94a3b8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
        body { background: var(--bg); color: var(--text); padding: 24px; min-height: 100vh; }
        .container { max-width: 980px; margin: 0 auto; }
        .header { display: flex; align-items: center; justify-content: space-between; padding-bottom: 20px; border-bottom: 1px solid var(--border); margin-bottom: 24px; }
        .header h1 { font-size: 22px; font-weight: 700; display: flex; align-items: center; gap: 10px; }
        .status-badge { display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px; border-radius: 9999px; font-size: 13px; font-weight: 600; }
        .status-online { background: rgba(16, 185, 129, 0.2); color: var(--success); border: 1px solid var(--success); }
        .status-offline { background: rgba(239, 68, 68, 0.2); color: var(--danger); border: 1px solid var(--danger); }
        .pulse-dot { width: 8px; height: 8px; border-radius: 50%; background: currentColor; }
        .grid { display: grid; grid-template-columns: 1.2fr 1fr; gap: 20px; margin-bottom: 24px; }
        @media(max-width: 768px) { .grid { grid-template-columns: 1fr; } }
        .card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 20px; }
        .card h2 { font-size: 16px; font-weight: 600; margin-bottom: 16px; color: var(--text); display: flex; align-items: center; gap: 8px; }
        .form-group { margin-bottom: 12px; }
        .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        .form-group label { display: block; font-size: 12px; color: var(--text-muted); margin-bottom: 4px; font-weight: 500; }
        .form-control { width: 100%; padding: 9px 12px; background: #0b1329; border: 1px solid var(--border); border-radius: 8px; color: #fff; font-size: 13px; outline: none; }
        .form-control:focus { border-color: var(--primary); }
        .btn { display: inline-flex; align-items: center; justify-content: center; gap: 8px; width: 100%; padding: 12px; background: var(--primary); color: white; border: none; border-radius: 8px; font-size: 15px; font-weight: 600; cursor: pointer; margin-top: 8px; }
        .btn:hover { background: var(--primary-hover); }
        .printer-item { padding: 10px 12px; background: #0b1329; border-radius: 8px; font-size: 13px; display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; border: 1px solid #1e293b; }
        .terminal { background: #000; border: 1px solid #222; border-radius: 10px; padding: 16px; height: 220px; overflow-y: auto; font-family: "Consolas", monospace; font-size: 13px; line-height: 1.6; }
        .log-entry { margin-bottom: 4px; word-break: break-all; }
        .log-time { color: var(--text-muted); }
        .log-info { color: #38bdf8; }
        .log-success { color: var(--success); font-weight: 600; }
        .log-error { color: var(--danger); font-weight: 600; }
        .log-warn { color: var(--warning); }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <div>
                <h1>🏥 HBYS Barkod Basım Ekranı (Gömülü Agent SignalR)</h1>
                <p style="color: var(--text-muted); font-size: 13px; margin-top: 4px;">Merkezi sunucu olmadan doğrudan bilgisayarınızdaki Agent'a (SignalR) bağlıdır</p>
            </div>
            <div id="statusBadge" class="status-badge status-offline">
                <span class="pulse-dot"></span>
                <span id="statusText">Agent Bekleniyor...</span>
            </div>
        </div>

        <div class="grid">
            <div class="card">
                <h2>⚡ Barkod Bas (İstekID + Parametrik Şablon)</h2>
                <div class="form-group">
                    <label for="istekId">HBYS İstek ID (Veritabanı Sorgu Anahtarı)</label>
                    <input type="text" id="istekId" class="form-control" value="504" placeholder="Örn: 504, 101...">
                </div>
                <div class="form-group">
                    <label for="hedefYazici">Hedef Yazıcı (Yerel veya Ağdaki Diğer Bilgisayarın Yazıcısı)</label>
                    <select id="hedefYazici" class="form-control">
                        <option value="">-- Bu Bilgisayarın Varsayılan Yazıcısı --</option>
                    </select>
                </div>
                <div class="form-row">
                    <div class="form-group">
                        <label for="barkodNo">Barkod No (Opsiyonel Parametre)</label>
                        <input type="text" id="barkodNo" class="form-control" placeholder="Boşsa IstekId/SQL kullanılır">
                    </div>
                    <div class="form-group">
                        <label for="hastaAdi">Hasta Adı (Opsiyonel Parametre)</label>
                        <input type="text" id="hastaAdi" class="form-control" placeholder="Boşsa varsayılan/SQL kullanılır">
                    </div>
                </div>
                <div class="form-row">
                    <div class="form-group">
                        <label for="protokolNo">Protokol No (Opsiyonel Parametre)</label>
                        <input type="text" id="protokolNo" class="form-control" placeholder="Örn: 2026-98451">
                    </div>
                    <div class="form-group">
                        <label for="bolum">Bölüm (Opsiyonel Parametre)</label>
                        <input type="text" id="bolum" class="form-control" placeholder="Örn: Dahiliye / Biyokimya">
                    </div>
                </div>
                <button id="btnYazdir" class="btn" onclick="yazdirEmriVer()">
                    🖨️ BARKOD BAS (SIGNALR İLE GÖNDER)
                </button>
            </div>

            <div class="card">
                <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom: 12px;">
                    <h2 style="margin-bottom:0;">📋 Algılanan Yazıcılar (Yerel + UDP Ağ)</h2>
                    <button onclick="yazicilariYenile()" style="background:none; border:none; color:var(--primary); font-size:12px; cursor:pointer; text-decoration:underline;">Yenile</button>
                </div>

                <!-- HASTANE A ve B BİLGİSAYARI SİMÜLASYON KONTROLLERİ -->
                <div style="background:#0b1329; border:1px solid #334155; border-radius:8px; padding:10px 12px; margin-bottom:14px; font-size:12px;">
                    <div style="font-weight:600; color:#38bdf8; margin-bottom:8px;">🧪 Hastane Cihaz & Paylaşım Simülasyonu (Canlı Test)</div>
                    <label style="display:flex; align-items:center; gap:8px; cursor:pointer; margin-bottom:6px; color:#e2e8f0;">
                        <input type="checkbox" id="chkUdpPaylasim" onchange="testModuGuncelle()" checked>
                        <span>🌐 <b>UDP Paylaşımı Açık</b> (Bu PC'nin yazıcılarını diğer PC'lere yayınla)</span>
                    </label>
                    <label style="display:flex; align-items:center; gap:8px; cursor:pointer; color:#e2e8f0;">
                        <input type="checkbox" id="chkYerelYok" onchange="testModuGuncelle()">
                        <span>🚫 <b>Bu Bilgisayarda Fiziksel Yazıcı Yok Modu (B Bilgisayarı)</b></span>
                    </label>
                </div>

                <div id="yaziciListesi">
                    <p style="color: var(--text-muted); font-size: 13px;">Yükleniyor...</p>
                </div>
            </div>
        </div>

        <div class="card">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                <h2 style="margin-bottom: 0;">📡 Canlı SignalR & Ağ Baskı Günlüğü</h2>
                <button onclick="document.getElementById('terminal').innerHTML=''" style="background:none; border:none; color:var(--text-muted); font-size:12px; cursor:pointer;">Temizle</button>
            </div>
            <div id="terminal" class="terminal"></div>
        </div>
    </div>

    <script>
        const terminal = document.getElementById("terminal");
        function log(msg, type="info") {
            const time = new Date().toLocaleTimeString();
            const el = document.createElement("div");
            el.className = `log-entry log-${type}`;
            el.innerHTML = `<span class="log-time">[${time}]</span> ${msg}`;
            terminal.appendChild(el);
            terminal.scrollTop = terminal.scrollHeight;
        }

        // Doğrudan Agent'ın kendi içindeki Gömülü SignalR Hub'ına bağlanır!
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/barkodHub")
            .withAutomaticReconnect()
            .build();

        connection.on("YazicilarAlindi", (data) => {
            log(`🎉 Agent'tan ${data.yazicilar.length} adet yazıcı alındı (Yerel + UDP Paylaşımlı).`, "success");
            document.getElementById("statusBadge").className = "status-badge status-online";
            document.getElementById("statusText").innerText = "Gömülü Agent Aktif 🟢";

            if (typeof data.udpPaylasimAcik === "boolean") {
                document.getElementById("chkUdpPaylasim").checked = data.udpPaylasimAcik;
            }
            if (typeof data.yerelYaziciYokModu === "boolean") {
                document.getElementById("chkYerelYok").checked = data.yerelYaziciYokModu;
            }

            const select = document.getElementById("hedefYazici");
            const mevcutSecim = select.value;
            const listDiv = document.getElementById("yaziciListesi");

            select.innerHTML = '<option value="">-- Bu Bilgisayarın Varsayılan Yazıcısı --</option>';
            listDiv.innerHTML = "";

            if (data.yazicilar.length === 0) {
                listDiv.innerHTML = '<div style="padding:12px; background:#0b1329; border:1px dashed #ef4444; border-radius:8px; color:#f87171; font-size:13px;">⚠️ Bu bilgisayarda yerel yazıcı yok ve şu an ağdan (UDP ile) paylaşılan hiçbir yazıcı algılanmadı!</div>';
            }

            data.yazicilar.forEach(p => {
                const opt = document.createElement("option");
                opt.value = p;
                opt.innerText = p;
                select.appendChild(opt);

                const isAg = p.startsWith("[");
                const item = document.createElement("div");
                item.className = "printer-item";
                item.innerHTML = `<span>${isAg ? '🌐' : '🖨️'} ${p}</span> <span style="color:${isAg ? '#38bdf8' : '#10b981'}; font-size:11px;">${isAg ? 'Ağ Yazıcısı (UDP)' : 'Yerel'}</span>`;
                listDiv.appendChild(item);
            });

            if (mevcutSecim) select.value = mevcutSecim;
        });

        connection.on("BaskiSonucuAlindi", (ack) => {
            if (ack.basarili) {
                log(`✅ [BASKI TAMAMLANDI] İstek #${ack.istekId} -> '${ack.yazici}' : ${ack.mesaj}`, "success");
            } else {
                log(`❌ [BASKI HATASI] İstek #${ack.istekId} -> ${ack.mesaj}`, "error");
            }
        });

        connection.start().then(() => {
            log("Agent'ın gömülü SignalR Hub'ına (/barkodHub) doğrudan bağlanıldı!", "info");
        }).catch(err => {
            log("SignalR bağlantı hatası: " + err, "error");
        });

        async function yazdirEmriVer() {
            const parametreler = {};
            const barkodNo = document.getElementById("barkodNo").value.trim();
            const hastaAdi = document.getElementById("hastaAdi").value.trim();
            const protokolNo = document.getElementById("protokolNo").value.trim();
            const bolum = document.getElementById("bolum").value.trim();

            if (barkodNo) parametreler["BarkodNo"] = barkodNo;
            if (hastaAdi) parametreler["HastaAdi"] = hastaAdi;
            if (protokolNo) parametreler["ProtokolNo"] = protokolNo;
            if (bolum) parametreler["Bolum"] = bolum;

            const emir = {
                istekId: document.getElementById("istekId").value.trim(),
                hedefYazici: document.getElementById("hedefYazici").value || null,
                parametreler: Object.keys(parametreler).length > 0 ? parametreler : null
            };

            if (!emir.istekId) {
                alert("Lütfen bir İstek ID girin!");
                return;
            }

            log(`📤 SignalR üzerinden Agent'a gönderiliyor: İstek #${emir.istekId} (Hedef: ${emir.hedefYazici || 'Varsayılan'})...`, "info");

            try {
                // Doğrudan SignalR Hub metodunu çağırıyoruz!
                await connection.invoke("BarkodYazdirGelismi", emir);
            } catch (ex) {
                log(`SignalR gönderim hatası: ${ex}`, "error");
            }
        }

        async function yazicilariYenile() {
            await connection.invoke("YazicilariGetir");
        }

        async function testModuGuncelle() {
            const udpPaylasim = document.getElementById("chkUdpPaylasim").checked;
            const yerelYok = document.getElementById("chkYerelYok").checked;
            log(`🔧 Simülasyon modu güncelleniyor: UDP Paylaşım=${udpPaylasim ? 'AÇIK' : 'KAPALI'}, Yerel Yazıcı Yok=${yerelYok ? 'EVET' : 'HAYIR'}`, "warn");
            await connection.invoke("TestModuDegistir", yerelYok, udpPaylasim);
        }
    </script>
</body>
</html>
""";
}
