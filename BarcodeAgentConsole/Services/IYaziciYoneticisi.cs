using System.Collections.Generic;

namespace BarcodeAgentConsole.Services;

/// <summary>
/// Bilgisayarda kurulu yazıcıları tarayan ve varsayılan yazıcıyı belirleyen servis arayüzü.
/// </summary>
public interface IYaziciYoneticisi
{
    List<string> YukluYazicilariGetir();
    string VarsayilanYaziciyiGetir(string? configYazici = null);
}
