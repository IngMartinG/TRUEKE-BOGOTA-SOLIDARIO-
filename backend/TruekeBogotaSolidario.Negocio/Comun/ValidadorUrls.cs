using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Evita que se guarden enlaces arbitrarios (phishing, rastreo, contenido ajeno) como imagen o documento.</summary>
public static class ValidadorUrls
{
    public static void ExigirHostPermitido(string? url, IReadOnlyCollection<string> hostsPermitidos, string nombreCampo)
    {
        if (url is null) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ReglaDeNegocioException($"{nombreCampo} debe ser una URL https válida.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ReglaDeNegocioException($"{nombreCampo} no puede incluir credenciales.");
        if (hostsPermitidos.Count == 0) return;
        var ok = hostsPermitidos.Any(h => string.Equals(uri.Host, h, StringComparison.OrdinalIgnoreCase));
        if (!ok) throw new ReglaDeNegocioException($"{nombreCampo} debe estar alojada en un servidor autorizado.");
    }
}
