using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>Trae la foto de perfil de un proveedor de identidad (Google) a nuestro almacenamiento, limpia y con nombre propio.</summary>
public interface IImportadorFotoPerfil
{
    /// <returns>La URL de la copia guardada, o null si no se pudo (nunca lanza: el inicio de sesión no depende de esto).</returns>
    Task<string?> ImportarAsync(Guid usuarioId, string? urlFoto, CancellationToken ct = default);
}

/// <summary>
/// La foto que entrega Google en el ID token se copia (no se enlaza): así no depende de un servidor ajeno, pasa por el mismo
/// procesador que las demás fotos (sin metadatos) y cumple la lista de hosts permitidos de imágenes.
/// Contra SSRF: solo https a *.googleusercontent.com, sin seguir redirecciones, con límite de tamaño y de tiempo.
/// </summary>
public sealed partial class ImportadorFotoGoogle : IImportadorFotoPerfil
{
    public const string ClienteHttp = "foto-google";

    private readonly IHttpClientFactory _http;
    private readonly IAlmacenArchivos _almacen;
    private readonly ILogger<ImportadorFotoGoogle> _log;

    public ImportadorFotoGoogle(IHttpClientFactory http, IAlmacenArchivos almacen, ILogger<ImportadorFotoGoogle> log)
    {
        _http = http; _almacen = almacen; _log = log;
    }

    public async Task<string?> ImportarAsync(Guid usuarioId, string? urlFoto, CancellationToken ct = default)
    {
        if (!_almacen.Habilitado || !EsFotoDeGoogle(urlFoto, out var uri)) return null;
        try
        {
            using var respuesta = await _http.CreateClient(ClienteHttp).GetAsync(Ampliar(uri), HttpCompletionOption.ResponseHeadersRead, ct);
            if (!respuesta.IsSuccessStatusCode || respuesta.Content.Headers.ContentLength > ReglasArchivos.TamanoMaximoBytes) return null;
            var bytes = await LeerConLimiteAsync(respuesta.Content, ct);
            return bytes is null ? null : await _almacen.GuardarImagenAsync(usuarioId, bytes, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogInformation(ex, "No se pudo importar la foto de Google del usuario {UsuarioId}", usuarioId);
            return null;
        }
    }

    /// <summary>Solo https y solo servidores de fotos de Google.</summary>
    public static bool EsFotoDeGoogle(string? url, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        var host = u.IdnHost;
        if (host != "googleusercontent.com" && !host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase)) return false;
        uri = u;
        return true;
    }

    /// <summary>Google entrega la foto a 96 px (sufijo "=s96-c"): se pide a 512 px para que se vea nítida en el perfil.</summary>
    public static Uri Ampliar(Uri uri)
    {
        var texto = uri.AbsoluteUri;
        return SufijoTamano().IsMatch(texto) ? new Uri(SufijoTamano().Replace(texto, "=s512-c")) : uri;
    }

    private static async Task<byte[]?> LeerConLimiteAsync(HttpContent contenido, CancellationToken ct)
    {
        await using var flujo = await contenido.ReadAsStreamAsync(ct);
        using var destino = new MemoryStream();
        var bufer = new byte[81920];
        int leidos;
        while ((leidos = await flujo.ReadAsync(bufer, ct)) > 0)
        {
            if (destino.Length + leidos > ReglasArchivos.TamanoMaximoBytes) return null; // sin Content-Length confiable
            destino.Write(bufer, 0, leidos);
        }
        return destino.ToArray();
    }

    [GeneratedRegex(@"=s\d+(-c)?$")]
    private static partial Regex SufijoTamano();
}
