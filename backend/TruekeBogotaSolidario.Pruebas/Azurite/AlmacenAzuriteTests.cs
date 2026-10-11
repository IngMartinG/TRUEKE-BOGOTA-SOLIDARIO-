using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.Azurite;

/// <summary>
/// El almacén de Azure contra Azurite: lo que el navegador sube con la SAS nunca se publica tal cual, ni aunque el
/// cliente ponga metadatos de "ya limpia" o intente reemplazar el archivo después de subirlo.
/// </summary>
[Trait("Categoria", "Azurite")]
public class AlmacenAzuriteTests : IClassFixture<AzuriteFixture>
{
    private static readonly HttpClient Http = new();
    private static readonly byte[] Marca = "GPS-4.6097,-74.0817"u8.ToArray();
    private readonly AzuriteFixture _azurite;

    public AlmacenAzuriteTests(AzuriteFixture azurite) => _azurite = azurite;

    private AlmacenBlobAzure Almacen(TimeProvider? reloj = null)
        => new(Options.Create(new AlmacenamientoOpciones { CadenaConexion = _azurite.CadenaConexion }), reloj ?? TimeProvider.System,
            NullLogger<AlmacenBlobAzure>.Instance);

    /// <summary>PNG real con datos "escondidos" al final (como un EXIF con GPS): la copia limpia no debe conservarlos.</summary>
    private static byte[] FotoConDatosOcultos()
    {
        using var mapa = new SKBitmap(64, 64);
        mapa.Erase(new SKColor(200, 40, 40));
        using var datos = mapa.Encode(SKEncodedImageFormat.Png, 90);
        return [.. datos.ToArray(), .. Marca];
    }

    private static async Task<HttpResponseMessage> PutAsync(SubidaArchivoDto s, byte[] contenido, string contentType, bool marcarLimpia = false)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, s.UrlSubida) { Content = new ByteArrayContent(contenido) };
        req.Headers.Add("x-ms-blob-type", "BlockBlob");
        req.Content.Headers.ContentType = new(contentType);
        if (marcarLimpia) req.Headers.Add("x-ms-meta-limpia", "1"); // lo que haría alguien que quiere saltarse la limpieza
        return await Http.SendAsync(req);
    }

    private BlobClient Blob(string url)
    {
        var u = new Uri(url);
        return new BlobServiceClient(_azurite.CadenaConexion)
            .GetBlobContainerClient(u.Segments[2].TrimEnd('/'))
            .GetBlobClient(string.Concat(u.Segments[3..]));
    }

    private async Task<bool> ExisteAsync(string url) => (await Blob(url).ExistsAsync()).Value;
    private async Task<byte[]> DescargarAsync(string url) => (await Blob(url).DownloadContentAsync()).Value.Content.ToArray();

    [SkippableFact]
    public async Task La_marca_limpia_que_pone_el_cliente_no_evita_la_limpieza()
    {
        Skip.IfNot(_azurite.Disponible, _azurite.MotivoNoDisponible);
        var almacen = Almacen();
        var yo = Guid.NewGuid();
        var foto = FotoConDatosOcultos();
        var s = await almacen.CrearSubidaAsync(yo, TipoArchivoDto.Imagen, "image/png", foto.Length);
        Assert.Contains("/subidas/", s.UrlArchivo);
        Assert.True((await PutAsync(s, foto, "image/png", marcarLimpia: true)).IsSuccessStatusCode);

        var publicada = await almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Imagen);

        Assert.NotEqual(s.UrlArchivo, publicada);
        Assert.Contains("/imagenes/", publicada);
        Assert.True((await DescargarAsync(publicada)).AsSpan().IndexOf(Marca) < 0); // re-codificada: sin datos ocultos
        Assert.False(await ExisteAsync(s.UrlArchivo));                                // la subida se borra
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Imagen));
    }

    [SkippableFact]
    public async Task La_SAS_no_puede_reemplazar_un_archivo_ya_subido()
    {
        Skip.IfNot(_azurite.Disponible, _azurite.MotivoNoDisponible);
        var almacen = Almacen();
        var foto = FotoConDatosOcultos();
        var s = await almacen.CrearSubidaAsync(Guid.NewGuid(), TipoArchivoDto.Imagen, "image/png", foto.Length);
        Assert.True((await PutAsync(s, foto, "image/png")).IsSuccessStatusCode);

        var segunda = await PutAsync(s, "<html><script>alert(1)</script></html>"u8.ToArray(), "text/html");

        Assert.False(segunda.IsSuccessStatusCode);
        Assert.Equal(foto, await DescargarAsync(s.UrlArchivo));
    }

    [SkippableFact]
    public async Task El_documento_se_copia_al_contenedor_privado_con_otro_nombre()
    {
        Skip.IfNot(_azurite.Disponible, _azurite.MotivoNoDisponible);
        var almacen = Almacen();
        var yo = Guid.NewGuid();
        var pdf = "%PDF-1.7 cedula de prueba"u8.ToArray();
        var s = await almacen.CrearSubidaAsync(yo, TipoArchivoDto.Documento, "application/pdf", pdf.Length);
        Assert.True((await PutAsync(s, pdf, "application/pdf")).IsSuccessStatusCode);

        var guardado = await almacen.ValidarArchivoPropioAsync(s.UrlArchivo, yo, TipoArchivoDto.Documento);

        Assert.Contains("/documentos/", guardado);
        Assert.Equal(pdf, await DescargarAsync(guardado));
        Assert.False(await ExisteAsync(s.UrlArchivo));
        Assert.False((await Http.GetAsync(guardado)).IsSuccessStatusCode); // privado: sin SAS no se lee
    }

    [SkippableFact]
    public async Task Las_subidas_sin_usar_se_borran_despues_de_una_hora()
    {
        Skip.IfNot(_azurite.Disponible, _azurite.MotivoNoDisponible);
        var foto = FotoConDatosOcultos();
        var s = await Almacen().CrearSubidaAsync(Guid.NewGuid(), TipoArchivoDto.Imagen, "image/png", foto.Length);
        Assert.True((await PutAsync(s, foto, "image/png")).IsSuccessStatusCode);

        await Almacen().EliminarSubidasVencidasAsync();
        Assert.True(await ExisteAsync(s.UrlArchivo)); // recién subida: se respeta

        await Almacen(new RelojAdelantado(TimeSpan.FromHours(2))).EliminarSubidasVencidasAsync();
        Assert.False(await ExisteAsync(s.UrlArchivo));
    }

    private sealed class RelojAdelantado(TimeSpan adelanto) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + adelanto;
    }
}
