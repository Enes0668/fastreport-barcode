using System;
using System.Text;
using BarcodeAgentConsole.Services;
using BarcodeAgentConsole.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "FastReport Barkod Baskı Ajanı (Background Worker)";

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================================");
Console.WriteLine("        🏥 FASTREPORT BARKOD BASKI AJANI (BACKGROUND WORKER)      ");
Console.WriteLine("==================================================================");
Console.ResetColor();

var builder = Host.CreateApplicationBuilder(args);

// 1. Bağımlılıkları Kaydet (Dependency Injection)
builder.Services.AddSingleton<IYaziciYoneticisi, YaziciYoneticisi>();
builder.Services.AddSingleton<IYaziciServisi, BarkodYaziciServisi>();

// 2. Arka Plan İşçisini Kaydet (BackgroundService)
builder.Services.AddHostedService<BarkodAjanWorker>();

var app = builder.Build();

// 3. Ajanı Başlat ve Çalıştır
await app.RunAsync();
