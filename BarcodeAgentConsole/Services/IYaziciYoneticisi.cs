using System;
using System.Collections.Generic;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// Bilgisayarda kurulu yazıcıları ve yerel ağdaki diğer Agent'lardan gelen yazıcıları yöneten servis arayüzü.
/// </summary>
public interface IYaziciYoneticisi
{
    /// <summary>
    /// Tek bilgisayarda A ve B bilgisayarlarını gerçekçi test edebilmek için:
    /// B bilgisayarı (2. açılan Agent) "YerelYaziciYokSimulasyonu = true" olarak ayarlandığında
    /// sanki kendisine hiç fiziksel yazıcı takılı değilmiş gibi davranır (sadece ağdan gelen yazıcıları görür).
    /// </summary>
    bool YerelYaziciYokSimulasyonu { get; set; }

    /// <summary>
    /// Bu bilgisayarın kendi yerel yazıcılarını UDP ile ağdaki diğer bilgisayarlara paylaşıp paylaşmayacağını belirler.
    /// </summary>
    bool UdpPaylasimAcik { get; set; }

    List<string> YerelYazicilariGetir();
    List<string> YukluYazicilariGetir();
    string VarsayilanYaziciyiGetir(string? configYazici = null);
    bool AgYazicilariniGuncelle(AjanPaylasimPaketi paket);
    bool ZamanAsiminaUgrayanAgYazicilariniTemizle(TimeSpan zamanAsimi);
    bool AjaninYazicilariniKaldir(string ajanAdi);
    bool AgYazicisiMi(string? yaziciAdi, out string ipAdresi, out int port, out string gercekYaziciAdi);
}
