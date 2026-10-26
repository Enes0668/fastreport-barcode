using System.Collections.Generic;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// FastReport motoru üzerinden termal yazıcıya etiket baskısı gönderen servis arayüzü.
/// </summary>
public interface IYaziciServisi
{
    string VarsayilanYazici { get; set; }
    (bool Basarili, string Mesaj) BarkodBas(string istekId, string? ozelYazici = null, Dictionary<string, string>? parametreler = null);
}
