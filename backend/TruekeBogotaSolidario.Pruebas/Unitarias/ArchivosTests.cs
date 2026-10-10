using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class ArchivosTests
{
    private static readonly Uri Contenedor = new("https://cuenta.blob.core.windows.net/imagenes");
    private static readonly Guid Yo = Guid.NewGuid();

    [Theory]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("IMAGE/PNG", "png")]
    [InlineData("image/webp", "webp")]
    public void Imagenes_permitidas(string tipo, string ext) => Assert.Equal(ext, ReglasArchivos.Extension(TipoArchivoDto.Imagen, tipo));

    [Theory]
    [InlineData("image/svg+xml")]   // SVG puede llevar JavaScript
    [InlineData("text/html")]
    [InlineData("application/pdf")] // PDF solo para documentos
    [InlineData("")]
    public void Tipos_no_permitidos_para_imagen(string tipo)
        => Assert.Throws<ReglaDeNegocioException>(() => ReglasArchivos.Extension(TipoArchivoDto.Imagen, tipo));

    [Fact]
    public void Documentos_admiten_pdf() => Assert.Equal("pdf", ReglasArchivos.Extension(TipoArchivoDto.Documento, "application/pdf"));

    [Theory]
    [InlineData(0)]
    [InlineData(5 * 1024 * 1024 + 1)]
    public void Tamano_fuera_de_rango(long bytes) => Assert.Throws<ReglaDeNegocioException>(() => ReglasArchivos.ValidarTamano(bytes));

    [Fact]
    public void Firmas_binarias()
    {
        Assert.True(ReglasArchivos.FirmaCoincide("jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.True(ReglasArchivos.FirmaCoincide("png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }));
        Assert.True(ReglasArchivos.FirmaCoincide("webp", "RIFF\0\0\0\0WEBP"u8.ToArray()));
        Assert.True(ReglasArchivos.FirmaCoincide("pdf", "%PDF-1.7"u8.ToArray()));
        Assert.False(ReglasArchivos.FirmaCoincide("jpg", "<html><script>"u8.ToArray()));   // HTML disfrazado de .jpg
        Assert.False(ReglasArchivos.FirmaCoincide("png", new byte[] { 0xFF, 0xD8, 0xFF }));   // JPEG con extensión .png
    }

    [Fact]
    public void Url_propia_se_acepta_y_las_demas_no()
    {
        var nombre = ReglasArchivos.NuevoNombre(Yo, "jpg");
        Assert.NotNull(ReglasArchivos.AnalizarUrl($"{Contenedor}/{nombre}", Contenedor, Yo));

        var otro = ReglasArchivos.NuevoNombre(Guid.NewGuid(), "jpg");
        Assert.Null(ReglasArchivos.AnalizarUrl($"{Contenedor}/{otro}", Contenedor, Yo));                                       // de otro usuario
        Assert.Null(ReglasArchivos.AnalizarUrl($"https://malicioso.example/imagenes/{nombre}", Contenedor, Yo));               // otro host
        Assert.Null(ReglasArchivos.AnalizarUrl($"https://cuenta.blob.core.windows.net/documentos/{nombre}", Contenedor, Yo));  // otro contenedor
        Assert.Null(ReglasArchivos.AnalizarUrl($"{Contenedor}/{nombre}?sv=2024&sig=x", Contenedor, Yo));                       // con SAS
        Assert.Null(ReglasArchivos.AnalizarUrl($"{Contenedor}/{Yo:N}/../{otro}", Contenedor, Yo));                             // rutas raras
        Assert.Null(ReglasArchivos.AnalizarUrl($"{Contenedor}/{Yo:N}/foto.exe", Contenedor, Yo));
    }

    [Fact]
    public void La_miniatura_se_deriva_del_nombre_y_no_sirve_como_foto_de_publicacion()
    {
        var nombre = ReglasArchivos.NuevoNombre(Yo, "png");
        var miniatura = ReglasArchivos.NombreMiniatura(nombre);
        Assert.Equal(nombre[..^4] + ".min.webp", miniatura);

        Assert.Null(ReglasArchivos.NombreMiniatura(miniatura!));                          // no hay miniatura de la miniatura
        Assert.Null(ReglasArchivos.NombreMiniatura(ReglasArchivos.NuevoNombre(Yo, "pdf"))); // los documentos no tienen
        Assert.Null(ReglasArchivos.AnalizarUrl($"{Contenedor}/{miniatura}", Contenedor, Yo)); // nadie la publica como foto
    }

    [Fact]
    public void La_CDN_es_opcional_pero_si_se_configura_debe_ser_https_y_tener_cuenta()
    {
        Assert.True(new AlmacenamientoOpciones().CdnValida);
        Assert.True(new AlmacenamientoOpciones { ServicioUrl = "https://cuenta.blob.core.windows.net", CdnUrl = "https://fotos.trueke.co" }.CdnValida);
        Assert.False(new AlmacenamientoOpciones { ServicioUrl = "https://cuenta.blob.core.windows.net", CdnUrl = "http://fotos.trueke.co" }.CdnValida);
        Assert.False(new AlmacenamientoOpciones { ServicioUrl = "https://cuenta.blob.core.windows.net", CdnUrl = "https://fotos.trueke.co/?x=1" }.CdnValida);
        Assert.False(new AlmacenamientoOpciones { CadenaConexion = "UseDevelopmentStorage=true", CdnUrl = "https://fotos.trueke.co" }.CdnValida);
    }

    /// <summary>Firma real del SDK de Azure con la llave pública del emulador (no hace llamadas de red).</summary>
    [Fact]
    public async Task La_SAS_es_de_un_solo_blob_solo_crear_y_escribir_y_dura_5_minutos()
    {
        var opciones = Options.Create(new AlmacenamientoOpciones { CadenaConexion = "UseDevelopmentStorage=true" });
        var almacen = new AlmacenBlobAzure(opciones, TimeProvider.System, NullLogger<AlmacenBlobAzure>.Instance);

        var s = await almacen.ConstruirSubidaAsync(Yo, TipoArchivoDto.Imagen, "image/png", "png", CancellationToken.None);
        var url = new Uri(s.UrlSubida);
        var q = HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("cw", q["sp"]);   // create + write: no puede leer, listar ni borrar
        Assert.Equal("b", q["sr"]);    // un solo blob
        var duracion = DateTimeOffset.Parse(q["se"]!) - DateTimeOffset.Parse(q["st"]!);
        Assert.InRange(duracion.TotalMinutes, 5, 6.1);
        Assert.Contains($"/imagenes/{Yo:N}/", s.UrlArchivo);
        Assert.StartsWith(s.UrlArchivo, s.UrlSubida);
        Assert.Equal("PUT", s.Metodo);
        Assert.Equal("BlockBlob", s.Cabeceras["x-ms-blob-type"]);
        Assert.NotNull(ReglasArchivos.AnalizarUrl(s.UrlArchivo, new Uri(s.UrlArchivo[..s.UrlArchivo.IndexOf($"/{Yo:N}/", StringComparison.Ordinal)]), Yo));
    }
}
