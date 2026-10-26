using System;
using System.Collections.Generic;

namespace BarcodeAgentConsole;

/// <summary>
/// Web'den gelen baskı emri modeli.
/// Sadece İstek ID, hedef yazıcı ve tamamen dinamik Key-Value parametre sözlüğünü taşır.
/// </summary>
public class BarkodEmirModel
{
    public string IstekId { get; set; } = string.Empty;
    public string? HedefYazici { get; set; }
    public Dictionary<string, string>? Parametreler { get; set; }
}

/// <summary>
/// Baskı tamamlandığında veya hata alındığında SignalR Hub üzerinden Web'e dönen geri bildirim modeli (ACK).
/// </summary>
public class BarkodSonucModel
{
    public string IstekId { get; set; } = string.Empty;
    public bool Basarili { get; set; }
    public string Mesaj { get; set; } = string.Empty;
    public string Yazici { get; set; } = string.Empty;
    public DateTime Tarih { get; set; } = DateTime.Now;
}

/// <summary>
/// 2. Kademe: A ve B bilgisayarlarındaki Agent'ların yerel ağ (LAN) üzerinden
/// her 5 saniyede bir birbirlerine fırlattığı yazıcı paylaşım paketi.
/// </summary>
public class AjanPaylasimPaketi
{
    public string AjanAdi { get; set; } = string.Empty;
    public string IpAdresi { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public List<string> Yazicilar { get; set; } = new();
}

/// <summary>
/// 2. Kademe: B bilgisayarındaki Agent'ın, A bilgisayarındaki Agent'a
/// yerel ağ üzerinden gönderdiği yazdırma isteği paketi.
/// </summary>
public class AgBaskiIstekPaketi
{
    public string GonderenAjan { get; set; } = string.Empty;
    public string IstekId { get; set; } = string.Empty;
    public string HedefYazici { get; set; } = string.Empty;
    public Dictionary<string, string>? Parametreler { get; set; }
}
