using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>
/// Mercancía nueva: un producto nuevo que se ofrece para la venta (de tienda o para revender) no es segunda mano. Solo lo
/// "Nuevo" puede serlo, un negocio que publica algo nuevo siempre vende mercancía y el catálogo filtra por origen.
/// </summary>
public class MercanciaNuevaTests
{
    private static Task<PublicacionDto> PublicarAsync(EntornoNegocio e, Guid duenio, CondicionDto condicion, bool mercancia, string titulo = "Camiseta de algodón")
        => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(duenio, new CrearPublicacionRequest
        {
            Titulo = titulo, Descripcion = "Talla M, color azul", CategoriaId = 1, Modo = ModoDto.Trueke, Condicion = condicion,
            Localidad = "Kennedy", EsMercanciaNueva = mercancia
        }));

    [Fact]
    public async Task Solo_lo_nuevo_puede_ser_mercancia_y_una_persona_elige()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaMercancia")).Usuario.Id;

        Assert.True((await PublicarAsync(e, ana, CondicionDto.Nuevo, mercancia: true)).EsMercanciaNueva);
        // Un regalo sin usar que ya tenía: es nuevo, pero no es mercancía.
        Assert.False((await PublicarAsync(e, ana, CondicionDto.Nuevo, mercancia: false)).EsMercanciaNueva);
        // Lo usado nunca es mercancía nueva, aunque lo marquen.
        Assert.False((await PublicarAsync(e, ana, CondicionDto.Usado, mercancia: true)).EsMercanciaNueva);
        Assert.False((await PublicarAsync(e, ana, CondicionDto.ComoNuevo, mercancia: true)).EsMercanciaNueva);
    }

    [Fact]
    public async Task Al_editar_y_dejar_de_ser_nuevo_deja_de_ser_mercancia()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaEdita")).Usuario.Id;
        var pub = await PublicarAsync(e, ana, CondicionDto.Nuevo, mercancia: true);

        var editada = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.EditarAsync(ana, pub.Id, new CrearPublicacionRequest
        {
            Titulo = pub.Titulo, Descripcion = pub.Descripcion, CategoriaId = 1, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado,
            Localidad = "Kennedy", EsMercanciaNueva = true
        }));

        Assert.False(editada.EsMercanciaNueva);
    }

    [Fact]
    public async Task Un_negocio_que_publica_algo_nuevo_siempre_vende_mercancia()
    {
        using var e = new EntornoNegocio();
        var tienda = (await e.RegistrarAsync("tiendaMercancia")).Usuario.Id;
        await e.PagarAsync(tienda, new IniciarPagoRequest { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = ConceptoPagoDto.Empresa });

        // No depende de lo que marque.
        Assert.True((await PublicarAsync(e, tienda, CondicionDto.Nuevo, mercancia: false)).EsMercanciaNueva);
        // Un negocio también puede ofrecer segunda mano.
        Assert.False((await PublicarAsync(e, tienda, CondicionDto.Usado, mercancia: false)).EsMercanciaNueva);
    }

    [Fact]
    public async Task El_catalogo_filtra_por_origen_y_lo_nuevo_sin_usar_cuenta_como_segunda_mano()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaFiltro")).Usuario.Id;
        var mercancia = await PublicarAsync(e, ana, CondicionDto.Nuevo, mercancia: true, "Medias nuevas de tienda");
        var sinUsar = await PublicarAsync(e, ana, CondicionDto.Nuevo, mercancia: false, "Regalo que nunca usé");
        var usada = await PublicarAsync(e, ana, CondicionDto.Usado, mercancia: false, "Chaqueta usada");

        async Task<List<Guid>> ListarAsync(OrigenDto? origen)
            => (await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s => s.ListarAsync(null, new FiltroPublicacionesRequest { Origen = origen })))
                .Items.Select(p => p.Id).ToList();

        Assert.Equal([mercancia.Id], await ListarAsync(OrigenDto.MercanciaNueva));
        var segundaMano = await ListarAsync(OrigenDto.SegundaMano);
        Assert.Contains(sinUsar.Id, segundaMano);
        Assert.Contains(usada.Id, segundaMano);
        Assert.DoesNotContain(mercancia.Id, segundaMano);
        Assert.Equal(3, (await ListarAsync(null)).Count);
    }
}
