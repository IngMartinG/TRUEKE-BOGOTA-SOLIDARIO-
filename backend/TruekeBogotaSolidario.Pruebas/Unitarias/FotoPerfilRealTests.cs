using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Archivos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>La regla que decide con los rostros encontrados (sin el modelo).</summary>
public class ReglaFotoPerfilTests
{
    [Fact]
    public void Sin_rostros_se_rechaza() => Assert.Null(ReglaFotoPerfil.Clasificar([]));

    [Fact]
    public void Rostros_con_poca_confianza_no_cuentan() => Assert.Null(ReglaFotoPerfil.Clasificar([new(0.5f, 0.3f)]));

    [Fact]
    public void Una_cara_clara_y_grande_se_aprueba()
        => Assert.Equal(EstadoFotoPerfil.Aprobada, ReglaFotoPerfil.Clasificar([new(0.97f, 0.2f)]));

    [Fact]
    public void Una_cara_pequena_o_dudosa_va_a_revision()
    {
        Assert.Equal(EstadoFotoPerfil.EnRevision, ReglaFotoPerfil.Clasificar([new(0.97f, 0.01f)])); // persona lejana en un paisaje
        Assert.Equal(EstadoFotoPerfil.EnRevision, ReglaFotoPerfil.Clasificar([new(0.8f, 0.2f)]));
    }

    [Fact]
    public void Una_forma_parecida_a_una_cara_pero_sin_rasgos_en_orden_va_a_revision()
        => Assert.Equal(EstadoFotoPerfil.EnRevision, ReglaFotoPerfil.Clasificar([new(0.97f, 0.2f, RasgosCoherentes: false)]));

    [Fact]
    public void Los_rasgos_de_una_cara_deben_estar_en_orden()
    {
        // Cara de 100 × 120: ojos arriba, nariz al centro, boca abajo.
        (float, float)[] cara = [(30, 40), (70, 40), (50, 65), (35, 90), (65, 90)];
        Assert.True(DetectorRostrosOnnx.RasgosCoherentes(cara, 0, 0, 100, 120));
        (float, float)[] bocaArriba = [(30, 90), (70, 90), (50, 65), (35, 40), (65, 40)];
        Assert.False(DetectorRostrosOnnx.RasgosCoherentes(bocaArriba, 0, 0, 100, 120));
        (float, float)[] ojosJuntos = [(48, 40), (52, 40), (50, 65), (35, 90), (65, 90)];
        Assert.False(DetectorRostrosOnnx.RasgosCoherentes(ojosJuntos, 0, 0, 100, 120));
        (float, float)[] fuera = [(30, 40), (70, 40), (50, 65), (35, 90), (65, 300)];
        Assert.False(DetectorRostrosOnnx.RasgosCoherentes(fuera, 0, 0, 100, 120));
    }

    [Fact]
    public void Varias_caras_del_mismo_tamano_van_a_revision_pero_gente_pequena_al_fondo_no()
    {
        Assert.Equal(EstadoFotoPerfil.EnRevision, ReglaFotoPerfil.Clasificar([new(0.97f, 0.15f), new(0.95f, 0.12f)]));
        Assert.Equal(EstadoFotoPerfil.Aprobada, ReglaFotoPerfil.Clasificar([new(0.97f, 0.2f), new(0.9f, 0.01f)]));
    }
}

/// <summary>El modelo real (UltraFace) con un retrato y con el logo de la plataforma.</summary>
public sealed class DetectorRostrosTests : IDisposable
{
    private readonly DetectorRostrosOnnx _detector = new(Options.Create(new FotosPerfilOpciones()), NullLogger<DetectorRostrosOnnx>.Instance);

    private static byte[] Recurso(string nombre) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Recursos", nombre));

    [Fact]
    public void Un_retrato_tiene_una_cara_clara()
    {
        var rostros = _detector.Detectar(Recurso("rostro-dominio-publico.jpg"));
        Assert.NotNull(rostros);
        Assert.Equal(EstadoFotoPerfil.Aprobada, ReglaFotoPerfil.Clasificar(rostros));
    }

    [Fact]
    public void Un_logo_no_tiene_rostro()
    {
        var rostros = _detector.Detectar(Recurso("sin-rostro-logo.png"));
        Assert.NotNull(rostros);
        Assert.Null(ReglaFotoPerfil.Clasificar(rostros));
    }

