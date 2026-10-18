using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.SupabaseStorage;

/// <summary>
/// El almacén de Supabase contra el servidor de Storage real: lo que el navegador sube con la URL firmada nunca se publica
/// tal cual, no se puede reemplazar después de subirlo y los buckets rechazan por sí mismos lo que no es imagen o PDF.
/// </summary>
[Trait("Categoria", "SupabaseStorage")]
public class AlmacenSupabaseTests : IClassFixture<StorageFixture>
{
    private static readonly HttpClient Http = new();
    private static readonly byte[] Marca = "GPS-4.6097,-74.0817"u8.ToArray();
    private readonly StorageFixture _storage;

    public AlmacenSupabaseTests(StorageFixture storage) => _storage = storage;

    private AlmacenSupabase Almacen(TimeProvider? reloj = null)
        => new(Options.Create(new AlmacenamientoOpciones { Url = _storage.Url, LlaveServicio = _storage.LlaveServicio }),
            new FabricaHttp(), reloj ?? TimeProvider.System, NullLogger<AlmacenSupabase>.Instance);

    /// <summary>PNG real con datos "escondidos" al final (como un EXIF con GPS): la copia limpia no debe conservarlos.</summary>
    private static byte[] FotoConDatosOcultos()
    {
        using var mapa = new SKBitmap(64, 64);
        mapa.Erase(new SKColor(200, 40, 40));
        using var datos = mapa.Encode(SKEncodedImageFormat.Png, 90);
        return [.. datos.ToArray(), .. Marca];
    }

