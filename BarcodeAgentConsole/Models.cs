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