    [Fact]
    public void Lo_que_no_es_imagen_no_se_puede_revisar() => Assert.Null(_detector.Detectar("no es una imagen"u8.ToArray()));

    [Fact]
    public void Apagado_no_revisa_nada()
    {
        using var apagado = new DetectorRostrosOnnx(Options.Create(new FotosPerfilOpciones { DetectorHabilitado = false }),
            NullLogger<DetectorRostrosOnnx>.Instance);
        Assert.Null(apagado.Detectar(Recurso("rostro-dominio-publico.jpg")));
    }

    public void Dispose() => _detector.Dispose();
}

/// <summary>Estados de la foto en la entidad: la rechazada vale solo hasta su plazo.</summary>
public class EstadoFotoUsuarioTests
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static Usuario ConFoto(EstadoFotoPerfil estado = EstadoFotoPerfil.Aprobada)
    {
        var u = new Usuario("Ana Pérez", "Chapinero", $"ana-{Guid.NewGuid():N}@trueke.test", "Clave12345");
        u.CambiarFoto("https://almacen.test/imagenes/ana.jpg", estado, Ahora);
        return u;
    }

    [Fact]
    public void La_foto_rechazada_vale_hasta_el_plazo()
    {
        var u = ConFoto();
        u.RechazarFoto("No se ve tu cara.", Ahora, Ahora.AddDays(7));
        Assert.True(u.TieneFotoValida(Ahora.AddDays(6)));
        Assert.False(u.TieneFotoValida(Ahora.AddDays(7)));
    }

    [Fact]
    public void Rechazar_otra_vez_no_amplia_el_plazo()
    {
        var u = ConFoto();
        u.RechazarFoto("No se ve tu cara.", Ahora, Ahora.AddDays(7));
        u.RechazarFoto("Sigue sin verse.", Ahora.AddDays(5), Ahora.AddDays(12));
        Assert.Equal(Ahora.AddDays(7), u.PlazoCambioFotoUtc);
    }

    [Fact]
    public void Cambiar_la_foto_limpia_el_rechazo_y_quitarla_la_deja_sin_revisar()
    {
        var u = ConFoto();
        u.RechazarFoto("No se ve tu cara.", Ahora, Ahora.AddDays(7));
        u.CambiarFoto("https://almacen.test/imagenes/ana-2.jpg", EstadoFotoPerfil.Aprobada, Ahora);
        Assert.Equal(EstadoFotoPerfil.Aprobada, u.EstadoFoto);
        Assert.Null(u.PlazoCambioFotoUtc);
        Assert.Null(u.MotivoRechazoFoto);
        Assert.True(u.TieneFotoValida(Ahora.AddYears(1)));

        u.QuitarFoto();
        Assert.Equal(EstadoFotoPerfil.SinRevisar, u.EstadoFoto);
        Assert.False(u.TieneFotoValida(Ahora));
    }

    [Fact]
    public void Una_foto_no_se_guarda_ya_rechazada_ni_se_rechaza_sin_motivo()
    {
        var u = new Usuario("Ana Pérez", "Chapinero", $"ana-{Guid.NewGuid():N}@trueke.test", "Clave12345");
        Assert.Throws<ReglaDeNegocioException>(() => u.CambiarFoto("https://almacen.test/x.jpg", EstadoFotoPerfil.Rechazada, Ahora));
        var conFoto = ConFoto();
        Assert.Throws<ReglaDeNegocioException>(() => conFoto.RechazarFoto(" ", Ahora, Ahora.AddDays(7)));
    }

    [Fact]
    public void La_revision_automatica_solo_toca_las_fotos_sin_revisar()
    {
        var u = ConFoto(EstadoFotoPerfil.SinRevisar);
        u.RegistrarRevisionFoto(EstadoFotoPerfil.EnRevision, Ahora);
        Assert.Equal(EstadoFotoPerfil.EnRevision, u.EstadoFoto);
        u.RegistrarRevisionFoto(EstadoFotoPerfil.Aprobada, Ahora); // ya no estaba sin revisar: no cambia
        Assert.Equal(EstadoFotoPerfil.EnRevision, u.EstadoFoto);
    }
}
