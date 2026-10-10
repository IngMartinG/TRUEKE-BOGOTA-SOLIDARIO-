using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Archivos;

public sealed class AlmacenamientoOpciones
{
    public const string Seccion = "Almacenamiento";
    /// <summary>Producción: URL de la cuenta (https://cuenta.blob.core.windows.net) + Managed Identity (sin llaves).</summary>
    public string ServicioUrl { get; set; } = "";
    /// <summary>Alternativa: cadena de conexión (Azurite en desarrollo: "UseDevelopmentStorage=true").</summary>
    public string CadenaConexion { get; set; } = "";
    /// <summary>Contenedor de imágenes de publicaciones (lectura pública).</summary>
    public string ContenedorImagenes { get; set; } = "imagenes";
    /// <summary>Contenedor de documentos de identidad (PRIVADO: solo se leen con SAS temporal).</summary>
    public string ContenedorDocumentos { get; set; } = "documentos";
    public bool Configurado => !string.IsNullOrWhiteSpace(ServicioUrl) || !string.IsNullOrWhiteSpace(CadenaConexion);

    /// <summary>
    /// Opcional: CDN delante de la cuenta (p. ej. https://fotos.midominio.co, que reenvía a <see cref="ServicioUrl"/>).
    /// Solo cambia el host desde el que el navegador DESCARGA las fotos públicas; en la base y en la API siguen las URLs
    /// de Blob (así la validación de "archivo propio" no cambia y apagar la CDN no rompe nada).
    /// </summary>
    public string CdnUrl { get; set; } = "";

    public bool CdnHabilitada => !string.IsNullOrWhiteSpace(CdnUrl);

    /// <summary>La CDN debe ser https, sin query, y reenviar a una cuenta conocida (ServicioUrl).</summary>
    public bool CdnValida => !CdnHabilitada
        || (Uri.TryCreate(CdnUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.Query)
            && Uri.TryCreate(ServicioUrl, UriKind.Absolute, out _));
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
    /// Crea la miniatura de las fotos publicadas que aún no la tienen (las anteriores a las miniaturas o aquellas cuya
    /// miniatura falló). Devuelve cuántas creó. Es idempotente: varias instancias a la vez no se estorban.
    /// </summary>
    Task<int> GenerarMiniaturasFaltantesAsync(CancellationToken ct = default);
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
    public Task<int> GenerarMiniaturasFaltantesAsync(CancellationToken ct = default) => Task.FromResult(0);
}

/// <summary>
/// Azure Blob Storage con SAS de alcance mínimo: un solo blob (nombre elegido por el servidor), solo crear/escribir,
/// válida 5 minutos. El tamaño no se puede limitar con una SAS, por eso se verifica DESPUÉS, al usar la URL.
/// </summary>
public sealed class AlmacenBlobAzure : IAlmacenArchivos
{
    private static readonly TimeSpan VigenciaSas = TimeSpan.FromMinutes(5);

    private readonly AlmacenamientoOpciones _o;
    private readonly BlobServiceClient _servicio;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AlmacenBlobAzure> _log;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private UserDelegationKey? _llaveDelegacion;
    private bool _contenedoresListos;

    public AlmacenBlobAzure(IOptions<AlmacenamientoOpciones> o, TimeProvider reloj, ILogger<AlmacenBlobAzure> log)
    {
        _o = o.Value;
        _reloj = reloj;
        _log = log;
        _servicio = !string.IsNullOrWhiteSpace(_o.CadenaConexion)
            ? new BlobServiceClient(_o.CadenaConexion)
            : new BlobServiceClient(new Uri(_o.ServicioUrl), new DefaultAzureCredential());
    }

    public bool Habilitado => true;

    private BlobContainerClient Contenedor(TipoArchivoDto tipo)
        => _servicio.GetBlobContainerClient(tipo == TipoArchivoDto.Documento ? _o.ContenedorDocumentos : _o.ContenedorImagenes);

    /// <summary>Azurite (desarrollo) usa siempre la cuenta "devstoreaccount1"; una cuenta real de Azure nunca se llama así.</summary>
    private bool EsEmuladorLocal => string.Equals(_servicio.AccountName, "devstoreaccount1", StringComparison.Ordinal);

