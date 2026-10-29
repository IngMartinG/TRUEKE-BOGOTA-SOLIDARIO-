using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Moderacion;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class EvaluadorRiesgoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ModeracionOpciones Opciones = new();

    /// <summary>Cuenta antigua, verificada, con foto aprobada y varias publicaciones: no tiene señales propias.</summary>
    private static ContextoRiesgo Confiable(bool esNueva = true) => new(Ahora.AddMonths(-6), IdentidadVerificada: true,
        EstadoFotoPerfil.Aprobada, PublicacionesAprobadas: 10, DenunciasConfirmadas: 0, esNueva);

    private static EvaluacionRiesgo Evaluar(string descripcion, ContextoRiesgo? c = null, string titulo = "Silla de madera")
        => EvaluadorRiesgo.Evaluar(titulo, descripcion, null, c ?? Confiable(), Opciones, Ahora);

    [Fact]
    public void Una_cuenta_confiable_con_texto_normal_se_publica_al_instante()
    {
        var r = Evaluar("Silla en buen estado, se arma fácil. Precio $1.500.000 negociable. Acepto Nequi.");
        Assert.False(r.RequiereRevision);
        Assert.Equal(0, r.Puntaje);
        Assert.Empty(r.Motivos);
    }

    [Fact]
    public void Las_primeras_publicaciones_de_una_cuenta_siempre_se_revisan_y_al_editar_no_cuenta()
    {
        var nueva = Confiable() with { PublicacionesAprobadas = 2 };
        var r = Evaluar("Silla en buen estado", nueva);
        Assert.True(r.RequiereRevision);
        Assert.Contains(r.Motivos, m => m.Contains("primeras 3"));

        Assert.False(Evaluar("Silla en buen estado", nueva with { EsNueva = false }).RequiereRevision);
        Assert.False(Evaluar("Silla en buen estado", nueva with { PublicacionesAprobadas = 3 }).RequiereRevision);
    }

    [Theory]
    [InlineData("Escríbeme al 310 555 1234")]
    [InlineData("Llama al +57 3105551234")]
    [InlineData("Fijo 601 555 12 34")]
    [InlineData("Correo vendedor@ejemplo.com")]
    [InlineData("Mira más fotos en https://ejemplo.com/fotos")]
    [InlineData("Catálogo en wa.me/573105551234")]
    public void Los_datos_de_contacto_o_enlaces_mandan_a_revision(string descripcion)
    {
        var r = Evaluar(descripcion);
        Assert.True(r.RequiereRevision);
        Assert.Contains("Datos de contacto o enlaces en el texto", r.Motivos);
    }

    [Theory]
    [InlineData("Pistola de silicona para manualidades")]
    [InlineData("Control remoto con pilas AAA")]
    [InlineData("Mueble que se arma en 10 minutos")]
    [InlineData("Precio 350.000, casi nuevo")]
    public void Los_usos_comunes_no_se_confunden_con_riesgo(string descripcion)
        => Assert.False(Evaluar(descripcion).RequiereRevision);

    [Theory]
    [InlineData("Vendo cachorros de 2 meses")]
    [InlineData("Medicamento sin abrir")]
    [InlineData("Réplica de reloj")]
    [InlineData("Arma de fuego antigua")]
    public void Un_posible_articulo_prohibido_se_revisa(string titulo)
    {
        var r = Evaluar("En buen estado", titulo: titulo);
        Assert.True(r.RequiereRevision);
        Assert.Contains(r.Motivos, m => m.StartsWith("Posible artículo prohibido"));
    }

    [Fact]
    public void La_lista_de_articulos_se_amplia_por_configuracion()
    {
        var o = new ModeracionOpciones { ArticulosRestringidos = ["Pólvora negra", "boletas"] };
        var r = EvaluadorRiesgo.Evaluar("Boletas para el concierto", "Dos entradas", null, Confiable(), o, Ahora);
        Assert.True(r.RequiereRevision);
        Assert.Contains("Posible artículo prohibido (boletas)", r.Motivos);
    }

    [Fact]
    public void Una_sola_senal_leve_no_alcanza_pero_varias_juntas_si()
    {
        // Cuenta nueva (30) + sin verificar (10) = 40: se publica.
        var nueva = new ContextoRiesgo(Ahora.AddDays(-2), false, EstadoFotoPerfil.Aprobada, 5, 0, true);
        Assert.False(Evaluar("Silla en buen estado", nueva).RequiereRevision);
        // …y si además invita a WhatsApp (30) = 70: se revisa.
        var r = Evaluar("Silla en buen estado, hablemos por WhatsApp", nueva);
        Assert.True(r.RequiereRevision);
        Assert.Equal(EvaluadorRiesgo.PesoCuentaNueva + EvaluadorRiesgo.PesoSinIdentidad + EvaluadorRiesgo.PesoFueraDePlataforma, r.Puntaje);
    }

    [Fact]
    public void Las_denuncias_confirmadas_y_la_foto_en_revision_suman()
    {
        var c = Confiable() with { DenunciasConfirmadas = 2, EstadoFoto = EstadoFotoPerfil.EnRevision };
        var r = Evaluar("Silla en buen estado", c);
        Assert.True(r.RequiereRevision);
        Assert.Contains("Ya le ocultaron contenido por 2 denuncias", r.Motivos);
        Assert.Contains("Su foto de perfil está en revisión", r.Motivos);
    }

    [Fact]
    public void Sin_preaprobacion_nada_espera_aunque_tenga_senales()
    {
        var o = new ModeracionOpciones { PreAprobacion = false };
        var r = EvaluadorRiesgo.Evaluar("Cachorros", "310 555 1234", null, Confiable(), o, Ahora);
        Assert.False(r.RequiereRevision);
        Assert.True(r.Puntaje >= o.UmbralRiesgo); // las señales se calculan igual
    }
}

