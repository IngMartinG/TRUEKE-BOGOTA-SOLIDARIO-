using System.Text.RegularExpressions;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>Reglas puras (sin Azure) para subir y aceptar archivos. Las usa el almacén real y las pruebas.</summary>
public static partial class ReglasArchivos
{
    public const long TamanoMaximoBytes = 5 * 1024 * 1024;
    public const int BytesFirma = 12;

    private static readonly IReadOnlyDictionary<string, string> Imagenes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    /// <summary>Documentos de identidad: imagen o PDF.</summary>
    private static readonly IReadOnlyDictionary<string, string> Documentos = new Dictionary<string, string>(Imagenes, StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = "pdf"
    };

    public static string Extension(TipoArchivoDto tipo, string contentType)
    {
        var permitidos = tipo == TipoArchivoDto.Documento ? Documentos : Imagenes;
        return permitidos.TryGetValue(contentType ?? "", out var ext)
            ? ext
            : throw new ReglaDeNegocioException(tipo == TipoArchivoDto.Documento
                ? "El documento debe ser JPG, PNG, WEBP o PDF."
                : "La imagen debe ser JPG, PNG o WEBP.");
    }

    public static void ValidarTamano(long bytes)
    {
        if (bytes <= 0) throw new ReglaDeNegocioException("El archivo está vacío.");
        if (bytes > TamanoMaximoBytes) throw new ReglaDeNegocioException("El archivo supera el máximo de 5 MB.");
    }

    /// <summary>Comprueba los "magic bytes": el contenido real debe coincidir con el tipo declarado (no basta la extensión).</summary>
    public static bool FirmaCoincide(string extension, ReadOnlySpan<byte> c) => extension switch
    {
        "jpg" => c.Length >= 3 && c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF,
        "png" => c.Length >= 8 && c[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "webp" => c.Length >= 12 && c[..4].SequenceEqual("RIFF"u8) && c[8..12].SequenceEqual("WEBP"u8),
        "pdf" => c.Length >= 5 && c[..5].SequenceEqual("%PDF-"u8),
        _ => false
    };

    [GeneratedRegex("^(?<usuario>[0-9a-f]{32})/[0-9a-f]{32}\\.(?<ext>jpg|png|webp|pdf)$")]
    private static partial Regex NombreBlob();

    /// <summary>Nombre que el servidor asigna: {usuario}/{aleatorio}.{ext}. El cliente nunca elige la ruta.</summary>
    public static string NuevoNombre(Guid usuarioId, string extension) => $"{usuarioId:N}/{Guid.NewGuid():N}.{extension}";

    /// <summary>
    /// Si <paramref name="url"/> es un archivo de <paramref name="contenedor"/> subido por <paramref name="usuarioId"/>,
    /// devuelve (nombre del blob, extensión); si no (otro host, otro contenedor, otro usuario, query, rutas raras), null.
    /// </summary>
    public static (string Nombre, string Extension)? AnalizarUrl(string url, Uri contenedor, Guid usuarioId)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment)
            || !string.IsNullOrEmpty(u.UserInfo)) return null;
        var baseContenedor = contenedor.AbsoluteUri.TrimEnd('/') + "/";
        if (!u.AbsoluteUri.StartsWith(baseContenedor, StringComparison.Ordinal)) return null;
        var nombre = u.AbsoluteUri[baseContenedor.Length..];
        var m = NombreBlob().Match(nombre);
        if (!m.Success || m.Groups["usuario"].Value != usuarioId.ToString("N")) return null;
        return (nombre, m.Groups["ext"].Value);
    }
}
