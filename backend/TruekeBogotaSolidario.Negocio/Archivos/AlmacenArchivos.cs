using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Archivos;

public sealed class AlmacenamientoOpciones
{
    public const string Seccion = "Almacenamiento";
    /// <summary>URL de la API de Storage de Supabase: https://&lt;proyecto&gt;.supabase.co/storage/v1.</summary>
    public string Url { get; set; } = "";
    /// <summary>
    /// Llave secreta del proyecto (sb_secret_… o la service_role antigua). Solo la tiene el servidor: con ella firma las
    /// subidas y escribe en los buckets. El navegador nunca la ve.
    /// </summary>
    public string LlaveServicio { get; set; } = "";
    /// <summary>Bucket de imágenes de publicaciones (lectura pública; solo el servidor escribe).</summary>
    public string BucketImagenes { get; set; } = "imagenes";
    /// <summary>Bucket de documentos de identidad (PRIVADO: solo se leen con una URL firmada temporal).</summary>
    public string BucketDocumentos { get; set; } = "documentos";
    /// <summary>
    /// Bucket PRIVADO donde el navegador sube los archivos con una URL firmada. Nada de aquí se publica tal cual: al usar el
    /// archivo, el servidor publica una copia propia (foto limpia o documento copiado) con un nombre nuevo y borra la subida.
    /// </summary>
    public string BucketSubidas { get; set; } = "subidas";
    public bool Configurado => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(LlaveServicio);

    /// <summary>https, o http solo hacia la propia máquina (Storage local en Docker para desarrollo y pruebas).</summary>
    public bool UrlValida => !Configurado || (UrlsSeguras.EsValida(Url) && Uri.TryCreate(Url, UriKind.Absolute, out var u) && string.IsNullOrEmpty(u.Query));

    /// <summary>
    /// Opcional: CDN o dominio propio delante de las fotos públicas (p. ej. https://fotos.midominio.co, que reenvía a
    /// {Url}/object/public/). Solo cambia el host desde el que el navegador DESCARGA las fotos; en la base y en la API
    /// siguen las URLs de Supabase (así la validación de "archivo propio" no cambia y apagar la CDN no rompe nada).
    /// </summary>
    public string CdnUrl { get; set; } = "";

    public bool CdnHabilitada => !string.IsNullOrWhiteSpace(CdnUrl);

    /// <summary>La CDN debe ser https, sin query, y reenviar a un Storage conocido (Url).</summary>
    public bool CdnValida => !CdnHabilitada
        || (Uri.TryCreate(CdnUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.Query)
            && Uri.TryCreate(Url, UriKind.Absolute, out _));

    /// <summary>Prefijo público de los buckets de lectura pública: {Url}/object/public/ (lo que la CDN reemplaza).</summary>
    public string OrigenPublico => Url.TrimEnd('/') + "/object/public/";
}

public interface IAlmacenArchivos
{
    /// <summary>false si no hay almacenamiento configurado (las URLs se validan solo por host permitido).</summary>
    bool Habilitado { get; }
    Task<SubidaArchivoDto> CrearSubidaAsync(Guid usuarioId, TipoArchivoDto tipo, string contentType, long tamanoBytes, CancellationToken ct = default);
    /// <summary>
    /// Exige que la URL sea un archivo NUESTRO, subido por ese usuario, existente, ≤ 5 MB y con contenido del tipo declarado.
    /// Devuelve la URL que se debe guardar: en las imágenes, la de la copia limpia (sin GPS ni metadatos).
    /// </summary>
    Task<string> ValidarArchivoPropioAsync(string url, Guid usuarioId, TipoArchivoDto tipo, CancellationToken ct = default);
    /// <summary>URL de lectura válida 5 minutos (documentos privados para moderadores).</summary>
    Task<string?> UrlLecturaTemporalAsync(string url, CancellationToken ct = default);
    Task EliminarDelUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
    /// <summary>Borra un documento privado (p. ej. el de identidad una vez resuelta la verificación). Ignora URLs ajenas.</summary>
    Task EliminarDocumentoAsync(string url, CancellationToken ct = default);
    /// <summary>Borra una imagen del usuario (p. ej. la foto de perfil anterior). Ignora URLs que no sean suyas.</summary>
    Task EliminarImagenPropiaAsync(string url, Guid usuarioId, CancellationToken ct = default);
    /// <summary>
    /// Guarda una imagen que obtuvo el servidor (p. ej. la foto de Google): la limpia (JPEG sin metadatos) y devuelve su URL
    /// pública. Lanza <see cref="ReglaDeNegocioException"/> si no es una imagen válida o supera el tamaño permitido.
    /// </summary>
    Task<string> GuardarImagenAsync(Guid usuarioId, byte[] contenido, CancellationToken ct = default);
    /// <summary>
    /// Borra las subidas que nadie usó (más de una hora en el bucket de subidas). Devuelve cuántas borró.
    /// </summary>
    Task<int> EliminarSubidasVencidasAsync(CancellationToken ct = default);
    /// <summary>
    /// Crea la miniatura de las fotos publicadas que aún no la tienen (las anteriores a las miniaturas o aquellas cuya
    /// miniatura falló). Devuelve cuántas creó. Es idempotente: varias instancias a la vez no se estorban.
    /// </summary>
    Task<int> GenerarMiniaturasFaltantesAsync(CancellationToken ct = default);
    /// <summary>
    /// Bytes de una foto pública YA publicada en el bucket de imágenes (p. ej. para la vista previa al compartir).
    /// null si la URL no es una foto de la plataforma, no existe o supera el tamaño máximo. Nunca sigue otras URLs.
    /// </summary>
    Task<byte[]?> LeerFotoPublicadaAsync(string url, CancellationToken ct = default);
}

public sealed class AlmacenDeshabilitado : IAlmacenArchivos
{
    public bool Habilitado => false;
    public Task<SubidaArchivoDto> CrearSubidaAsync(Guid usuarioId, TipoArchivoDto tipo, string contentType, long tamanoBytes, CancellationToken ct = default)
        => throw new ReglaDeNegocioException("La subida de archivos no está configurada en este entorno.");
    public Task<string> ValidarArchivoPropioAsync(string url, Guid usuarioId, TipoArchivoDto tipo, CancellationToken ct = default) => Task.FromResult(url);
    public Task<string?> UrlLecturaTemporalAsync(string url, CancellationToken ct = default) => Task.FromResult<string?>(url);
    public Task EliminarDelUsuarioAsync(Guid usuarioId, CancellationToken ct = default) => Task.CompletedTask;
    public Task EliminarDocumentoAsync(string url, CancellationToken ct = default) => Task.CompletedTask;
    public Task EliminarImagenPropiaAsync(string url, Guid usuarioId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> GuardarImagenAsync(Guid usuarioId, byte[] contenido, CancellationToken ct = default)
        => throw new ReglaDeNegocioException("La subida de archivos no está configurada en este entorno.");
    public Task<int> EliminarSubidasVencidasAsync(CancellationToken ct = default) => Task.FromResult(0);
    public Task<int> GenerarMiniaturasFaltantesAsync(CancellationToken ct = default) => Task.FromResult(0);
    public Task<byte[]?> LeerFotoPublicadaAsync(string url, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
}

