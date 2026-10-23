using System.Collections.Generic;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// Bilgisayarda kurulu yazıcıları ve yerel ağdaki diğer Agent'lardan gelen yazıcıları yöneten servis arayüzü.
/// </summary>
public interface IYaziciYoneticisi
{
    List<string> YerelYazicilariGetir();
    List<string> YukluYazicilariGetir();
    string VarsayilanYaziciyiGetir(string? configYazici = null);
    bool AgYazicilariniGuncelle(AjanPaylasimPaketi paket);
    bool AgYazicisiMi(string? yaziciAdi, out string ipAdresi, out int port, out string gercekYaziciAdi);
}
