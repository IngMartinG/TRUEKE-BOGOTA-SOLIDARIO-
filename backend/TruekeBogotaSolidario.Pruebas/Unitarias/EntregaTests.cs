using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Aceptar → ambos confirman → Completada (puntos) ; cierres automáticos ; calificaciones.</summary>
public class EntregaTests
{
    private static async Task<(Guid Duenio, Guid Solicitante, Guid Solicitud, Guid Publicacion)> AcordarAsync(EntornoNegocio e)
    {
        var duenio = (await e.RegistrarAsync("duenia")).Usuario.Id;
        var solicitante = (await e.RegistrarAsync("solicitante")).Usuario.Id;
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(duenio, new CrearPublicacionRequest
        { Titulo = "Licuadora", Descripcion = "Funciona", CategoriaId = 3, Modo = ModoDto.Donacion, Condicion = CondicionDto.Usado, Localidad = "Usaquén" }));
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(solicitante, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "La necesito" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(duenio, sol.Id));
        return (duenio, solicitante, sol.Id, pub.Id);
    }

    private static Task<int> SaldoAsync(EntornoNegocio e, Guid id) => e.EnScopeAsync<IEcoPuntosService, int>(async s => (await s.ResumenAsync(id)).Saldo);

    private static Task<SolicitudDto> SolicitudAsync(EntornoNegocio e, Guid actor, Guid id)
        => e.EnScopeAsync<ISolicitudService, SolicitudDto>(async s => (await s.ListarEnviadasAsync(actor)).Concat(await s.ListarRecibidasAsync(actor)).Single(x => x.Id == id));

    [Fact]
    public async Task Aceptar_no_otorga_puntos_hasta_que_ambos_confirman()
    {
        using var e = new EntornoNegocio();
        var (duenio, solicitante, sol, _) = await AcordarAsync(e);
        var inicial = await SaldoAsync(e, solicitante);

        var tras1 = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol));
        Assert.Equal("Aceptada", tras1.Estado);
        Assert.True(tras1.ConfirmadaPorDuenio);
        Assert.NotNull(tras1.CierreAutomaticoUtc);
        Assert.Equal(inicial, await SaldoAsync(e, solicitante));

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol)));                // no dos veces

        var final = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(solicitante, sol));
        Assert.Equal("Completada", final.Estado);
        Assert.True(final.PuedoCalificar);
        Assert.Equal(inicial + PoliticaEcoPuntos.PuntosPorModo(Datos.Entidades.ModoTransaccion.Donacion), await SaldoAsync(e, solicitante));
    }

    [Fact]
    public async Task Un_tercero_no_puede_confirmar_ni_ver_la_solicitud()
    {
        using var e = new EntornoNegocio();
        var (_, _, sol, _) = await AcordarAsync(e);
        var tercero = (await e.RegistrarAsync("tercero")).Usuario.Id;
        await Assert.ThrowsAsync<NoEncontradoException>(() => e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(tercero, sol)));
    }

    [Fact]
    public async Task No_concretada_libera_la_publicacion_y_no_da_puntos()
    {
        using var e = new EntornoNegocio();
        var (duenio, solicitante, sol, pub) = await AcordarAsync(e);
        var r = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.MarcarNoConcretadaAsync(solicitante, sol, "No llegó a la cita"));
        Assert.Equal("NoConcretada", r.Estado);
        var p = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(null, pub));
        Assert.Equal("Disponible", p.Estado);

        // quien ya confirmó no puede "deshacer"; la otra parte sí puede indicar que no se concretó
        var (d2, s2, sol2, _) = await AcordarAsync(e);
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(d2, sol2));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.MarcarNoConcretadaAsync(d2, sol2, "Me arrepentí")));
        Assert.Equal("NoConcretada", (await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.MarcarNoConcretadaAsync(s2, sol2, "Nunca llegó"))).Estado);
    }

    [Fact]
    public async Task Con_una_confirmacion_se_cierra_sola_a_los_7_dias_sin_puntos_ni_impacto()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var (duenio, solicitante, sol, _) = await AcordarAsync(e);
        var saldoDuenio = await SaldoAsync(e, duenio);
        var saldoSolicitante = await SaldoAsync(e, solicitante);
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol));

        reloj.Avanzar(TimeSpan.FromDays(6));
        Assert.Empty(await e.EnScopeAsync<ISolicitudService, IReadOnlyList<Guid>>(s => s.ListarParaCierreAutomaticoAsync(100)));

        reloj.Avanzar(TimeSpan.FromDays(2));
        var ids = await e.EnScopeAsync<ISolicitudService, IReadOnlyList<Guid>>(s => s.ListarParaCierreAutomaticoAsync(100));
        Assert.Contains(sol, ids);
        await e.EnScopeAsync<ISolicitudService>(s => s.CerrarAutomaticamenteAsync(sol));
        Assert.Equal("Completada", (await SolicitudAsync(e, solicitante, sol)).Estado);
        // Solo cuenta lo que confirman las dos partes: ni Eco-Puntos ni impacto para nadie.
        Assert.Equal(saldoDuenio, await SaldoAsync(e, duenio));
        Assert.Equal(saldoSolicitante, await SaldoAsync(e, solicitante));
        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, duenio));
        Assert.Equal(0, perfil.Impacto!.Objetos);
        var avisos = await e.EnScopeAsync<INotificacionService, PaginaDto<NotificacionDto>>(s => s.ListarAsync(duenio, false, 1, 50));
        Assert.Contains(avisos.Items, n => n.Tipo == TiposNotificacion.IntercambioCerradoSinConfirmar);
    }

    private static async Task<IReadOnlyList<NotificacionDto>> RecordatoriosAsync(EntornoNegocio e, Guid id)
        => (await e.EnScopeAsync<INotificacionService, PaginaDto<NotificacionDto>>(s => s.ListarAsync(id, false, 1, 50)))
            .Items.Where(n => n.Tipo == TiposNotificacion.EntregaPendiente).ToList();

    private static async Task RecordarTodosAsync(EntornoNegocio e)
    {
        foreach (var id in await e.EnScopeAsync<ISolicitudService, IReadOnlyList<Guid>>(s => s.ListarParaRecordatorioAsync(100)))
            await e.EnScopeAsync<ISolicitudService>(s => s.RecordarEntregaAsync(id));
    }

    [Fact]
    public async Task Recuerda_la_entrega_a_los_3_dias_y_un_dia_antes_del_cierre_solo_a_quien_no_confirmo()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var (duenio, solicitante, sol, _) = await AcordarAsync(e);

        reloj.Avanzar(TimeSpan.FromDays(2));
        await RecordarTodosAsync(e);
        Assert.Empty(await RecordatoriosAsync(e, solicitante)); // aún no

        reloj.Avanzar(TimeSpan.FromDays(1.5));
        await RecordarTodosAsync(e);
        await RecordarTodosAsync(e); // la tarea corre cada hora: el mismo aviso no se repite
        Assert.Single(await RecordatoriosAsync(e, duenio));
        var primero = Assert.Single(await RecordatoriosAsync(e, solicitante));
        Assert.Equal(sol, primero.RecursoId);
        Assert.Contains("¿Ya se hizo la entrega", primero.Mensaje);

        // El dueño confirma: el cierre pasa a los 7 días y solo a quien falta le llega el aviso de la víspera
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol));
        reloj.Avanzar(TimeSpan.FromDays(3)); // día 6.5
        await RecordarTodosAsync(e);
        Assert.Single(await RecordatoriosAsync(e, duenio));
        var avisos = await RecordatoriosAsync(e, solicitante);
        Assert.Equal(2, avisos.Count);
        Assert.Contains(avisos, n => n.Mensaje.StartsWith("Mañana se cierra el intercambio") && n.Mensaje.Contains("sumen Eco-Puntos"));
        Assert.Empty(await e.EnScopeAsync<ISolicitudService, IReadOnlyList<Guid>>(s => s.ListarParaRecordatorioAsync(100)));
    }

    [Fact]
    public async Task Sin_confirmaciones_avisa_a_ambos_la_vispera_del_cierre_y_nada_tras_completar()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var (duenio, solicitante, sol, _) = await AcordarAsync(e);
        reloj.Avanzar(TimeSpan.FromDays(29.5));
        await RecordarTodosAsync(e);
        // A la víspera sale solo el aviso de cierre (reemplaza al de entrega que no alcanzó a salir)
        foreach (var id in new[] { duenio, solicitante })
            Assert.StartsWith("Mañana se cierra como no concretado", Assert.Single(await RecordatoriosAsync(e, id)).Mensaje);

        // Un intercambio completado no recibe recordatorios
        var (d2, s2, sol2, _) = await AcordarAsync(e);
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(d2, sol2));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(s2, sol2));
        reloj.Avanzar(TimeSpan.FromDays(4));
        Assert.DoesNotContain(sol2, await e.EnScopeAsync<ISolicitudService, IReadOnlyList<Guid>>(s => s.ListarParaRecordatorioAsync(100)));
        Assert.Empty(await RecordatoriosAsync(e, s2));
    }

    [Fact]
    public async Task Sin_confirmaciones_queda_no_concretada_a_los_30_dias()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var (duenio, _, sol, pub) = await AcordarAsync(e);
        reloj.Avanzar(TimeSpan.FromDays(31));
        await e.EnScopeAsync<ISolicitudService>(s => s.CerrarAutomaticamenteAsync(sol));
        Assert.Equal("NoConcretada", (await SolicitudAsync(e, duenio, sol)).Estado);
        Assert.Equal("Disponible", (await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(null, pub))).Estado);
    }

    [Fact]
    public async Task Calificar_solo_tras_completar_una_vez_y_actualiza_el_promedio()
    {
        using var e = new EntornoNegocio();
        var (duenio, solicitante, sol, _) = await AcordarAsync(e);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(solicitante, sol, new CalificarRequest { Estrellas = 5 })));

        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(solicitante, sol));

        var c = await e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(solicitante, sol, new CalificarRequest { Estrellas = 4, Comentario = "Muy amable" }));
        Assert.Equal(4, c.Estrellas);
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(() =>
            e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(solicitante, sol, new CalificarRequest { Estrellas = 1 })));
        await e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(duenio, sol, new CalificarRequest { Estrellas = 2 }));

        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, duenio));
        Assert.Equal(4.0m, perfil.CalificacionPromedio);
        Assert.Equal(1, perfil.TotalCalificaciones);
        var lista = await e.EnScopeAsync<ISolicitudService, PaginaDto<CalificacionDto>>(s => s.ListarCalificacionesAsync(duenio, 1, 20));
        Assert.Equal("Muy amable", Assert.Single(lista.Items).Comentario);
        Assert.False((await SolicitudAsync(e, solicitante, sol)).PuedoCalificar);
    }

    [Fact]
    public async Task El_plazo_para_calificar_vence_a_los_30_dias()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var (duenio, solicitante, sol, _) = await AcordarAsync(e);
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(solicitante, sol));
        reloj.Avanzar(TimeSpan.FromDays(31));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(solicitante, sol, new CalificarRequest { Estrellas = 5 })));
    }
}
