@echo off
chcp 65001 >nul
title FastReport Barkod Ajanı - Tek Tıkla Kurulum ve Güvenlik Ayarı
color 0b

:: 1. Yönetici Yetkisi Kontrolü (Gerekirse otomatik yönetici olarak yeniden başlatır)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo =========================================================================
    echo   [UYARI] Bu kurulum için Yönetici (Administrator) izni gerekiyor.
    echo   Lütfen açılan pencerede "Evet" butonuna basınız...
    echo =========================================================================
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

echo =========================================================================
echo   🏥 FASTREPORT BARKOD AJANI - OTOMATİK AĞ VE GÜVENLİK KURULUMU         
echo =========================================================================
echo.

echo [1/4] Ağ profili "Özel Ağ (Private)" olarak ayarlanıyor...
powershell -Command "Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private -ErrorAction SilentlyContinue"
echo    -> [TAMAMLANDI] Ağ bağlantısı iç iletişime açıldı.
echo.

echo [2/4] TCP Port 5055 güvenlik duvarı kuralı ekleniyor...
netsh advfirewall firewall delete rule name="Barkod Ajan (TCP 5055)" >nul 2>&1
netsh advfirewall firewall add rule name="Barkod Ajan (TCP 5055)" dir=in action=allow protocol=TCP localport=5055 profile=any enable=yes >nul
echo    -> [TAMAMLANDI] Port 5055 içeri gelen istekler için açıldı.
echo.

echo [3/4] BarcodeAgentConsole.exe program izni tanımlanıyor...
if exist "%~dp0BarcodeAgentConsole.exe" (
    netsh advfirewall firewall delete rule name="Barkod Ajan Uygulamasi" >nul 2>&1
    netsh advfirewall firewall add rule name="Barkod Ajan Uygulamasi" dir=in action=allow program="%~dp0BarcodeAgentConsole.exe" profile=any enable=yes >nul
    echo    -> [TAMAMLANDI] Program güvenlik duvarına beyaz liste olarak eklendi.
) else (
    echo    -> [ATLANDI] BarcodeAgentConsole.exe bulunamadı.
)
echo.

echo [4/4] Ping (ICMPv4) teşhis izni açılıyor...
netsh advfirewall firewall delete rule name="Barkod Ajan Ping Izni" >nul 2>&1
netsh advfirewall firewall add rule name="Barkod Ajan Ping Izni" protocol=icmpv4:8,any dir=in action=allow profile=any enable=yes >nul
echo    -> [TAMAMLANDI] Karşılıklı cihaz tanıma ve ping desteği açıldı.
echo.

echo =========================================================================
echo   ✅ TÜM AYARLAR BAŞARIYLA YAPILDI!
echo   Bu bilgisayar artık odadaki diğer tüm bilgisayarlarla haberleşebilir.
echo =========================================================================
echo.
set /p baslat="Barkod Ajanı şimdi başlatılsın mı? (E/H - Varsayılan: E): "
if /i "%baslat%"=="H" (
    echo Kurulum penceresi kapatılıyor...
    timeout /t 3 >nul
    exit /b
)

echo.
echo Ajan başlatılıyor...
start "" "%~dp0BarcodeAgentConsole.exe"
exit /b
