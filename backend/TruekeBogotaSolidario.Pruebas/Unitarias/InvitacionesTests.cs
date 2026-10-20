using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Invita y ganan los dos: +20 para cada uno cuando la persona invitada completa su primer intercambio válido.</summary>
public class InvitacionesTests
{
    private static readonly int PuntosTrueke = PoliticaEcoPuntos.PuntosPorModo(ModoTransaccion.Trueke);

    private static async Task CompletarTruekeAsync(EntornoNegocio e, Guid oferente, Guid receptor)
    {
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(oferente, new CrearPublicacionRequest
        { Titulo = "Libro usado", Descripcion = "Buen estado", CategoriaId = 2, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Kennedy" }));
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(receptor, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "Me sirve" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(oferente, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(oferente, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(receptor, sol.Id));
    }

    private static Task<InvitacionDto> InvitacionAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IEcoPuntosService, InvitacionDto>(s => s.InvitacionAsync(actor));

    private static async Task<int> SaldoAsync(EntornoNegocio e, Guid actor)
        => (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(actor))).Saldo;

    /// <summary>Ana invita a Beto (registrado con su código).</summary>
    private static async Task<(Guid Ana, Guid Beto)> AnaInvitaABetoAsync(EntornoNegocio e)
    {
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var codigo = (await InvitacionAsync(e, ana)).Codigo;
        var beto = (await e.RegistrarAsync("beto", codigo)).Usuario.Id;
        return (ana, beto);
    }

    [Fact]
    public async Task El_codigo_es_corto_legible_y_siempre_el_mismo()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var primero = await InvitacionAsync(e, ana);
        Assert.Equal(CodigosInvitacion.Largo, primero.Codigo.Length);
        Assert.DoesNotMatch("[01OIL]", primero.Codigo);
        Assert.Equal(primero.Codigo, (await InvitacionAsync(e, ana)).Codigo);
        Assert.Equal(0, primero.Logradas);
        Assert.Equal(PoliticaEcoPuntos.PuntosInvitacion, primero.PuntosPorInvitacion);
    }

    [Fact]
    public async Task Ganan_los_dos_solo_al_primer_intercambio_completado_no_al_registrarse()
    {
        using var e = new EntornoNegocio();
        var (ana, beto) = await AnaInvitaABetoAsync(e);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse, await SaldoAsync(e, ana));
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse, await SaldoAsync(e, beto)); // registrarse no da nada extra

        var carla = (await e.RegistrarAsync("carla")).Usuario.Id;
        await CompletarTruekeAsync(e, carla, beto);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PuntosTrueke + PoliticaEcoPuntos.PuntosInvitacion, await SaldoAsync(e, beto));
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PoliticaEcoPuntos.PuntosInvitacion, await SaldoAsync(e, ana));
        Assert.Equal(1, (await InvitacionAsync(e, ana)).Logradas);

        // el segundo intercambio ya no da recompensa
        await CompletarTruekeAsync(e, (await e.RegistrarAsync("dario")).Usuario.Id, beto);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PoliticaEcoPuntos.PuntosInvitacion, await SaldoAsync(e, ana));

        var historial = await e.EnScopeAsync<IEcoPuntosService, PaginaDto<MovimientoEcoPuntosDto>>(s => s.MovimientosAsync(ana, 1, 20));
        Assert.Single(historial.Items, m => m.Concepto == "Invitacion");
    }

    [Fact]
    public async Task Un_intercambio_con_quien_invito_no_cuenta_y_se_espera_al_siguiente()
    {
        using var e = new EntornoNegocio();
        var (ana, beto) = await AnaInvitaABetoAsync(e);
        await CompletarTruekeAsync(e, ana, beto);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PuntosTrueke + PoliticaEcoPuntos.PuntosPrimeraPublicacion, await SaldoAsync(e, ana)); // el trueke y su primera publicación: nada por la invitación
        Assert.Equal(0, (await InvitacionAsync(e, ana)).Logradas);

        await CompletarTruekeAsync(e, (await e.RegistrarAsync("carla")).Usuario.Id, beto);
        Assert.Equal(1, (await InvitacionAsync(e, ana)).Logradas);
    }

    [Fact]
    public async Task Quien_invita_gana_como_maximo_10_por_ciclo_y_la_persona_invitada_recibe_lo_suyo_igual()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var codigo = (await InvitacionAsync(e, ana)).Codigo;
        var otros = new List<Guid>();
        for (var i = 0; i <= PoliticaEcoPuntos.MaxInvitacionesRecompensadasPorCiclo; i++)
        {
            var invitado = (await e.RegistrarAsync($"inv{i}", codigo)).Usuario.Id;
            var otro = (await e.RegistrarAsync($"otro{i}")).Usuario.Id;
            await CompletarTruekeAsync(e, otro, invitado);
            otros.Add(invitado);
        }

        var tope = PoliticaEcoPuntos.MaxInvitacionesRecompensadasPorCiclo;
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + tope * PoliticaEcoPuntos.PuntosInvitacion, await SaldoAsync(e, ana));
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PuntosTrueke + PoliticaEcoPuntos.PuntosInvitacion, await SaldoAsync(e, otros[^1]));
        var resumen = await InvitacionAsync(e, ana);
        Assert.Equal(tope + 1, resumen.Logradas);

        // pasado el ciclo vuelve a ganar
        reloj.Avanzar(TimeSpan.FromDays(PoliticaEcoPuntos.DiasCicloInvitaciones + 1));
        var nuevo = (await e.RegistrarAsync("nuevo", codigo)).Usuario.Id;
        await CompletarTruekeAsync(e, (await e.RegistrarAsync("alguien")).Usuario.Id, nuevo);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + (tope + 1) * PoliticaEcoPuntos.PuntosInvitacion, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task Un_codigo_que_no_existe_no_impide_registrarse()
    {
        using var e = new EntornoNegocio();
        var beto = (await e.RegistrarAsync("beto", "NOEXISTE")).Usuario.Id;
        var carla = (await e.RegistrarAsync("carla")).Usuario.Id;
        await CompletarTruekeAsync(e, carla, beto);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PuntosTrueke, await SaldoAsync(e, beto));
    }

    [Fact]
    public void Nadie_se_invita_a_si_mismo()
    {
        var ana = new Usuario("Ana Prueba", "Kennedy", "ana@t.co", "Clave12345");
        ana.RegistrarInvitador(ana);
        Assert.Null(ana.InvitadoPorId);
        Assert.False(ana.EsperaRecompensaInvitacion);
    }

    [Fact]
    public async Task Al_eliminar_la_cuenta_su_codigo_deja_de_funcionar()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var codigo = (await InvitacionAsync(e, ana)).Codigo;
        await e.ModificarUsuarioAsync(ana, u => u.Anonimizar(DateTime.UtcNow));
        var beto = (await e.RegistrarAsync("beto", codigo)).Usuario.Id;
        Guid? invitadoPor = Guid.Empty;
        await e.ModificarUsuarioAsync(beto, u => invitadoPor = u.InvitadoPorId);
        Assert.Null(invitadoPor);
    }
}
