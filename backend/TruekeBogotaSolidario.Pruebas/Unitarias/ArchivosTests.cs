using System.Text;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class ArchivosTests
{
    private static readonly Uri Contenedor = new("https://proyecto.supabase.co/storage/v1/object/public/imagenes");
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
        Assert.Null(ReglasArchivos.AnalizarUrl($"https://proyecto.supabase.co/storage/v1/object/authenticated/documentos/{nombre}", Contenedor, Yo)); // otro bucket
        Assert.Null(ReglasArchivos.AnalizarUrl($"{Contenedor}/{nombre}?token=x", Contenedor, Yo));                                   // con firma
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
    public void La_CDN_es_opcional_pero_si_se_configura_debe_ser_https_y_tener_Storage()
    {
        const string storage = "https://proyecto.supabase.co/storage/v1";
        Assert.True(new AlmacenamientoOpciones().CdnValida);
        Assert.True(new AlmacenamientoOpciones { Url = storage, CdnUrl = "https://fotos.trueke.co" }.CdnValida);
        Assert.False(new AlmacenamientoOpciones { Url = storage, CdnUrl = "http://fotos.trueke.co" }.CdnValida);
        Assert.False(new AlmacenamientoOpciones { Url = storage, CdnUrl = "https://fotos.trueke.co/?x=1" }.CdnValida);
        Assert.False(new AlmacenamientoOpciones { CdnUrl = "https://fotos.trueke.co" }.CdnValida);
        Assert.Equal($"{storage}/object/public/", new AlmacenamientoOpciones { Url = storage + "/" }.OrigenPublico);
    }

    [Theory]
    [InlineData("https://proyecto.supabase.co/storage/v1", true)]
    [InlineData("http://127.0.0.1:8082/storage/v1", true)]   // Storage local en Docker
    [InlineData("http://proyecto.supabase.co/storage/v1", false)] // la llave secreta nunca viaja sin TLS
    [InlineData("https://proyecto.supabase.co/storage/v1?x=1", false)]
    [InlineData("https://usuario:clave@proyecto.supabase.co/storage/v1", false)]
    public void La_URL_de_Storage_es_https_o_local(string url, bool valida)
        => Assert.Equal(valida, new AlmacenamientoOpciones { Url = url, LlaveServicio = "sb_secret_x" }.UrlValida);

    [Fact]
    public void El_vencimiento_de_la_URL_firmada_se_lee_del_token()
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = $"{B64("{\"alg\":\"HS256\"}")}.{B64("{\"url\":\"subidas/a/b.png\",\"exp\":1792300000}")}.firma";

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792300000).UtcDateTime, AlmacenSupabase.Vencimiento(token));
        Assert.Null(AlmacenSupabase.Vencimiento(null));
        Assert.Null(AlmacenSupabase.Vencimiento("no-es-un-token"));
        Assert.Null(AlmacenSupabase.Vencimiento("a.%%%.c"));
    }

    [Theory]
    [InlineData("sb_secret_abc123", false)] // llave nueva: solo en la cabecera apikey
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.firma", true)] // service_role antigua (JWT)
    public void Solo_las_llaves_JWT_van_como_Bearer(string llave, bool jwt) => Assert.Equal(jwt, AlmacenSupabase.EsJwt(llave));

    [Fact]
    public void Solo_se_reconocen_documentos_con_nombre_del_servidor()
    {
        var documentos = new Uri("https://proyecto.supabase.co/storage/v1/object/authenticated/documentos");
        var nombre = ReglasArchivos.NuevoNombre(Guid.NewGuid(), "pdf");
        Assert.Equal(nombre, ReglasArchivos.NombreArchivo($"{documentos}/{nombre}", documentos));
        Assert.Null(ReglasArchivos.NombreArchivo($"{documentos}/{nombre}?token=x", documentos));
        Assert.Null(ReglasArchivos.NombreArchivo($"{documentos}/../imagenes/{nombre}", documentos));
        Assert.Null(ReglasArchivos.NombreFotoPublicada($"{Contenedor}/{nombre}", Contenedor)); // un PDF nunca es una foto
    }
}
