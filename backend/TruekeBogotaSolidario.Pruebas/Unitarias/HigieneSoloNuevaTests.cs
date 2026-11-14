using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Moderacion;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Ropa interior, medias y trajes de baño solo se publican como "Nuevo" (por higiene).</summary>
public class HigieneSoloNuevaTests
{
    [Theory]
    [InlineData("Ropa interior de algodón", "ropa interior")]
    [InlineData("CALZÓN talla M", "calzon")]
    [InlineData("Set de medias deportivas", "medias")]
    [InlineData("Traje de baño de dos piezas", "traje de bano")]
    [InlineData("Vestido de baño niña", "vestido de bano")]
    [InlineData("Brasier sin varillas", "brasier")]
    [InlineData("Bóxer para hombre", "boxer")]
    [InlineData("Calcetines de lana", "calcetines")]
    public void Detecta_los_articulos_de_higiene_sin_importar_tildes_ni_mayusculas(string titulo, string esperado)
        => Assert.Equal(esperado, ArticulosDeHigiene.Detectar(titulo));

    [Theory]
    [InlineData("Zapatos de cuero")]
    [InlineData("Calzado deportivo")]               // "calzado" no es "calzon"
    [InlineData("Chaqueta impermeable")]
    [InlineData("Media hora de clase de guitarra")] // "media" suelta no cuenta
    [InlineData("Mesa de centro")]
    [InlineData("Interiores de un carro")]
    public void No_confunde_otros_articulos(string titulo) => Assert.Null(ArticulosDeHigiene.Detectar(titulo));

    private static Task<PublicacionDto> PublicarAsync(EntornoNegocio e, Guid duenio, string titulo, CondicionDto condicion, string descripcion = "Talla M, color azul")
        => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(duenio, new CrearPublicacionRequest
        {
            Titulo = titulo, Descripcion = descripcion, CategoriaId = 1, Modo = ModoDto.Trueke, Condicion = condicion, Localidad = "Kennedy",
            EsMercanciaNueva = condicion == CondicionDto.Nuevo
        }));

    [Fact]
    public async Task No_se_publica_ropa_interior_medias_ni_trajes_de_bano_que_no_sean_nuevos()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaHigiene")).Usuario.Id;

        foreach (var condicion in new[] { CondicionDto.ComoNuevo, CondicionDto.Usado, CondicionDto.UsadoConDetalles })
        {
            var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => PublicarAsync(e, ana, "Medias de lana", condicion,
                condicion == CondicionDto.UsadoConDetalles ? "Con detalles" : "Buen estado"));
            Assert.Equal(ArticulosDeHigiene.Mensaje, ex.Message);
        }
        // Nuevas sí, y otros artículos usados también.
        Assert.Equal("Medias de lana", (await PublicarAsync(e, ana, "Medias de lana", CondicionDto.Nuevo)).Titulo);
        Assert.Equal("Chaqueta usada", (await PublicarAsync(e, ana, "Chaqueta usada", CondicionDto.Usado)).Titulo);
    }

    [Fact]
    public async Task Al_editar_tampoco_se_puede_pasar_a_usado()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaEditaHig")).Usuario.Id;
        var pub = await PublicarAsync(e, ana, "Traje de baño", CondicionDto.Nuevo);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.EditarAsync(ana, pub.Id,
            new CrearPublicacionRequest
            {
                Titulo = pub.Titulo, Descripcion = pub.Descripcion, CategoriaId = 1, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Kennedy"
            })));
    }

    [Fact]
    public async Task Que_aparezca_solo_en_la_descripcion_no_bloquea()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaDescHig")).Usuario.Id;
        Assert.Equal("Zapatos de cuero", (await PublicarAsync(e, ana, "Zapatos de cuero", CondicionDto.Usado, "No incluye medias ni plantillas")).Titulo);
    }

    // ---------- señal para la moderación (descripción) ----------

    private static readonly DateTime Ahora = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ContextoRiesgo Confiable = new(Ahora.AddMonths(-6), true, EstadoFotoPerfil.Aprobada, 10, 0, true);

    [Fact]
    public void En_la_descripcion_de_algo_usado_manda_a_revision_y_en_algo_nuevo_no()
    {
        var o = new ModeracionOpciones();
        var usado = EvaluadorRiesgo.Evaluar("Zapatos de cuero", "Se venden con ropa interior de regalo", null, Confiable, o, Ahora, CondicionProducto.Usado);
        Assert.True(usado.RequiereRevision);
        Assert.Contains(usado.Motivos, m => m.Contains("higiene"));

        Assert.False(EvaluadorRiesgo.Evaluar("Zapatos de cuero", "Se venden con ropa interior de regalo", null, Confiable, o, Ahora, CondicionProducto.Nuevo).RequiereRevision);
        // Sin estado conocido (llamadas anteriores) no suma la señal.
        Assert.False(EvaluadorRiesgo.Evaluar("Zapatos de cuero", "Se venden con ropa interior de regalo", null, Confiable, o, Ahora).RequiereRevision);
    }
}
