using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Respaldo cuando NO hay almacenamiento configurado: evita que se guarden enlaces arbitrarios (phishing, rastreo,
/// contenido ajeno) como imagen o documento. Con Azure Blob habilitado se usa IAlmacenArchivos.ValidarArchivoPropioAsync.
/// </summary>
public static class ValidadorUrls
{
    public static void ExigirHostPermitido(string? url, IReadOnlyCollection<string> hostsPermitidos, string nombreCampo)
    {
        if (url is null) return;
        if (!UrlsSeguras.EsValida(url))
            throw new ReglaDeNegocioException($"{nombreCampo} debe ser una URL https válida.");
        if (hostsPermitidos.Count == 0) return;
        var host = new Uri(url).Host;
        if (!hostsPermitidos.Any(h => string.Equals(host, h, StringComparison.OrdinalIgnoreCase)))
            throw new ReglaDeNegocioException($"{nombreCampo} debe estar alojada en un servidor autorizado.");
    }
}
