# UTF-8 Output
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "FastReport Barkod Ajanı - Otomatik Ağ ve Güvenlik Kurulumu"

Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host "   🏥 FASTREPORT BARKOD AJANI - OTOMATİK AĞ VE GÜVENLİK KURULUMU         " -ForegroundColor Cyan
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Ağ Profilini Private Yap
Write-Host "[1/4] Ağ profili 'Özel Ağ (Private)' olarak ayarlanıyor..." -ForegroundColor Yellow
try {
    Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private -ErrorAction SilentlyContinue
    Write-Host "   [OK] Ağ bağlantısı iç iletişime açıldı." -ForegroundColor Green
} catch {
    Write-Host "   [UYARI] Ağ profili ayarlanamadı: $_" -ForegroundColor Red
}
Write-Host ""

# 2. Port 5055 Kuralı
Write-Host "[2/4] TCP Port 5055 güvenlik duvarı kuralı ekleniyor..." -ForegroundColor Yellow
try {
    netsh advfirewall firewall delete rule name="Barkod Ajan (TCP 5055)" | Out-Null
    netsh advfirewall firewall add rule name="Barkod Ajan (TCP 5055)" dir=in action=allow protocol=TCP localport=5055 profile=any enable=yes | Out-Null
    Write-Host "   [OK] Port 5055 içeri gelen istekler için açıldı." -ForegroundColor Green
} catch {
    Write-Host "   [HATA] Port kuralı eklenemedi: $_" -ForegroundColor Red
}
Write-Host ""

# 3. BarcodeAgentConsole.exe Kuralı
Write-Host "[3/4] BarcodeAgentConsole.exe program izni tanımlanıyor..." -ForegroundColor Yellow
$exePath = Join-Path $PSScriptRoot "BarcodeAgentConsole.exe"
if (Test-Path $exePath) {
    netsh advfirewall firewall delete rule name="Barkod Ajan Uygulamasi" | Out-Null
    netsh advfirewall firewall add rule name="Barkod Ajan Uygulamasi" dir=in action=allow program="$exePath" profile=any enable=yes | Out-Null
    Write-Host "   [OK] Program güvenlik duvarına eklendi." -ForegroundColor Green
} else {
    Write-Host "   [ATLANDI] BarcodeAgentConsole.exe bu klasörde bulunamadı." -ForegroundColor Gray
}
Write-Host ""

# 4. Ping (ICMPv4) Kuralı
Write-Host "[4/4] Ping (ICMPv4) teşhis izni açılıyor..." -ForegroundColor Yellow
try {
    netsh advfirewall firewall delete rule name="Barkod Ajan Ping Izni" | Out-Null
    netsh advfirewall firewall add rule name="Barkod Ajan Ping Izni" protocol=icmpv4:8,any dir=in action=allow profile=any enable=yes | Out-Null
    Write-Host "   [OK] Ping desteği açıldı." -ForegroundColor Green
} catch {
    Write-Host "   [HATA] Ping kuralı eklenemedi: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "=========================================================================" -ForegroundColor Green
Write-Host "   TÜM AYARLAR BAŞARIYLA YAPILDI!" -ForegroundColor Green
Write-Host "   Bu bilgisayar artık odadaki diğer bilgisayarlarla haberleşebilir." -ForegroundColor Green
Write-Host "=========================================================================" -ForegroundColor Green
Write-Host ""