    /// <summary>
    /// Solo en Azurite: el navegador sube las fotos directo al emulador (PUT con SAS) desde otro origen, así que necesita
    /// una regla CORS. En Azure la regla se configura en la cuenta de Storage limitada al dominio del front (README §9).
    /// </summary>
    private async Task PermitirSubidasDelNavegadorEnEmuladorAsync(CancellationToken ct)
    {
        var propiedades = (await _servicio.GetPropertiesAsync(ct)).Value;
        if (propiedades.Cors.Count > 0) return;
        propiedades.Cors.Add(new BlobCorsRule
        {
            AllowedOrigins = "*",
            AllowedMethods = "PUT,GET,HEAD,OPTIONS",
            AllowedHeaders = "*",
            ExposedHeaders = "*",
            MaxAgeInSeconds = 3600
        });
        await _servicio.SetPropertiesAsync(propiedades, ct);
        _log.LogInformation("Azurite: regla CORS de desarrollo creada para las subidas desde el navegador");
    }

    /// <summary>Crea los contenedores si no existen (en Azure lo normal es crearlos por infraestructura; en Azurite, aquí).</summary>
    private async Task AsegurarContenedoresAsync(CancellationToken ct)
    {
        if (_contenedoresListos) return;
        try
        {
            await Contenedor(TipoArchivoDto.Imagen).CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
            await Contenedor(TipoArchivoDto.Documento).CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            if (EsEmuladorLocal) await PermitirSubidasDelNavegadorEnEmuladorAsync(ct);
        }
        catch (RequestFailedException ex)
        {
            _log.LogWarning(ex, "No se pudieron crear/verificar los contenedores (deben existir por infraestructura)");
        }
        _contenedoresListos = true;
    }

    private async Task<Uri> FirmarAsync(BlobClient blob, BlobSasPermissions permisos, CancellationToken ct)
    {
        var ahora = _reloj.GetUtcNow();
        var sas = new BlobSasBuilder
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            StartsOn = ahora.AddMinutes(-1),
            ExpiresOn = ahora + VigenciaSas,
            Protocol = blob.Uri.Scheme == Uri.UriSchemeHttps ? SasProtocol.Https : SasProtocol.HttpsAndHttp
        };
        sas.SetPermissions(permisos);

        if (blob.CanGenerateSasUri) return blob.GenerateSasUri(sas); // cadena de conexión con llave (Azurite / desarrollo)

