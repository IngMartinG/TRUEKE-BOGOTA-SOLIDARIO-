using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Lo que prometen los planes y el destacado se cumple: orden, cupo de destacados, sello de Empresa y filtros.</summary>
public class PlanesCumplenTests
{
    private static CrearPublicacionRequest Pub(string titulo, decimal? precio = null, double? lat = null, double? lon = null)
        => new()
        {
            Titulo = titulo, Descripcion = "Buen estado", CategoriaId = 1, Modo = precio is null ? ModoDto.Trueke : ModoDto.Compra,
            Condicion = CondicionDto.Usado, Localidad = "Centro", PrecioReferenciaCop = precio, Latitud = lat, Longitud = lon
        };

    private static Task<PublicacionDto> CrearAsync(EntornoNegocio e, Guid actor, CrearPublicacionRequest r)
        => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(actor, r));

    private static Task DestacarAsync(EntornoNegocio e, Guid actor, Guid publicacionId)
        => e.PagarAsync(actor, new IniciarPagoRequest
        {
            FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = ConceptoPagoDto.Destacar, PublicacionId = publicacionId
        });

    private static Task PlanAsync(EntornoNegocio e, Guid actor, ConceptoPagoDto plan)
        => e.PagarAsync(actor, new IniciarPagoRequest { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = plan });

    private static Task<EcoPuntosResumenDto> ResumenAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(actor));

    [Theory]
    [InlineData(OrdenPublicacionesDto.PrecioAsc)]
    [InlineData(OrdenPublicacionesDto.PrecioDesc)]
    [InlineData(OrdenPublicacionesDto.Recientes)]
    public async Task La_destacada_aparece_primero_con_cualquier_orden(OrdenPublicacionesDto orden)
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await CrearAsync(e, ana, Pub("Barata", 10_000));
        var destacada = await CrearAsync(e, ana, Pub("Mediana destacada", 50_000));
        await CrearAsync(e, ana, Pub("Cara", 90_000));
        await DestacarAsync(e, ana, destacada.Id);

        var pagina = await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s =>
            s.ListarAsync(null, new FiltroPublicacionesRequest { Orden = orden }));
        Assert.Equal(destacada.Id, pagina.Items[0].Id);
        if (orden == OrdenPublicacionesDto.PrecioAsc) Assert.Equal(["Barata", "Cara"], pagina.Items.Skip(1).Select(p => p.Titulo));
        if (orden == OrdenPublicacionesDto.PrecioDesc) Assert.Equal(["Cara", "Barata"], pagina.Items.Skip(1).Select(p => p.Titulo));
    }

    [Fact]
    public async Task En_cerca_de_mi_la_destacada_va_primero_dentro_del_radio_y_se_puede_filtrar_por_verificados()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var cerquita = await CrearAsync(e, beto, Pub("Al lado", lat: 4.6100, lon: -74.0800));
        var destacada = await CrearAsync(e, ana, Pub("Destacada a 3 km", lat: 4.6370, lon: -74.0800));
        await CrearAsync(e, ana, Pub("Lejos", lat: 4.9000, lon: -74.0800)); // fuera del radio de 5 km
        await DestacarAsync(e, ana, destacada.Id);

        var cerca = new CercanasRequest { Lat = 4.6097, Lon = -74.0817, RadioKm = 5 };
        var lista = await e.EnScopeAsync<IPublicacionService, IReadOnlyList<PublicacionCercanaDto>>(s => s.ListarCercanasAsync(null, cerca));
        Assert.Equal([destacada.Id, cerquita.Id], lista.Select(x => x.Publicacion.Id));

        await e.VerificarIdentidadAsync(beto);
        var verificados = await e.EnScopeAsync<IPublicacionService, IReadOnlyList<PublicacionCercanaDto>>(s =>
            s.ListarCercanasAsync(null, new CercanasRequest { Lat = 4.6097, Lon = -74.0817, RadioKm = 5, SoloVerificados = true }));
        Assert.Equal(cerquita.Id, Assert.Single(verificados).Publicacion.Id);
    }

    [Fact]
    public async Task En_el_perfil_publico_las_destacadas_van_primero()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var vieja = await CrearAsync(e, ana, Pub("Vieja destacada"));
        await CrearAsync(e, ana, Pub("Nueva"));
        await DestacarAsync(e, ana, vieja.Id);

        var perfil = await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s => s.ListarDePerfilAsync(null, ana, 1, 20));
        Assert.Equal(vieja.Id, perfil.Items[0].Id);
    }

    [Fact]
    public async Task Los_destacados_gratis_se_reponen_cada_30_dias_y_renovar_antes_no_los_pierde()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var pub = await CrearAsync(e, ana, Pub("Bicicleta"));
        await PlanAsync(e, ana, ConceptoPagoDto.Premium);

        await e.EnScopeAsync<IPagoService>(s => s.DestacarGratisAsync(ana, pub.Id));
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisPremium - 1, (await ResumenAsync(e, ana)).DestacadosGratisRestantes);

        // Renovar antes de que venza solo suma días: el cupo que quedaba sigue igual (antes se reiniciaba y se perdía)
        reloj.Avanzar(TimeSpan.FromDays(20));
        await PlanAsync(e, ana, ConceptoPagoDto.Premium);
        var renovado = await ResumenAsync(e, ana);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisPremium - 1, renovado.DestacadosGratisRestantes);
        Assert.NotNull(renovado.ProximaRecargaDestacadosUtc);

        // A los 30 días del inicio se repone el cupo completo (no se acumula con lo que sobró)
        reloj.Avanzar(TimeSpan.FromDays(11));
        var repuesto = await ResumenAsync(e, ana);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisPremium, repuesto.DestacadosGratisRestantes);
        Assert.True(repuesto.ProximaRecargaDestacadosUtc > reloj.GetUtcNow().UtcDateTime);

        // Al usarlo, la reposición queda aplicada y se descuenta de los 3
        reloj.Avanzar(TimeSpan.FromDays(8)); // el destacado anterior (7 días) ya venció
        await e.EnScopeAsync<IPagoService>(s => s.DestacarGratisAsync(ana, pub.Id));
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisPremium - 1, (await ResumenAsync(e, ana)).DestacadosGratisRestantes);
    }

    [Fact]
    public async Task La_empresa_muestra_nombre_comercial_y_sello_y_el_NIT_solo_si_lo_elige()
    {
        using var e = new EntornoNegocio();
        var tienda = (await e.RegistrarAsync("tienda")).Usuario.Id;
        await PlanAsync(e, tienda, ConceptoPagoDto.Empresa);
        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilEmpresaAsync(tienda,
            new PerfilEmpresaRequest { NombreComercial = "Muebles La 80", Nit = "900123456" }));
        var pub = await CrearAsync(e, tienda, Pub("Silla"));

        var enCatalogo = (await e.EnScopeAsync<IPublicacionService, PaginaDto<PublicacionDto>>(s =>
            s.ListarAsync(null, new FiltroPublicacionesRequest()))).Items.Single(p => p.Id == pub.Id);
        Assert.Equal("Muebles La 80", enCatalogo.Propietario.NombreComercial);
        Assert.True(enCatalogo.Propietario.NitRegistrado);

        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, tienda));
        Assert.True(perfil.NitRegistrado);
        Assert.Null(perfil.Nit); // por defecto el número no se publica

        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilEmpresaAsync(tienda,
            new PerfilEmpresaRequest { NombreComercial = "Muebles La 80", Nit = "900123456", MostrarNit = true }));
        perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, tienda));
        Assert.Equal("900123456-8", perfil.Nit);
    }

    [Fact]
    public async Task Sin_plan_Empresa_no_hay_sello_ni_NIT()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await PlanAsync(e, ana, ConceptoPagoDto.Premium);
        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, ana));
        Assert.False(perfil.NitRegistrado);
        Assert.Null(perfil.Nit);
        Assert.Null(perfil.NombreComercial);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IAuthService, UsuarioDto>(s =>
            s.ActualizarPerfilEmpresaAsync(ana, new PerfilEmpresaRequest { NombreComercial = "Tienda", Nit = "900123456" })));
    }
}
