namespace BarcodeAgentConsole;

public class BarkodIstekModel
{
    public string BarkodNo   { get; set; } = string.Empty;
    public string HastaAdi   { get; set; } = string.Empty;
    public string ProtokolNo { get; set; } = string.Empty;
    public string Bolum      { get; set; } = string.Empty;
}

internal static class ReportObjectNames
{
    public const string DataBand        = "Data1";
    public const string Barcode         = "Barcode1";
    public const string TextHastaAdi   = "txtHastaAdi";
    public const string TextProtokolNo = "txtProtokolNo";
    public const string TextBolum      = "txtBolum";

    public const string ParamBarkodNo   = "BarkodNo";
    public const string ParamHastaAdi  = "HastaAdi";
    public const string ParamProtokolNo = "ProtokolNo";
    public const string ParamBolum      = "Bolum";

    public const string DataSourceName  = "Numuneler";
}
