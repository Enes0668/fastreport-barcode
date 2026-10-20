using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// SignalR servisini ekle
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

var app = builder.Build();

app.UseCors();
app.Urls.Add("http://localhost:5000");

// SignalR Hub Endpoint'i (BarcodeAgentConsole tam bu adresi dinliyor)
app.MapHub<BarkodHub>("/barkodHub");

// Web arayüzünden doğrudan tetikleme API'si
app.MapPost("/api/yazdir", async (IHubContext<BarkodHub> hubContext, BarkodEmirDto emir) =>
{
    if (string.IsNullOrWhiteSpace(emir.IstekId))
        return Results.BadRequest(new { basarili = false, mesaj = "İstek ID boş olamaz!" });

    if (!string.IsNullOrWhiteSpace(emir.HedefYazici))
    {
        await hubContext.Clients.All.SendAsync("BarkodYazdirGelismi", emir);
    }
    else
    {
        await hubContext.Clients.All.SendAsync("BarkodYazdir", emir.IstekId);
    }

    return Results.Ok(new { basarili = true, mesaj = $"İstek #{emir.IstekId} ajana iletildi." });
});

// Ajanın güncel yazıcıları tekrar göndermesini isteme API'si
app.MapGet("/api/yazicilari-yenile", async (IHubContext<BarkodHub> hubContext) =>
{
    await hubContext.Clients.All.SendAsync("YazicilariGetir");
    return Results.Ok(new { basarili = true, mesaj = "Ajanlara yazıcı yenileme emri gönderildi." });
});

// Test Dashboard HTML Arayüzü
app.MapGet("/", () => Results.Content(IndexHtml.Content, "text/html; charset=utf-8"));

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("==================================================================");
Console.WriteLine(" 🚀 BARKOD SİNYAL TEST SUNUCUSU ÇALIŞIYOR (PORT 5000) ");
Console.WriteLine(" 🌐 Tarayıcıdan açın: http://localhost:5000");
Console.WriteLine("==================================================================");
Console.ResetColor();

app.Run();

// -----------------------------------------------------------------------------
// SIGNALR HUB VE MODELLER
// -----------------------------------------------------------------------------
public class BarkodHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.All.SendAsync("AjanDurumuGuncellendi", new
        {
            durum = "Baglandi",
            connectionId = Context.ConnectionId,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.All.SendAsync("AjanDurumuGuncellendi", new
        {
            durum = "Koptu",
            connectionId = Context.ConnectionId,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });
        await base.OnDisconnectedAsync(exception);
    }

    // Ajan masaüstündeki yazıcıları bildirdiğinde:
    public async Task YazicilariBildir(List<string> yazicilar)
    {
        await Clients.All.SendAsync("YazicilarAlindi", new
        {
            connectionId = Context.ConnectionId,
            yazicilar = yazicilar,
            zaman = DateTime.Now.ToString("HH:mm:ss")
        });
    }

    // Ajan baskıyı tamamlayıp ACK döndüğünde:
    public async Task BarkodSonucBildir(BarkodSonucModel sonuc)
    {
        await Clients.All.SendAsync("BaskiSonucuAlindi", sonuc);
    }
}

public class BarkodEmirDto
{
    public string IstekId { get; set; } = string.Empty;
    public string? HedefYazici { get; set; }
}