public class ModeracionPublicacionTests
{
    private static Publicacion Nueva() => new(Guid.NewGuid(), new DatosPublicacion("Silla", "De madera", new Categoria(4, "Hogar", "Muebles"),
        ModoTransaccion.Trueke, CondicionProducto.Usado, null, "11001", "Suba", null, null, null, null));

    [Fact]
    public void Pendiente_no_es_visible_y_al_aprobarla_entra_como_recien_publicada()
    {
        var p = Nueva();
        var ahora = p.FechaRelevancia.AddHours(3);
        p.EnviarARevision(100, ["Una de sus primeras 3 publicaciones"], ahora);
        Assert.False(p.EsVisible);
        Assert.Equal(EstadoModeracion.Pendiente, p.EstadoModeracion);
        Assert.Throws<ReglaDeNegocioException>(() => p.ValidarPuedeImpulsarse(ahora));
        Assert.Throws<ReglaDeNegocioException>(() => p.ValidarPuedeDestacarse(ahora));

        p.AprobarModeracion(ahora);
        Assert.True(p.EsVisible);
        Assert.Equal(ahora, p.FechaRelevancia);
        Assert.Null(p.EnRevisionDesdeUtc);
    }

    [Fact]
    public void Una_edicion_mientras_espera_conserva_su_lugar_y_suma_las_senales()
    {
        var p = Nueva();
        var t0 = DateTime.UtcNow;
        p.EnviarARevision(100, ["Una de sus primeras 3 publicaciones"], t0);
        p.EnviarARevision(60, ["Datos de contacto o enlaces en el texto"], t0.AddHours(1));
        Assert.Equal(t0, p.EnRevisionDesdeUtc);
        Assert.Equal(100, p.PuntajeRiesgo);
        Assert.Equal("Una de sus primeras 3 publicaciones; Datos de contacto o enlaces en el texto", p.MotivosRiesgo);
    }

    [Fact]
    public void El_rechazo_exige_motivo_y_solo_aplica_a_las_que_esperan()
    {
        var p = Nueva();
        Assert.Throws<ReglaDeNegocioException>(() => p.RechazarModeracion("No cumple")); // está aprobada
        p.EnviarARevision(100, ["x"], DateTime.UtcNow);
        Assert.Throws<ReglaDeNegocioException>(() => p.RechazarModeracion(" "));
        p.RechazarModeracion("Parece un animal vivo.");
        Assert.Equal(EstadoModeracion.Rechazada, p.EstadoModeracion);
        Assert.Equal("Parece un animal vivo.", p.MotivoRechazo);
        Assert.False(p.EsVisible);
    }
}