    /// <summary>Lo que hace el navegador: PUT del archivo a la URL firmada, con las cabeceras que dio la API.</summary>
    private static async Task<HttpResponseMessage> PutAsync(SubidaArchivoDto s, byte[] contenido, string? contentType = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, s.UrlSubida) { Content = new ByteArrayContent(contenido) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? s.Cabeceras["Content-Type"]);
        return await Http.SendAsync(req);
    }

    /// <summary>
    /// Storage rechaza con un error o, si el archivo es muy grande, dejando de leerlo (corta o deja colgada la conexión).
    /// Quien llama comprueba además que el archivo no quedó guardado.
    /// </summary>
    private static async Task<bool> RechazadoAsync(SubidaArchivoDto s, byte[] contenido, string? contentType = null)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, s.UrlSubida) { Content = new ByteArrayContent(contenido) };
            req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? s.Cabeceras["Content-Type"]);
            return !(await Http.SendAsync(req, limite.Token)).IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return true; }
    }

    /// <summary>Lectura con la llave de servicio (como el servidor), sin importar si el bucket es público.</summary>
    private async Task<HttpResponseMessage> LeerComoServidorAsync(string url)
    {
        var ruta = new Uri(url).AbsolutePath.Replace("/object/public/", "/object/authenticated/", StringComparison.Ordinal);
        using var req = new HttpRequestMessage(HttpMethod.Get, _storage.Url + ruta);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _storage.LlaveServicio);
        return await Http.SendAsync(req);
    }

    private async Task<bool> ExisteAsync(string url) => (await LeerComoServidorAsync(url)).IsSuccessStatusCode;

    [SkippableFact]
    public async Task La_foto_subida_se_publica_limpia_con_otro_nombre_miniatura_y_cache_inmutable()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var almacen = Almacen();
        var yo = Guid.NewGuid();
        var foto = FotoConDatosOcultos();
        var s = await almacen.CrearSubidaAsync(yo, TipoArchivoDto.Imagen, "image/png", foto.Length);
        Assert.Contains("/subidas/", s.UrlArchivo);
        Assert.True(s.ExpiraUtc > DateTime.UtcNow); // vencimiento real del token, leído de Supabase
        var put = await PutAsync(s, foto);
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());
        Assert.False((await Http.GetAsync(s.UrlArchivo)).IsSuccessStatusCode); // la subida es privada

        var publicada = await almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Imagen);

        Assert.NotEqual(s.UrlArchivo, publicada);
        Assert.Contains("/object/public/imagenes/", publicada);
        var get = await Http.GetAsync(publicada); // bucket público: se lee sin llave
        Assert.True(get.IsSuccessStatusCode);
        Assert.Equal("public, max-age=31536000, immutable", get.Headers.CacheControl?.ToString());
        Assert.True((await get.Content.ReadAsByteArrayAsync()).AsSpan().IndexOf(Marca) < 0); // re-codificada: sin datos ocultos
        var miniatura = await Http.GetAsync(publicada.Replace(".png", ReglasArchivos.SufijoMiniatura, StringComparison.Ordinal));
        Assert.True(miniatura.IsSuccessStatusCode);
        Assert.Equal("image/webp", miniatura.Content.Headers.ContentType?.MediaType);
        Assert.False(await ExisteAsync(s.UrlArchivo));                          // la subida se borra
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Imagen));
        Assert.Equal(publicada, await almacen.ValidarArchivoPropioAsync(publicada, yo, TipoArchivoDto.Imagen)); // al editar: la misma
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => almacen.ValidarArchivoPropioAsync(publicada, Guid.NewGuid(), TipoArchivoDto.Imagen));
    }

    [SkippableFact]
    public async Task La_URL_firmada_no_puede_reemplazar_un_archivo_ya_subido()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var almacen = Almacen();
        var foto = FotoConDatosOcultos();
        var s = await almacen.CrearSubidaAsync(Guid.NewGuid(), TipoArchivoDto.Imagen, "image/png", foto.Length);
        Assert.True((await PutAsync(s, foto)).IsSuccessStatusCode);

        var segunda = await PutAsync(s, FotoConDatosOcultos()[..^4]);

        Assert.False(segunda.IsSuccessStatusCode);
        Assert.Equal(foto, await (await LeerComoServidorAsync(s.UrlArchivo)).Content.ReadAsByteArrayAsync());
    }

    [SkippableFact]
    public async Task El_bucket_de_subidas_rechaza_otros_tipos_y_mas_de_5_MB()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var almacen = Almacen();
        var html = await almacen.CrearSubidaAsync(Guid.NewGuid(), TipoArchivoDto.Imagen, "image/png", 100);
        Assert.True(await RechazadoAsync(html, "<html><script>alert(1)</script></html>"u8.ToArray(), "text/html"));
        Assert.False(await ExisteAsync(html.UrlArchivo));

        var grande = await almacen.CrearSubidaAsync(Guid.NewGuid(), TipoArchivoDto.Imagen, "image/png", 100);
        Assert.True(await RechazadoAsync(grande, new byte[ReglasArchivos.TamanoMaximoBytes + 1]));
        Assert.False(await ExisteAsync(grande.UrlArchivo));
    }

    [SkippableFact]
    public async Task El_contenido_disfrazado_se_rechaza_y_se_borra()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var almacen = Almacen();
        var yo = Guid.NewGuid();
        var s = await almacen.CrearSubidaAsync(yo, TipoArchivoDto.Imagen, "image/png", 100);
        Assert.True((await PutAsync(s, "<html><script>alert(1)</script></html>"u8.ToArray())).IsSuccessStatusCode); // dice ser PNG

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Imagen));
        Assert.False(await ExisteAsync(s.UrlArchivo));
    }

    [SkippableFact]
    public async Task El_documento_se_copia_al_bucket_privado_y_solo_se_lee_con_URL_temporal()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var almacen = Almacen();
        var yo = Guid.NewGuid();
        var pdf = "%PDF-1.7 cedula de prueba"u8.ToArray();
        var s = await almacen.CrearSubidaAsync(yo, TipoArchivoDto.Documento, "application/pdf", pdf.Length);
        Assert.True((await PutAsync(s, pdf)).IsSuccessStatusCode);

        var guardado = await almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Documento);

        Assert.Contains("/documentos/", guardado);
        Assert.False(await ExisteAsync(s.UrlArchivo));
        Assert.False((await Http.GetAsync(guardado)).IsSuccessStatusCode);                                 // privado: sin firma no se lee
        Assert.False((await Http.GetAsync(guardado.Replace("/authenticated/", "/public/"))).IsSuccessStatusCode);
        var temporal = await almacen.UrlLecturaTemporalAsync(guardado);
        Assert.Contains("token=", temporal);
        Assert.Equal(pdf, await Http.GetByteArrayAsync(temporal));

        await almacen.EliminarDocumentoAsync(guardado);
        Assert.False(await ExisteAsync(guardado));
    }

    [SkippableFact]
    public async Task Con_la_llave_publica_nadie_puede_escribir_en_los_buckets()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        await Almacen().GuardarImagenAsync(Guid.NewGuid(), FotoConDatosOcultos()); // crea los buckets

        foreach (var ruta in new[] { "object/imagenes/x/a.png", "object/upload/sign/subidas/x/a.png" })
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_storage.Url}/{ruta}") { Content = new ByteArrayContent(FotoConDatosOcultos()) };
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _storage.LlaveAnonima);
            Assert.False((await Http.SendAsync(req)).IsSuccessStatusCode, ruta);
        }
    }

    [SkippableFact]
    public async Task Las_subidas_sin_usar_se_borran_despues_de_una_hora()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var foto = FotoConDatosOcultos();
        var s = await Almacen().CrearSubidaAsync(Guid.NewGuid(), TipoArchivoDto.Imagen, "image/png", foto.Length);
        Assert.True((await PutAsync(s, foto)).IsSuccessStatusCode);

        await Almacen().EliminarSubidasVencidasAsync();
        Assert.True(await ExisteAsync(s.UrlArchivo)); // recién subida: se respeta

        Assert.True(await Almacen(new RelojAdelantado(TimeSpan.FromHours(2))).EliminarSubidasVencidasAsync() >= 1);
        Assert.False(await ExisteAsync(s.UrlArchivo));
    }

    [SkippableFact]
    public async Task Al_borrar_la_cuenta_se_borran_sus_fotos_y_las_miniaturas_faltantes_se_regeneran()
    {
        Skip.IfNot(_storage.Disponible, _storage.MotivoNoDisponible);
        var almacen = Almacen();
        var yo = Guid.NewGuid();
        var foto = await almacen.GuardarImagenAsync(yo, FotoConDatosOcultos());
        var miniatura = foto.Replace(".jpg", ReglasArchivos.SufijoMiniatura, StringComparison.Ordinal);
        Assert.NotNull(await almacen.LeerFotoPublicadaAsync(foto));

        // Sin miniatura (p. ej. falló al publicar): el relleno la vuelve a crear.
        await almacen.EliminarImagenPropiaAsync(foto, yo);
        var otra = await almacen.GuardarImagenAsync(yo, FotoConDatosOcultos());
        var otraMiniatura = otra.Replace(".jpg", ReglasArchivos.SufijoMiniatura, StringComparison.Ordinal);
        using (var req = new HttpRequestMessage(HttpMethod.Delete, $"{_storage.Url}/object/imagenes/{new Uri(otraMiniatura).Segments[^2]}{new Uri(otraMiniatura).Segments[^1]}"))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _storage.LlaveServicio);
            Assert.True((await Http.SendAsync(req)).IsSuccessStatusCode);
        }
        Assert.False(await ExisteAsync(otraMiniatura));
        Assert.True(await almacen.GenerarMiniaturasFaltantesAsync() >= 1);
        Assert.True(await ExisteAsync(otraMiniatura));
        Assert.False(await ExisteAsync(foto));
        Assert.False(await ExisteAsync(miniatura));

        await almacen.EliminarDelUsuarioAsync(yo);
        Assert.False(await ExisteAsync(otra));
        Assert.False(await ExisteAsync(otraMiniatura));
        Assert.Null(await almacen.LeerFotoPublicadaAsync(otra));
        Assert.Null(await almacen.LeerFotoPublicadaAsync("https://otro.example/x.jpg")); // nunca sigue otras URLs
    }

    private sealed class FabricaHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => Http;
    }

    private sealed class RelojAdelantado(TimeSpan adelanto) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + adelanto;
    }
}
