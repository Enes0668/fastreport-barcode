namespace FastReportBarcodeApp;

/// <summary>
/// FastReport şablon içindeki nesne ve parametre adları için sabitler.
/// Magic string kullanımını önler; şablon adı değişirse tek yerden güncellenir.
/// </summary>
internal static class ReportObjectNames
{
    // Şablon band adları
    public const string DataBand        = "Data1";

    // Şablon nesne adları
    public const string Barcode         = "Barcode1";
    public const string TextHastaAdi   = "txtHastaAdi";
    public const string TextProtokolNo = "txtProtokolNo";
    public const string TextBolum      = "txtBolum";

    // Parametre adları
    public const string ParamBarkodNo   = "BarkodNo";
    public const string ParamHastaAdi  = "HastaAdi";
    public const string ParamProtokolNo = "ProtokolNo";
    public const string ParamBolum      = "Bolum";

    // DataSource adı (toplu baskı)
    public const string DataSourceName  = "Numuneler";
}
