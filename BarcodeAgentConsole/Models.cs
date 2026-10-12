namespace BarcodeAgentConsole;

/// <summary>
/// Web'den gelen baskı emri modeli.
/// Artık HastaAdi, Bolum gibi hardcoded alanlar YOK!
/// Sadece İstek ID (veya opsiyonel hedef yazıcı) taşır.
/// </summary>
public class BarkodEmirModel
{
    public string IstekId { get; set; } = string.Empty;
    public string? HedefYazici { get; set; }
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
