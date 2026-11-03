using System.Collections.Generic;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// FastReport motoru üzerinden termal yazıcıya etiket baskısı gönderen ve şablon senkronizasyonunu yöneten servis arayüzü.
/// </summary>
public interface IYaziciServisi
{
    string VarsayilanYazici { get; set; }
    string SablonYolu { get; }
    string SablonlarKlasoru { get; }
    List<string> MevcutSablonlariGetir();
    long SablonZamanDamgasiniGetir(string? ozelSablonAdi = null);
    Dictionary<string, long> SablonKatalogunuGetir();
    string SablonIceriginiGetir(string? ozelSablonAdi = null);
    bool SablonuGuncelle(string yeniFrxXmlIcerik, long yeniZamanDamgasi, string? ozelSablonAdi = null);
    bool YeniSablonOlustur(string sablonAdi, bool a4Modu = false);
    (bool Basarili, string Mesaj) BarkodBas(string istekId, string? ozelYazici = null, Dictionary<string, string>? parametreler = null, string? sablonAdi = null);
    (bool Basarili, string Mesaj) RaporOnizle(string istekId, string? ozelYazici = null, Dictionary<string, string>? parametreler = null, string? sablonAdi = null);
}