public class BarkodSonucModel
{
    public string IstekId { get; set; } = string.Empty;
    public bool Basarili { get; set; }
    public string Mesaj { get; set; } = string.Empty;
    public string Yazici { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
}

public static class IndexHtml
{
    public const string Content = """
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Barkod Baskı Ajanı Test Merkezi</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
    <style>
        :root {
            --bg: #0f172a;
            --card: #1e293b;
            --border: #334155;
            --primary: #3b82f6;
            --primary-hover: #2563eb;
            --success: #10b981;
            --danger: #ef4444;
            --warning: #f59e0b;
            --text: #f8fafc;
            --text-muted: #94a3b8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
        body { background: var(--bg); color: var(--text); padding: 24px; min-height: 100vh; }
        .container { max-width: 960px; margin: 0 auto; }
        .header { display: flex; align-items: center; justify-content: space-between; padding-bottom: 20px; border-bottom: 1px solid var(--border); margin-bottom: 24px; }
        .header h1 { font-size: 24px; font-weight: 700; display: flex; align-items: center; gap: 10px; }
        .status-badge { display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px; border-radius: 9999px; font-size: 13px; font-weight: 600; }
        .status-online { background: rgba(16, 185, 129, 0.2); color: var(--success); border: 1px solid var(--success); }
        .status-offline { background: rgba(239, 68, 68, 0.2); color: var(--danger); border: 1px solid var(--danger); }
        .pulse-dot { width: 8px; height: 8px; border-radius: 50%; background: currentColor; }
        
        .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px; }
        @media(max-width: 768px) { .grid { grid-template-columns: 1fr; } }
        
        .card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 20px; }
        .card h2 { font-size: 16px; font-weight: 600; margin-bottom: 16px; color: var(--text); display: flex; align-items: center; gap: 8px; }
        
        .form-group { margin-bottom: 16px; }
        .form-group label { display: block; font-size: 13px; color: var(--text-muted); margin-bottom: 6px; font-weight: 500; }
        .form-control { width: 100%; padding: 10px 12px; background: #0b1329; border: 1px solid var(--border); border-radius: 8px; color: #fff; font-size: 14px; outline: none; transition: 0.2s; }
        .form-control:focus { border-color: var(--primary); }
        
        .btn { display: inline-flex; align-items: center; justify-content: center; gap: 8px; width: 100%; padding: 12px; background: var(--primary); color: white; border: none; border-radius: 8px; font-size: 15px; font-weight: 600; cursor: pointer; transition: 0.2s; }
        .btn:hover { background: var(--primary-hover); }
        .btn:disabled { opacity: 0.5; cursor: not-allowed; }
        
        .printer-item { padding: 10px 12px; background: #0b1329; border-radius: 8px; font-size: 13px; display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; border: 1px solid #1e293b; }
        
        .terminal { background: #000; border: 1px solid #222; border-radius: 10px; padding: 16px; height: 260px; overflow-y: auto; font-family: "Consolas", monospace; font-size: 13px; line-height: 1.6; }
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
                <h1>🖨️ Barkod Baskı Test Merkezi</h1>
                <p style="color: var(--text-muted); font-size: 13px; margin-top: 4px;">BarcodeAgentConsole Uçtan Uca Doğrulama Konsolu</p>
            </div>
            <div id="statusBadge" class="status-badge status-offline">
                <span class="pulse-dot"></span>
                <span id="statusText">Ajan Bekleniyor...</span>
            </div>
        </div>

        <div class="grid">
            <!-- Baskı Emri Gönderme Kartı -->
            <div class="card">
                <h2>⚡ Baskı Emri Gönder</h2>
                <div class="form-group">
                    <label for="istekId">Laboratuvar / HBYS İstek Numarası (IstekId)</label>
                    <input type="text" id="istekId" class="form-control" value="101" placeholder="Örn: 101, 504...">
                </div>
                <div class="form-group">
                    <label for="hedefYazici">Hedef Yazıcı (Ajanın Bildirdiği)</label>
                    <select id="hedefYazici" class="form-control">
                        <option value="">-- Ajanın Varsayılan Yazıcısı --</option>
                    </select>
                </div>
                <button id="btnYazdir" class="btn" onclick="yazdirEmriVer()">
                    🚀 BARKOD YAZDIR
                </button>
            </div>

            <!-- Masadaki Yazıcılar Kartı -->
            <div class="card">
                <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom: 16px;">
                    <h2 style="margin-bottom:0;">📋 Ajanın Masasındaki Yazıcılar</h2>
                    <button onclick="yazicilariYenile()" style="background:none; border:none; color:var(--primary); font-size:12px; cursor:pointer; text-decoration:underline;">Yenile</button>
                </div>
                <div id="yaziciListesi">
                    <p style="color: var(--text-muted); font-size: 13px;">Ajan bağlandığında kurulu yazıcılar otomatik burada listelenecektir.</p>
                </div>
            </div>
        </div>

        <!-- Canlı Olay Akışı / Log Terminali -->
        <div class="card" style="margin-bottom: 0;">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                <h2 style="margin-bottom: 0;">📡 Canlı Sinyal & ACK Günlüğü</h2>
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

        // Web Arayüzünün SignalR Bağlantısı (Sunucuya bağlanır)
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/barkodHub")
            .withAutomaticReconnect()
            .build();

        connection.on("AjanDurumuGuncellendi", (data) => {
            log(`[Sinyal] Yeni bir istemci bağlandı/ayrıldı (ID: ${data.connectionId})`, "warn");
        });

        connection.on("YazicilarAlindi", (data) => {
            log(`🎉 Ajan ${data.yazicilar.length} adet yazıcı bildirdi!`, "success");
            
            // Badge'i Online Yap
            document.getElementById("statusBadge").className = "status-badge status-online";
            document.getElementById("statusText").innerText = "Ajan Bağlı & Hazır 🟢";

            // Dropdown ve Listeyi Güncelle
            const select = document.getElementById("hedefYazici");
            const listDiv = document.getElementById("yaziciListesi");
            
            select.innerHTML = '<option value="">-- Ajanın Varsayılan Yazıcısı --</option>';
            listDiv.innerHTML = "";

            data.yazicilar.forEach(p => {
                // Dropdown
                const opt = document.createElement("option");
                opt.value = p;
                opt.innerText = p;
                select.appendChild(opt);

                // Liste
                const item = document.createElement("div");
                item.className = "printer-item";
                item.innerHTML = `<span>🖨️ ${p}</span> <span style="color:var(--text-muted); font-size:11px;">Aktif</span>`;
                listDiv.appendChild(item);
            });
        });

        // AJANDAN GELEN ACK (ONAY) BİLDİRİMİ
        connection.on("BaskiSonucuAlindi", (ack) => {
            if (ack.basarili) {
                log(`✅ [ACK GELDİ] İstek #${ack.istekId} başarıyla basıldı! Yazıcı: '${ack.yazici}' - Mesaj: ${ack.mesaj}`, "success");
            } else {
                log(`❌ [ACK GELDİ - HATA] İstek #${ack.istekId} basılamadı! Hata: ${ack.mesaj}`, "error");
            }
        });

        connection.start().then(() => {
            log("SignalR Test Paneli hazır. BarcodeAgentConsole bekleniyor...", "info");
        }).catch(err => {
            log("SignalR bağlantı hatası: " + err, "error");
        });

        async function yazdirEmriVer() {
            const istekId = document.getElementById("istekId").value.trim();
            const hedefYazici = document.getElementById("hedefYazici").value;

            if (!istekId) {
                alert("Lütfen bir İstek ID girin!");
                return;
            }

            log(`📤 Web'den emri gönderiliyor: İstek #${istekId} (Hedef: ${hedefYazici || 'Varsayılan'})...`, "info");

            try {
                const res = await fetch("/api/yazdir", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ istekId: istekId, hedefYazici: hedefYazici || null })
                });
                const json = await res.json();
                log(`📡 Emrin sunucuya ulaşması: ${json.mesaj}`, "info");
            } catch (ex) {
                log(`Sunucu hatası: ${ex}`, "error");
            }
        }

        async function yazicilariYenile() {
            log("Ajanlara güncel yazıcı listesini sorma emri verildi...", "warn");
            await fetch("/api/yazicilari-yenile");
        }
    </script>
</body>
</html>
""";
}
