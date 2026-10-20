using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Niveles Semilla → Guardián: solo cuenta lo ganado participando; recargar no sube y gastar no baja.</summary>
public class NivelesEcoPuntosTests
{
    private static async Task CompletarTruekeAsync(EntornoNegocio e, Guid oferente, Guid receptor)
    {
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(oferente, new CrearPublicacionRequest
        { Titulo = "Libro usado", Descripcion = "Buen estado", CategoriaId = 2, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Kennedy" }));
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(receptor, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "Me sirve" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(oferente, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(oferente, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(receptor, sol.Id));
    }

    private static Task<EcoPuntosResumenDto> ResumenAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(actor));

    [Theory]
    [InlineData(0, NivelEcoPuntos.Semilla)]
    [InlineData(199, NivelEcoPuntos.Semilla)]
    [InlineData(200, NivelEcoPuntos.Brote)]
    [InlineData(499, NivelEcoPuntos.Brote)]
    [InlineData(500, NivelEcoPuntos.Arbol)]
    [InlineData(999, NivelEcoPuntos.Arbol)]
    [InlineData(1_000, NivelEcoPuntos.Bosque)]
    [InlineData(2_499, NivelEcoPuntos.Bosque)]
    [InlineData(2_500, NivelEcoPuntos.Guardian)]
    [InlineData(1_000_000, NivelEcoPuntos.Guardian)]
    public void El_nivel_cambia_justo_en_cada_borde(int puntos, NivelEcoPuntos esperado)
        => Assert.Equal(esperado, PoliticaEcoPuntos.NivelPara(puntos));

    [Fact]
    public void Solo_lo_ganado_participando_cuenta_para_el_nivel()
    {
        var cuentan = Enum.GetValues<ConceptoEcoPuntos>().Where(PoliticaEcoPuntos.CuentaParaNivel).ToHashSet();
        Assert.Equal(new HashSet<ConceptoEcoPuntos>
            { ConceptoEcoPuntos.SaldoInicial, ConceptoEcoPuntos.Bienvenida, ConceptoEcoPuntos.Intercambio, ConceptoEcoPuntos.Invitacion }, cuentan);
    }

    [Fact]
    public async Task Recargar_sube_el_saldo_pero_no_el_nivel()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, new IniciarPagoRequest
        { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = 50_000 });

        var r = await ResumenAsync(e, ana);
        Assert.True(r.Saldo > PoliticaEcoPuntos.Niveles[1].PuntosMinimos); // 500 + bono, alcanzaría Brote si contara
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, r.Nivel!.PuntosParticipacion);
        Assert.Equal("Semilla", r.Nivel.Nombre);
    }

    [Fact]
    public async Task Gastar_puntos_no_baja_de_nivel()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(300, ConceptoEcoPuntos.Intercambio));
        await e.ModificarUsuarioAsync(ana, u => u.DebitarEcoPuntos(u.SaldoEcoPuntos, ConceptoEcoPuntos.Impulsar));

        var r = await ResumenAsync(e, ana);
        Assert.Equal(0, r.Saldo);
        Assert.Equal("Brote", r.Nivel!.Nombre);
        Assert.Equal(310, r.Nivel.PuntosParticipacion);
    }

    [Fact]
    public async Task El_resumen_propio_trae_el_progreso_hacia_el_siguiente_nivel()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(340, ConceptoEcoPuntos.Intercambio)); // 350 en total

        var nivel = (await ResumenAsync(e, ana)).Nivel!;
        Assert.Equal(("Brote", 200, "Arbol", 500), (nivel.Nombre, nivel.PuntosMinimosNivel, nivel.SiguienteNivel, nivel.PuntosSiguienteNivel));
        Assert.Equal(50, nivel.Progreso);

        await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(5_000, ConceptoEcoPuntos.Intercambio));
        nivel = (await ResumenAsync(e, ana)).Nivel!;
        Assert.Equal("Guardian", nivel.Nombre);
        Assert.Null(nivel.SiguienteNivel);
        Assert.Null(nivel.PuntosSiguienteNivel);
        Assert.Equal(100, nivel.Progreso);
    }

    [Fact]
    public async Task Al_subir_de_nivel_con_un_intercambio_llega_un_aviso()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(185, ConceptoEcoPuntos.Intercambio)); // 195: le faltan 5 para Brote

        await CompletarTruekeAsync(e, ana, beto);

        var avisosAna = await e.EnScopeAsync<INotificacionService, PaginaDto<NotificacionDto>>(s => s.ListarAsync(ana, false, 1, 50));
        Assert.Single(avisosAna.Items, n => n.Tipo == TiposNotificacion.NivelAlcanzado && n.Mensaje.Contains("Brote"));
        var avisosBeto = await e.EnScopeAsync<INotificacionService, PaginaDto<NotificacionDto>>(s => s.ListarAsync(beto, false, 1, 50));
        Assert.DoesNotContain(avisosBeto.Items, n => n.Tipo == TiposNotificacion.NivelAlcanzado); // Beto sigue en Semilla
    }

    [Fact]
    public async Task Los_demas_solo_ven_el_nombre_del_nivel()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(600, ConceptoEcoPuntos.Intercambio));

        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(beto, ana));
        Assert.Equal("Arbol", perfil.Nivel);
        var anonimo = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, ana));
        Assert.Equal("Arbol", anonimo.Nivel);

    }

    [Fact]
    public void Una_cuenta_suspendida_se_muestra_en_Semilla_sin_perder_su_nivel_real()
    {
        var u = new Usuario("Ana Pérez", "Kennedy", "ana@t.co", "Clave12345");
        u.AcreditarEcoPuntos(600, ConceptoEcoPuntos.Intercambio);
        Assert.Equal("Arbol", Mapeos.APerfilPublico(u, DateTime.UtcNow).Nivel);

        u.Suspender("Pruebas", null, DateTime.UtcNow);
        Assert.Equal("Semilla", Mapeos.APerfilPublico(u, DateTime.UtcNow).Nivel);
        Assert.Equal(NivelEcoPuntos.Arbol, u.Nivel);
    }

    [Fact]
    public void Al_eliminar_la_cuenta_el_nivel_vuelve_a_cero()
    {
        var u = new Usuario("Ana Pérez", "Kennedy", "ana@t.co", "Clave12345");
        u.AcreditarEcoPuntos(3_000, ConceptoEcoPuntos.Intercambio);
        Assert.Equal(NivelEcoPuntos.Guardian, u.Nivel);
        u.Anonimizar(DateTime.UtcNow);
        Assert.Equal(0, u.PuntosParticipacion);
        Assert.Equal(NivelEcoPuntos.Semilla, u.Nivel);
    }
}
