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
    /// Bu bilgisayarın kendi yerel yazıcılarını yerel ağdaki diğer bilgisayarlara paylaşıp paylaşmayacağını belirler.
    /// </summary>
    bool AgPaylasimAcik { get; set; }

    /// <summary>
    /// PDF, OneNote, Fax gibi gereksiz sanal yazıcıların elenip elenmeyeceğini belirler.
    /// </summary>
    bool SanalYazicilariFiltrele { get; set; }

    /// <summary>
    /// Sadece bilgisayara fiziksel olarak takılı ve açık (Çevrimiçi / Online) olan yazıcıların listelenmesini sağlar.
    /// Fişi çekilen yazıcılar otomatik olarak gizlenir.
    /// </summary>
    bool SadeceCevrimiciYazicilar { get; set; }

    bool GecerliYaziciMi(string yaziciAdi);
    bool YaziciCevrimiciMi(string yaziciAdi);

    List<string> YerelYazicilariGetir();
    List<string> YukluYazicilariGetir();
    string VarsayilanYaziciyiGetir(string? configYazici = null);
    bool AgYazicilariniGuncelle(AjanPaylasimPaketi paket);
    bool ZamanAsiminaUgrayanAgYazicilariniTemizle(TimeSpan zamanAsimi);
    bool AjaninYazicilariniKaldir(string ajanAdi);
    bool AgYazicisiMi(string? yaziciAdi, out string ipAdresi, out int port, out string gercekYaziciAdi);
}
