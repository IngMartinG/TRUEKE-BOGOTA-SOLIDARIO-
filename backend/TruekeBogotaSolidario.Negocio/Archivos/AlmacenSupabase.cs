using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>Error que devuelve la API de Storage (su código va en el cuerpo: 404 no existe, 409 ya existe…).</summary>
public sealed class ErrorAlmacenamientoException(int codigo, string mensaje) : Exception(mensaje)
{
    public int Codigo { get; } = codigo;
}

/// <summary>
/// Supabase Storage por su API REST, con la llave secreta del proyecto (solo en el servidor). El navegador sube con una URL
/// firmada (createSignedUploadUrl): sirve para UN archivo nuevo, con nombre elegido por el servidor, en el bucket PRIVADO de
/// subidas, y no puede sobrescribir. El bucket rechaza por sí mismo más de 5 MB y los tipos no permitidos, y aun así todo se
/// vuelve a verificar al usar la URL. Lo que se publica es siempre una copia hecha por el servidor con otro nombre.
/// </summary>
public sealed class AlmacenSupabase : IAlmacenArchivos
{
    public const string ClienteHttp = "almacenamiento";
    /// <summary>Una subida que nadie usó en este tiempo se borra (el formulario ya la habría enviado).</summary>
    private static readonly TimeSpan VidaSubida = TimeSpan.FromHours(1);
    /// <summary>Vigencia de la URL de lectura de un documento privado (moderadores).</summary>
    private const int SegundosLecturaTemporal = 300;
    /// <summary>Vigencia de las URLs de las fotos del chat: alcanza para una sesión de chat sin quedar abiertas por días.</summary>
    private const int SegundosLecturaChat = 3600;
    private const string CachePublica = "public, max-age=31536000, immutable"; // el nombre nunca se reutiliza
    private const int TamanoPagina = 1000;

    private static readonly string[] TiposImagen = ["image/jpeg", "image/png", "image/webp"];
    private static readonly string[] TiposDocumento = [.. TiposImagen, "application/pdf"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AlmacenamientoOpciones _o;
    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AlmacenSupabase> _log;
    private readonly string _base;
    private volatile bool _bucketsListos;

    public AlmacenSupabase(IOptions<AlmacenamientoOpciones> o, IHttpClientFactory http, TimeProvider reloj, ILogger<AlmacenSupabase> log)
    {
        _o = o.Value;
        _http = http;
        _reloj = reloj;
        _log = log;
        _base = _o.Url.TrimEnd('/');
    }

    public bool Habilitado => true;

    private string BucketDe(TipoArchivoDto tipo) => tipo == TipoArchivoDto.Documento ? _o.BucketDocumentos : _o.BucketImagenes;

    /// <summary>Prefijo de las URLs públicas de las fotos (lo que se guarda en la base).</summary>
    private Uri BaseImagenes => new($"{_base}/object/public/{_o.BucketImagenes}");
    /// <summary>Prefijo de los archivos privados: identifican el archivo, pero sin la llave no se pueden leer.</summary>
    private Uri BasePrivada(string bucket) => new($"{_base}/object/authenticated/{bucket}");

    // ---------- API REST ----------

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, HttpContent? contenido, CancellationToken ct,
        HttpCompletionOption completar = HttpCompletionOption.ResponseContentRead, Action<HttpRequestHeaders>? cabeceras = null)
    {
        using var req = new HttpRequestMessage(metodo, $"{_base}/{ruta}") { Content = contenido };
        req.Headers.Add("apikey", _o.LlaveServicio);
        // Las llaves nuevas (sb_secret_…) van SOLO en "apikey": no son JWT y el gateway de Supabase arma el token del rol.
        // Las JWT (service_role antigua, o el Storage local sin gateway) van además como Bearer.
        if (EsJwt(_o.LlaveServicio)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.LlaveServicio);
        cabeceras?.Invoke(req.Headers);
        return await _http.CreateClient(ClienteHttp).SendAsync(req, completar, ct);
    }

    internal static bool EsJwt(string llave) => llave.StartsWith("eyJ", StringComparison.Ordinal) && llave.Count(c => c == '.') == 2;

