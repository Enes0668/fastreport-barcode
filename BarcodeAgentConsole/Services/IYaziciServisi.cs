using System.Collections.Generic;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// FastReport motoru üzerinden termal yazıcıya etiket baskısı gönderen ve şablon senkronizasyonunu yöneten servis arayüzü.
/// </summary>
public interface IYaziciServisi
{
    string VarsayilanYazici { get; set; }
    string SablonYolu { get; }
    long SablonZamanDamgasiniGetir();
    string SablonIceriginiGetir();
    bool SablonuGuncelle(string yeniFrxXmlIcerik, long yeniZamanDamgasi);
    (bool Basarili, string Mesaj) BarkodBas(string istekId, string? ozelYazici = null, Dictionary<string, string>? parametreler = null);
}
