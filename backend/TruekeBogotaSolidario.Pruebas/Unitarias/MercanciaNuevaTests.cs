using TruekeBogotaSolidario.Datos.Common;
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

    // ---------- PR 2: impacto 2.1.0, Eco-Puntos y logros ----------

    private static async Task<(Guid Vendedor, Guid Comprador)> VenderAsync(EntornoNegocio e, CondicionDto condicion, bool mercancia, string sufijo)
    {
        var vendedor = (await e.RegistrarAsync("vende" + sufijo)).Usuario.Id;
        var comprador = (await e.RegistrarAsync("compra" + sufijo)).Usuario.Id;
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(vendedor, new CrearPublicacionRequest
        {
            Titulo = "Camiseta " + sufijo, Descripcion = "Talla M, color azul", CategoriaId = 1, Modo = ModoDto.Compra, PrecioReferenciaCop = 30_000,
            Condicion = condicion, Localidad = "Kennedy", EsMercanciaNueva = mercancia
        }));
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(comprador, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "La quiero" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(vendedor, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(vendedor, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(comprador, sol.Id));
        return (vendedor, comprador);
    }

    private static Task<PerfilUsuarioDto> PerfilAsync(EntornoNegocio e, Guid id)
        => e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, id));

    [Fact]
    public async Task Vender_mercancia_nueva_da_un_punto_y_no_suma_impacto_ni_el_logro_de_segunda_mano()
    {
        using var e = new EntornoNegocio();
        var (vendedor, comprador) = await VenderAsync(e, CondicionDto.Nuevo, mercancia: true, "merc");

        var perfil = await PerfilAsync(e, vendedor);
        Assert.Equal(0, perfil.Impacto!.Objetos);
        Assert.Equal(0m, perfil.Impacto.KgReutilizados);
        // El logro "Segunda mano" no avanza con una venta de mercancía (el contador de compras del perfil sí sube).
        Assert.Equal(0, (await ProgresoSegundaManoAsync(e, vendedor)));
        Assert.Equal(1, perfil.ComprasRealizadas);
        Assert.Equal(PoliticaEcoPuntos.PuntosVentaMercanciaNueva, await PuntosDeIntercambioAsync(e, vendedor));
        Assert.Equal(PoliticaEcoPuntos.PuntosVentaMercanciaNueva, await PuntosDeIntercambioAsync(e, comprador));
    }

    [Fact]
    public async Task Vender_algo_de_segunda_mano_sigue_dando_los_puntos_de_una_compra_y_suma_impacto()
    {
        using var e = new EntornoNegocio();
        var (vendedor, comprador) = await VenderAsync(e, CondicionDto.Usado, mercancia: false, "usada");

        Assert.Equal(PoliticaEcoPuntos.PuntosPorModo(Datos.Entidades.ModoTransaccion.Compra), await PuntosDeIntercambioAsync(e, vendedor));
        Assert.Equal(PoliticaEcoPuntos.PuntosPorModo(Datos.Entidades.ModoTransaccion.Compra), await PuntosDeIntercambioAsync(e, comprador));
        var perfil = await PerfilAsync(e, vendedor);
        Assert.Equal(1, perfil.Impacto!.Objetos);
        Assert.Equal(1, (await ProgresoSegundaManoAsync(e, vendedor)));
    }

    [Fact]
    public async Task Lo_nuevo_sin_usar_que_alguien_ya_tenia_se_reutiliza_y_cuenta_como_segunda_mano()
    {
        using var e = new EntornoNegocio();
        var (vendedor, _) = await VenderAsync(e, CondicionDto.Nuevo, mercancia: false, "sinusar");

        Assert.Equal(1, (await PerfilAsync(e, vendedor)).Impacto!.Objetos);
    }

    private static async Task<int> ProgresoSegundaManoAsync(EntornoNegocio e, Guid id)
        => (await e.EnScopeAsync<IPublicacionService, IReadOnlyList<LogroDto>>(s => s.MisLogrosAsync(id))).Single(l => l.Clave == "segunda-mano").Progreso;

    private static async Task<int> PuntosDeIntercambioAsync(EntornoNegocio e, Guid id)
        => (await e.EnScopeAsync<IEcoPuntosService, PaginaDto<MovimientoEcoPuntosDto>>(s => s.MovimientosAsync(id, 1, 50)))
            .Items.Single(m => m.Concepto == "Intercambio").Puntos;
}