    /// <summary>Lanza <see cref="ErrorAlmacenamientoException"/> si la respuesta es un error (Storage pone el código real en el cuerpo).</summary>
    private static async Task ExigirExitoAsync(HttpResponseMessage r, CancellationToken ct)
    {
        if (r.IsSuccessStatusCode) return;
        var codigo = (int)r.StatusCode;
        var mensaje = r.ReasonPhrase ?? "";
        try
        {
            var error = await r.Content.ReadFromJsonAsync<ErrorStorage>(Json, ct);
            if (int.TryParse(error?.StatusCode, out var c)) codigo = c;
            mensaje = error?.Message ?? mensaje;
        }
        catch (JsonException) { }
        throw new ErrorAlmacenamientoException(codigo, $"Storage respondió {codigo}: {mensaje}");
    }

    private async Task<T> LeerAsync<T>(HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        using var r = await EnviarAsync(metodo, ruta, cuerpo is null ? null : JsonContent.Create(cuerpo, options: Json), ct);
        await ExigirExitoAsync(r, ct);
        return (await r.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private async Task SinRespuestaAsync(HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        using var r = await EnviarAsync(metodo, ruta, cuerpo is null ? null : JsonContent.Create(cuerpo, options: Json), ct);
        await ExigirExitoAsync(r, ct);
    }

    /// <summary>Descarga como máximo 5 MB (lee 1 byte de más para detectar si el archivo es mayor). Devuelve también su tipo.</summary>
    private async Task<(byte[] Datos, string ContentType)> DescargarAsync(string bucket, string nombre, CancellationToken ct)
    {
        using var r = await EnviarAsync(HttpMethod.Get, $"object/authenticated/{bucket}/{nombre}", null, ct, HttpCompletionOption.ResponseHeadersRead);
        await ExigirExitoAsync(r, ct);
        if (r.Content.Headers.ContentLength > ReglasArchivos.TamanoMaximoBytes)
            throw new ReglaDeNegocioException("El archivo supera el máximo de 5 MB.");
        await using var flujo = await r.Content.ReadAsStreamAsync(ct);
        using var destino = new MemoryStream();
        var bufer = new byte[81920];
        int leidos;
        while ((leidos = await flujo.ReadAsync(bufer, ct)) > 0)
        {
            destino.Write(bufer, 0, leidos);
            if (destino.Length > ReglasArchivos.TamanoMaximoBytes) throw new ReglaDeNegocioException("El archivo supera el máximo de 5 MB.");
        }
        return (destino.ToArray(), r.Content.Headers.ContentType?.MediaType ?? "");
    }

    /// <summary>Solo el servidor escribe así: nombre nuevo y nunca sobrescribe (sin x-upsert, un nombre repetido da 409).</summary>
    private async Task SubirInmutableAsync(string bucket, string nombre, byte[] datos, string contentType, string cacheControl, CancellationToken ct)
    {
        var contenido = new ByteArrayContent(datos);
        contenido.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var r = await EnviarAsync(HttpMethod.Post, $"object/{bucket}/{nombre}", contenido, ct, cabeceras: h =>
        {
            h.Add("x-upsert", "false");
            h.TryAddWithoutValidation("Cache-Control", cacheControl); // Storage la guarda y la devuelve al servir el archivo
        });
        await ExigirExitoAsync(r, ct);
    }

    /// <summary>Borra varios archivos de un bucket (los que no existen se ignoran).</summary>
    private Task BorrarAsync(string bucket, IReadOnlyCollection<string> nombres, CancellationToken ct)
        => nombres.Count == 0 ? Task.CompletedTask : SinRespuestaAsync(HttpMethod.Delete, $"object/{bucket}", new { prefixes = nombres }, ct);

    /// <summary>
    /// Archivos de un bucket ("{usuario}/{archivo}"), con su fecha de creación. Storage lista por carpetas: primero las de
    /// usuario y luego el contenido de cada una (o solo la de <paramref name="carpeta"/>).
    /// </summary>
    private async IAsyncEnumerable<(string Nombre, DateTimeOffset? Creado)> ListarAsync(string bucket, string? carpeta,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        List<string> carpetas = carpeta is not null ? [carpeta] : [];
        if (carpeta is null)
            await foreach (var c in ListarNivelAsync(bucket, "", ct))
                if (c.Id is null) carpetas.Add(c.Name);
        foreach (var c in carpetas)
            await foreach (var a in ListarNivelAsync(bucket, c + "/", ct))
                if (a.Id is not null) yield return ($"{c}/{a.Name}", a.CreatedAt);
    }

    private async IAsyncEnumerable<ElementoStorage> ListarNivelAsync(string bucket, string prefijo,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (var desde = 0; ; desde += TamanoPagina)
        {
            var pagina = await LeerAsync<List<ElementoStorage>>(HttpMethod.Post, $"object/list/{bucket}",
                new { prefix = prefijo, limit = TamanoPagina, offset = desde, sortBy = new { column = "name", order = "asc" } }, ct);
            foreach (var e in pagina) yield return e;
            if (pagina.Count < TamanoPagina) yield break;
        }
    }

    /// <summary>
    /// Crea los buckets si no existen y les fija la configuración: imágenes público (solo lectura: sin políticas en
    /// storage.objects nadie más que el servidor escribe), documentos y subidas privados; los tres con 5 MB y tipos permitidos.
    /// </summary>
    private async Task AsegurarBucketsAsync(CancellationToken ct)
    {
        if (_bucketsListos) return;
        try
        {
            await AsegurarBucketAsync(_o.BucketImagenes, publico: true, TiposImagen, ct);
            await AsegurarBucketAsync(_o.BucketDocumentos, publico: false, TiposDocumento, ct);
            await AsegurarBucketAsync(_o.BucketSubidas, publico: false, TiposDocumento, ct);
            await AsegurarBucketAsync(_o.BucketChat, publico: false, TiposImagen, ct);
            _bucketsListos = true;
        }
        catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
        {
            _log.LogWarning(ex, "No se pudieron crear/verificar los buckets de Storage; se reintentará en la próxima operación");
        }
    }

    private async Task AsegurarBucketAsync(string id, bool publico, string[] tipos, CancellationToken ct)
    {
        var config = new { id, name = id, @public = publico, file_size_limit = ReglasArchivos.TamanoMaximoBytes, allowed_mime_types = tipos };
        try
        {
            await SinRespuestaAsync(HttpMethod.Post, "bucket", config, ct);
            _log.LogInformation("Bucket de Storage {Bucket} creado (público: {Publico})", id, publico);
        }
        catch (ErrorAlmacenamientoException ex) when (ex.Codigo == 409)
        {
            // Ya existía: se le vuelve a fijar la configuración (p. ej. si alguien lo cambió a mano en el panel).
            await SinRespuestaAsync(HttpMethod.Put, $"bucket/{id}", config, ct);
        }
    }

    // ---------- IAlmacenArchivos ----------

    public async Task<SubidaArchivoDto> CrearSubidaAsync(Guid usuarioId, TipoArchivoDto tipo, string contentType, long tamanoBytes, CancellationToken ct = default)
    {
        var ext = ReglasArchivos.Extension(tipo, contentType);
        ReglasArchivos.ValidarTamano(tamanoBytes);
        await AsegurarBucketsAsync(ct);
        var nombre = ReglasArchivos.NuevoNombre(usuarioId, ext);
        var firmada = await LeerAsync<UrlFirmada>(HttpMethod.Post, $"object/upload/sign/{_o.BucketSubidas}/{nombre}", null, ct);
        var cabeceras = new Dictionary<string, string> { ["Content-Type"] = contentType.ToLowerInvariant() };
        return new SubidaArchivoDto(_base + firmada.Url, $"{BasePrivada(_o.BucketSubidas)}/{nombre}", "PUT", cabeceras,
            Vencimiento(firmada.Token) ?? _reloj.GetUtcNow().UtcDateTime.AddMinutes(1));
    }

    /// <summary>Vencimiento real de la URL firmada (lo fija Supabase: no se puede elegir), leído del token.</summary>
    internal static DateTime? Vencimiento(string? token)
    {
        var partes = token?.Split('.');
        if (partes is not { Length: 3 }) return null;
        try
        {
            var carga = partes[1].Replace('-', '+').Replace('_', '/');
            carga = carga.PadRight(carga.Length + (4 - carga.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(carga));
            return json.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var s)
                ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    public async Task<string> ValidarArchivoPropioAsync(string url, Guid usuarioId, TipoArchivoDto tipo, CancellationToken ct = default)
    {
        await AsegurarBucketsAsync(ct);
        if (ReglasArchivos.AnalizarUrl(url, BasePrivada(_o.BucketSubidas), usuarioId) is not { } subida)
        {
            // Una foto propia que ya está publicada (p. ej. al editar una publicación): solo el servidor escribe en ese
            // bucket y siempre con fotos limpias, así que se acepta tal cual si existe.
            if (tipo == TipoArchivoDto.Imagen && ReglasArchivos.AnalizarUrl(url, BaseImagenes, usuarioId) is { } publicada)
                return await ExisteAsync(_o.BucketImagenes, publicada.Nombre, ct)
                    ? url
                    : throw new ReglaDeNegocioException("La imagen ya no existe. Súbela de nuevo.");
            throw new ReglaDeNegocioException("El archivo debe subirse primero con POST /api/v1/archivos/subidas.");
        }

        byte[] contenido;
        string contentType;
        try
        {
            // Se descarga UNA vez y se valida y copia ese contenido. La URL firmada no puede reemplazar lo ya subido.
            (contenido, contentType) = await DescargarAsync(_o.BucketSubidas, subida.Nombre, ct);
        }
        catch (ErrorAlmacenamientoException ex) when (ex.Codigo == 404)
        {
            throw new ReglaDeNegocioException("El archivo no existe o aún no terminó de subirse.");
        }
        catch (ReglaDeNegocioException)
        {
            await BorrarAsync(_o.BucketSubidas, [subida.Nombre], ct); // supera el tamaño: no se conserva
            throw;
        }

        string final;
        try
        {
            ReglasArchivos.ValidarTamano(contenido.Length);
            if (ReglasArchivos.Extension(tipo, contentType) != subida.Extension)
                throw new ReglaDeNegocioException("El tipo del archivo no coincide con su extensión.");
            if (!ReglasArchivos.FirmaCoincide(subida.Extension, contenido.AsSpan(0, Math.Min(ReglasArchivos.BytesFirma, contenido.Length))))
                throw new ReglaDeNegocioException("El contenido del archivo no corresponde a una imagen o documento válido.");

            if (tipo == TipoArchivoDto.Imagen)
            {
                // Siempre se publica una copia limpia (sin GPS ni metadatos) con un nombre NUEVO en el bucket público.
                using var flujo = new MemoryStream(contenido, writable: false);
                var limpia = await ProcesadorImagenes.LimpiarConMiniaturaAsync(flujo, subida.Extension, ct);
                final = await PublicarLimpiaAsync(usuarioId, limpia, subida.Extension, ct);
                _log.LogInformation("Imagen limpiada y publicada ({Antes} → {Despues} bytes, miniatura {Miniatura} bytes)",
                    contenido.Length, limpia.Foto.Length, limpia.Miniatura.Length);
            }
            else if (tipo == TipoArchivoDto.FotoChat)
            {
                // Foto del chat: misma limpieza (sin GPS ni metadatos), pero a un bucket PRIVADO.
                using var flujo = new MemoryStream(contenido, writable: false);
                var limpia = await ProcesadorImagenes.LimpiarAsync(flujo, subida.Extension, ct);
                var nombre = ReglasArchivos.NuevoNombre(usuarioId, subida.Extension);
                await SubirInmutableAsync(_o.BucketChat, nombre, limpia, TipoImagen(subida.Extension), "private, no-store", ct);
                final = $"{BasePrivada(_o.BucketChat)}/{nombre}";
            }
            else
            {
                // Documento: copia del servidor con nombre nuevo en el bucket privado (ninguna URL de subida llega ahí).
                var nombre = ReglasArchivos.NuevoNombre(usuarioId, subida.Extension);
                await SubirInmutableAsync(_o.BucketDocumentos, nombre, contenido, contentType, "private, no-store", ct);
                final = $"{BasePrivada(_o.BucketDocumentos)}/{nombre}";
            }
        }
        catch (ReglaDeNegocioException)
        {
            await BorrarAsync(_o.BucketSubidas, [subida.Nombre], ct); // inválida, disfrazada, corrupta o "bomba": no se conserva
            throw;
        }

        // La subida ya no sirve: lo publicado es la copia del servidor.
        await BorrarAsync(_o.BucketSubidas, [subida.Nombre], ct);
        return final;
    }

    private async Task<bool> ExisteAsync(string bucket, string nombre, CancellationToken ct)
    {
        using var r = await EnviarAsync(HttpMethod.Get, $"object/info/authenticated/{bucket}/{nombre}", null, ct);
        if (r.IsSuccessStatusCode) return true;
        try { await ExigirExitoAsync(r, ct); }
        catch (ErrorAlmacenamientoException ex) when (ex.Codigo == 404) { return false; }
        return false;
    }

    /// <summary>
    /// Sube una imagen YA limpia con un nombre nuevo del usuario (nunca sobrescribe) y su miniatura; devuelve la URL pública
    /// de la foto. Si la miniatura falla, la foto se publica igual: el front usa la foto grande y el relleno la crea después.
    /// </summary>
    private async Task<string> PublicarLimpiaAsync(Guid usuarioId, ProcesadorImagenes.ImagenLimpia limpia, string extension, CancellationToken ct)
    {
        var nombre = ReglasArchivos.NuevoNombre(usuarioId, extension);
        await SubirInmutableAsync(_o.BucketImagenes, nombre, limpia.Foto, TipoImagen(extension), CachePublica, ct);
        try
        {
            await SubirInmutableAsync(_o.BucketImagenes, ReglasArchivos.NombreMiniatura(nombre)!, limpia.Miniatura, "image/webp", CachePublica, ct);
        }
        catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
        {
            _log.LogWarning(ex, "No se pudo publicar la miniatura de {Archivo}; se reintentará en el próximo arranque", nombre);
        }
        return $"{BaseImagenes}/{nombre}";
    }

    private static string TipoImagen(string extension) => extension switch { "png" => "image/png", "webp" => "image/webp", _ => "image/jpeg" };

    public async Task<string> GuardarImagenAsync(Guid usuarioId, byte[] contenido, CancellationToken ct = default)
    {
        ReglasArchivos.ValidarTamano(contenido.Length);
        await AsegurarBucketsAsync(ct);
        using var flujo = new MemoryStream(contenido);
        var limpia = await ProcesadorImagenes.LimpiarConMiniaturaAsync(flujo, "jpg", ct); // siempre JPEG, sin metadatos
        return await PublicarLimpiaAsync(usuarioId, limpia, "jpg", ct);
    }

    public async Task<int> EliminarSubidasVencidasAsync(CancellationToken ct = default)
    {
        await AsegurarBucketsAsync(ct);
        var limite = _reloj.GetUtcNow() - VidaSubida;
        var vencidas = new List<string>();
        try
        {
            await foreach (var (nombre, creado) in ListarAsync(_o.BucketSubidas, null, ct))
                if (creado is { } c && c < limite) vencidas.Add(nombre);
            foreach (var lote in vencidas.Chunk(TamanoPagina)) await BorrarAsync(_o.BucketSubidas, lote, ct);
        }
        catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
        {
            _log.LogWarning(ex, "No se pudieron borrar las subidas vencidas; se reintentará más tarde");
            return 0;
        }
        if (vencidas.Count > 0) _log.LogInformation("Subidas sin usar borradas: {Total}", vencidas.Count);
        return vencidas.Count;
    }

    public async Task<int> GenerarMiniaturasFaltantesAsync(CancellationToken ct = default)
    {
        await AsegurarBucketsAsync(ct);
        var existentes = new HashSet<string>(StringComparer.Ordinal);
        var fotos = new List<string>();
        await foreach (var (nombre, _) in ListarAsync(_o.BucketImagenes, null, ct))
        {
            existentes.Add(nombre);
            if (ReglasArchivos.NombreMiniatura(nombre) is not null) fotos.Add(nombre);
        }

        var creadas = 0;
        foreach (var nombre in fotos)
        {
            var nombreMiniatura = ReglasArchivos.NombreMiniatura(nombre)!;
            if (existentes.Contains(nombreMiniatura)) continue;
            try
            {
                var (contenido, _) = await DescargarAsync(_o.BucketImagenes, nombre, ct);
                using var flujo = new MemoryStream(contenido, writable: false);
                var miniatura = await ProcesadorImagenes.MiniaturaAsync(flujo, ct);
                await SubirInmutableAsync(_o.BucketImagenes, nombreMiniatura, miniatura, "image/webp", CachePublica, ct);
                creadas++;
            }
            catch (ErrorAlmacenamientoException ex) when (ex.Codigo is 404 or 409)
            {
                // La foto se borró mientras tanto, u otra instancia ya creó la miniatura.
            }
            catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException or ReglaDeNegocioException)
            {
                _log.LogWarning(ex, "No se pudo crear la miniatura de {Archivo}", nombre);
            }
        }
        if (creadas > 0) _log.LogInformation("Miniaturas creadas para fotos existentes: {Total}", creadas);
        return creadas;
    }

    public async Task<byte[]?> LeerFotoPublicadaAsync(string url, CancellationToken ct = default)
    {
        if (ReglasArchivos.NombreFotoPublicada(url, BaseImagenes) is not { } nombre) return null;
        try
        {
            var (datos, _) = await DescargarAsync(_o.BucketImagenes, nombre, ct);
            return datos.Length > 0 ? datos : null;
        }
        catch (ErrorAlmacenamientoException ex) when (ex.Codigo == 404)
        {
            return null;
        }
        catch (ReglaDeNegocioException)
        {
            return null; // supera el tamaño máximo
        }
    }

    /// <summary>El nombre del documento privado al que apunta la URL, o null si la URL no es de ese bucket.</summary>
    private string? Documento(string url) => ReglasArchivos.NombreArchivo(url, BasePrivada(_o.BucketDocumentos));

    public async Task<string?> UrlLecturaTemporalAsync(string url, CancellationToken ct = default)
    {
        if (Documento(url) is not { } nombre) return null;
        var firmada = await LeerAsync<UrlLectura>(HttpMethod.Post, $"object/sign/{_o.BucketDocumentos}/{nombre}",
            new { expiresIn = SegundosLecturaTemporal }, ct);
        return _base + firmada.SignedUrl;
    }

    public async Task<IReadOnlyDictionary<string, string>> UrlsLecturaChatAsync(IReadOnlyCollection<string> urls, CancellationToken ct = default)
    {
        var resultado = new Dictionary<string, string>(StringComparer.Ordinal);
        var porNombre = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var url in urls)
            if (ReglasArchivos.NombreArchivo(url, BasePrivada(_o.BucketChat)) is { } nombre) porNombre.TryAdd(nombre, url);
        if (porNombre.Count == 0) return resultado;
        try
        {
            // Una sola llamada para toda la página de mensajes; las que fallan (archivo borrado) vienen con "error".
            var firmadas = await LeerAsync<List<UrlLecturaLote>>(HttpMethod.Post, $"object/sign/{_o.BucketChat}",
                new { expiresIn = SegundosLecturaChat, paths = porNombre.Keys }, ct);
            foreach (var f in firmadas)
                if (f.Error is null && f.SignedUrl is { } s && f.Path is { } p && porNombre.TryGetValue(p, out var original))
                    resultado[original] = _base + s;
        }
        catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
        {
            // Sin URL la foto se muestra como "no disponible"; el texto del chat sigue funcionando.
            _log.LogWarning(ex, "No se pudieron firmar las fotos del chat");
        }
        return resultado;
    }

    public async Task EliminarDocumentoAsync(string url, CancellationToken ct = default)
    {
        if (Documento(url) is not { } nombre) return;
        try { await BorrarAsync(_o.BucketDocumentos, [nombre], ct); }
        catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
        {
            _log.LogWarning(ex, "No se pudo borrar un documento de verificación");
        }
    }

    public async Task EliminarImagenPropiaAsync(string url, Guid usuarioId, CancellationToken ct = default)
    {
        if (ReglasArchivos.AnalizarUrl(url, BaseImagenes, usuarioId) is not { } analisis) return;
        try
        {
            var nombres = new List<string> { analisis.Nombre };
            if (ReglasArchivos.NombreMiniatura(analisis.Nombre) is { } miniatura) nombres.Add(miniatura);
            await BorrarAsync(_o.BucketImagenes, nombres, ct);
        }
        catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
        {
            _log.LogWarning(ex, "No se pudo borrar la imagen anterior del usuario {UsuarioId}", usuarioId);
        }
    }

    public async Task EliminarDelUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        foreach (var bucket in new[] { _o.BucketImagenes, _o.BucketDocumentos, _o.BucketSubidas, _o.BucketChat })
        {
            try
            {
                var nombres = new List<string>();
                await foreach (var (nombre, _) in ListarAsync(bucket, usuarioId.ToString("N"), ct)) nombres.Add(nombre);
                foreach (var lote in nombres.Chunk(TamanoPagina)) await BorrarAsync(bucket, lote, ct);
            }
            catch (Exception ex) when (ex is ErrorAlmacenamientoException or HttpRequestException)
            {
                _log.LogWarning(ex, "No se pudieron eliminar los archivos del usuario {UsuarioId} en {Bucket}", usuarioId, bucket);
            }
        }
    }

    // ---------- Respuestas de la API ----------

    private sealed record ErrorStorage(string? StatusCode, string? Error, string? Message);
    private sealed record UrlFirmada(string Url, string? Token);
    private sealed record UrlLectura([property: JsonPropertyName("signedURL")] string SignedUrl);
    private sealed record UrlLecturaLote(string? Path, [property: JsonPropertyName("signedURL")] string? SignedUrl, string? Error);
    private sealed record ElementoStorage(string Name, string? Id, [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt);
}