        // Managed Identity: SAS de delegación de usuario (firmada con una llave temporal, sin llave de cuenta)
        await _candado.WaitAsync(ct);
        try
        {
            if (_llaveDelegacion is null || _llaveDelegacion.SignedExpiresOn < ahora.AddMinutes(10))
                _llaveDelegacion = (await _servicio.GetUserDelegationKeyAsync(ahora.AddMinutes(-1), ahora.AddHours(2), ct)).Value;
        }
        finally { _candado.Release(); }
        return new BlobUriBuilder(blob.Uri) { Sas = sas.ToSasQueryParameters(_llaveDelegacion, _servicio.AccountName) }.ToUri();
    }

    public async Task<SubidaArchivoDto> CrearSubidaAsync(Guid usuarioId, TipoArchivoDto tipo, string contentType, long tamanoBytes, CancellationToken ct = default)
    {
        var ext = ReglasArchivos.Extension(tipo, contentType);
        ReglasArchivos.ValidarTamano(tamanoBytes);
        await AsegurarContenedoresAsync(ct);
        return await ConstruirSubidaAsync(usuarioId, tipo, contentType, ext, ct);
    }

    /// <summary>Solo firma (sin llamadas de red cuando hay llave de cuenta): lo prueban las pruebas unitarias.</summary>
    internal async Task<SubidaArchivoDto> ConstruirSubidaAsync(Guid usuarioId, TipoArchivoDto tipo, string contentType, string ext, CancellationToken ct)
    {
        var blob = Contenedor(tipo).GetBlobClient(ReglasArchivos.NuevoNombre(usuarioId, ext));
        var url = await FirmarAsync(blob, BlobSasPermissions.Create | BlobSasPermissions.Write, ct);
        var cabeceras = new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob", ["Content-Type"] = contentType.ToLowerInvariant() };
        return new SubidaArchivoDto(url.ToString(), blob.Uri.ToString(), "PUT", cabeceras, _reloj.GetUtcNow().UtcDateTime + VigenciaSas);
    }

    public async Task<string> ValidarArchivoPropioAsync(string url, Guid usuarioId, TipoArchivoDto tipo, CancellationToken ct = default)
    {
        var contenedor = Contenedor(tipo);
        var analisis = ReglasArchivos.AnalizarUrl(url, contenedor.Uri, usuarioId)
                       ?? throw new ReglaDeNegocioException("El archivo debe subirse primero con POST /api/v1/archivos/subidas.");
        var blob = contenedor.GetBlobClient(analisis.Nombre);
        try
        {
            var props = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
            ReglasArchivos.ValidarTamano(props.ContentLength);
            if (ReglasArchivos.Extension(tipo, props.ContentType) != analisis.Extension)
                throw new ReglaDeNegocioException("El tipo del archivo no coincide con su extensión.");

            var inicio = (await blob.DownloadContentAsync(new BlobDownloadOptions { Range = new HttpRange(0, ReglasArchivos.BytesFirma) }, ct)).Value.Content;
            if (!ReglasArchivos.FirmaCoincide(analisis.Extension, inicio.ToArray()))
            {
                await blob.DeleteIfExistsAsync(cancellationToken: ct); // contenido disfrazado: se elimina
                throw new ReglaDeNegocioException("El contenido del archivo no corresponde a una imagen o documento válido.");
            }

            // Fotos públicas: se publica una copia limpia (sin GPS ni metadatos) con un nombre NUEVO y se borra el original.
            // Con un nombre nuevo, la SAS de subida (aún vigente unos minutos) ya no puede reemplazar la foto publicada.
            if (tipo == TipoArchivoDto.Imagen && !(props.Metadata.TryGetValue(MetadatoLimpia, out var limpia) && limpia == "1"))
                return await LimpiarImagenAsync(contenedor, blob, usuarioId, analisis.Extension, ct);
            return url;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new ReglaDeNegocioException("El archivo no existe o aún no terminó de subirse.");
        }
    }

    private const string MetadatoLimpia = "limpia";

    private async Task<string> LimpiarImagenAsync(BlobContainerClient contenedor, BlobClient original, Guid usuarioId, string extension, CancellationToken ct)
    {
        var contenido = (await original.DownloadContentAsync(ct)).Value.Content;
        ReglasArchivos.ValidarTamano(contenido.ToMemory().Length);
        ProcesadorImagenes.ImagenLimpia limpia;
        try
        {
            using var flujo = contenido.ToStream();
            limpia = await ProcesadorImagenes.LimpiarConMiniaturaAsync(flujo, extension, ct);
        }
        catch (ReglaDeNegocioException)
        {
            await original.DeleteIfExistsAsync(cancellationToken: ct); // imagen corrupta o "bomba": no se conserva
            throw;
        }

        var url = await PublicarLimpiaAsync(contenedor, usuarioId, limpia, extension, ct);
        await original.DeleteIfExistsAsync(cancellationToken: ct);
        _log.LogInformation("Imagen limpiada y publicada ({Antes} → {Despues} bytes, miniatura {Miniatura} bytes)",
            contenido.ToMemory().Length, limpia.Foto.Length, limpia.Miniatura.Length);
        return url;
    }

    /// <summary>
    /// Sube una imagen YA limpia con un nombre nuevo del usuario (nunca sobrescribe) y su miniatura; devuelve la URL pública
    /// de la foto. Si la miniatura falla, la foto se publica igual: el front usa la foto grande y el relleno la crea después.
    /// </summary>
    private async Task<string> PublicarLimpiaAsync(BlobContainerClient contenedor, Guid usuarioId, ProcesadorImagenes.ImagenLimpia limpia,
        string extension, CancellationToken ct)
    {
        var destino = contenedor.GetBlobClient(ReglasArchivos.NuevoNombre(usuarioId, extension));
        await SubirInmutableAsync(destino, limpia.Foto, extension switch { "png" => "image/png", "webp" => "image/webp", _ => "image/jpeg" }, ct);
        try
        {
            await SubirInmutableAsync(contenedor.GetBlobClient(ReglasArchivos.NombreMiniatura(destino.Name)), limpia.Miniatura, "image/webp", ct);
        }
        catch (RequestFailedException ex)
        {
            _log.LogWarning(ex, "No se pudo publicar la miniatura de {Blob}; se reintentará en el próximo arranque", destino.Name);
        }
        return destino.Uri.ToString();
    }

    private static Task SubirInmutableAsync(BlobClient destino, byte[] datos, string contentType, CancellationToken ct)
        => destino.UploadAsync(new BinaryData(datos), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                CacheControl = "public, max-age=31536000, immutable" // el nombre nunca se reutiliza
            },
            Metadata = new Dictionary<string, string> { [MetadatoLimpia] = "1" },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } // nunca sobrescribir
        }, ct);

    public async Task<string> GuardarImagenAsync(Guid usuarioId, byte[] contenido, CancellationToken ct = default)
    {
        ReglasArchivos.ValidarTamano(contenido.Length);
        await AsegurarContenedoresAsync(ct);
        using var flujo = new MemoryStream(contenido);
        var limpia = await ProcesadorImagenes.LimpiarConMiniaturaAsync(flujo, "jpg", ct); // siempre JPEG, sin metadatos
        return await PublicarLimpiaAsync(Contenedor(TipoArchivoDto.Imagen), usuarioId, limpia, "jpg", ct);
    }

    public async Task<int> GenerarMiniaturasFaltantesAsync(CancellationToken ct = default)
    {
        await AsegurarContenedoresAsync(ct);
        var contenedor = Contenedor(TipoArchivoDto.Imagen);
        var existentes = new HashSet<string>(StringComparer.Ordinal);
        var fotos = new List<string>();
        await foreach (var item in contenedor.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, null, ct))
        {
            existentes.Add(item.Name);
            // Solo fotos ya limpias: los originales recién subidos (aún sin validar) pueden traer GPS y se borran al validarlos.
            if (item.Metadata.TryGetValue(MetadatoLimpia, out var limpia) && limpia == "1" && ReglasArchivos.NombreMiniatura(item.Name) is not null)
                fotos.Add(item.Name);
        }

        var creadas = 0;
        foreach (var nombre in fotos)
        {
            var nombreMiniatura = ReglasArchivos.NombreMiniatura(nombre)!;
            if (existentes.Contains(nombreMiniatura)) continue;
            try
            {
                var contenido = (await contenedor.GetBlobClient(nombre).DownloadContentAsync(ct)).Value.Content;
                using var flujo = contenido.ToStream();
                var miniatura = await ProcesadorImagenes.MiniaturaAsync(flujo, ct);
                await SubirInmutableAsync(contenedor.GetBlobClient(nombreMiniatura), miniatura, "image/webp", ct);
                creadas++;
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 409)
            {
                // La foto se borró mientras tanto, u otra instancia ya creó la miniatura.
            }
            catch (Exception ex) when (ex is RequestFailedException or ReglaDeNegocioException)
            {
                _log.LogWarning(ex, "No se pudo crear la miniatura de {Blob}", nombre);
            }
        }
        if (creadas > 0) _log.LogInformation("Miniaturas creadas para fotos existentes: {Total}", creadas);
        return creadas;
    }

    /// <summary>El blob de documentos al que apunta la URL, o null si la URL no es de ese contenedor.</summary>
    private BlobClient? Documento(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return null;
        var contenedor = Contenedor(TipoArchivoDto.Documento);
        var baseContenedor = contenedor.Uri.AbsoluteUri.TrimEnd('/') + "/";
        return u.AbsoluteUri.StartsWith(baseContenedor, StringComparison.Ordinal)
            ? contenedor.GetBlobClient(Uri.UnescapeDataString(u.AbsoluteUri[baseContenedor.Length..]))
            : null;
    }

    public async Task<string?> UrlLecturaTemporalAsync(string url, CancellationToken ct = default)
        => Documento(url) is { } blob ? (await FirmarAsync(blob, BlobSasPermissions.Read, ct)).ToString() : null;

    public async Task EliminarDocumentoAsync(string url, CancellationToken ct = default)
    {
        if (Documento(url) is not { } blob) return;
        try { await blob.DeleteIfExistsAsync(cancellationToken: ct); }
        catch (RequestFailedException ex) { _log.LogWarning(ex, "No se pudo borrar un documento de verificación"); }
    }

    public async Task EliminarImagenPropiaAsync(string url, Guid usuarioId, CancellationToken ct = default)
    {
        var contenedor = Contenedor(TipoArchivoDto.Imagen);
        if (ReglasArchivos.AnalizarUrl(url, contenedor.Uri, usuarioId) is not { } analisis) return;
        try
        {
            await contenedor.DeleteBlobIfExistsAsync(analisis.Nombre, cancellationToken: ct);
            if (ReglasArchivos.NombreMiniatura(analisis.Nombre) is { } miniatura)
                await contenedor.DeleteBlobIfExistsAsync(miniatura, cancellationToken: ct);
        }
        catch (RequestFailedException ex) { _log.LogWarning(ex, "No se pudo borrar la imagen anterior del usuario {UsuarioId}", usuarioId); }
    }

    public async Task EliminarDelUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        foreach (var tipo in new[] { TipoArchivoDto.Imagen, TipoArchivoDto.Documento })
        {
            var contenedor = Contenedor(tipo);
            try
            {
                await foreach (var item in contenedor.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{usuarioId:N}/", ct))
                    await contenedor.DeleteBlobIfExistsAsync(item.Name, cancellationToken: ct);
            }
            catch (RequestFailedException ex)
            {
                _log.LogWarning(ex, "No se pudieron eliminar los archivos del usuario {UsuarioId} en {Contenedor}", usuarioId, contenedor.Name);
            }
        }
    }
}
