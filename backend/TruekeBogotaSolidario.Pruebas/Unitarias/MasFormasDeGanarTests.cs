using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>
/// Más formas de ganar Eco-Puntos (primera publicación, perfil completo, calificaciones y denuncias procedentes) y su antiabuso:
/// una sola vez, tope diario o mensual, una vez por pareja y doble clic.
/// </summary>
public class MasFormasDeGanarTests
{
    // ------------------------------------------------------------------ Ayudas
    private static Task<PublicacionDto> PublicarAsync(EntornoNegocio e, Guid duenio, string titulo = "Libro usado")
        => e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(duenio, new CrearPublicacionRequest
        { Titulo = titulo, Descripcion = "Buen estado", CategoriaId = 2, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Kennedy" }));

    private static async Task<SolicitudDto> IntercambiarAsync(EntornoNegocio e, Guid pubId, Guid duenio, Guid receptor)
    {
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(receptor, new CrearSolicitudRequest { PublicacionId = pubId, Mensaje = "Me sirve" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(duenio, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(duenio, sol.Id));
        return await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(receptor, sol.Id));
    }

    private static async Task<SolicitudDto> CompletarTruekeAsync(EntornoNegocio e, Guid duenio, Guid receptor)
        => await IntercambiarAsync(e, (await PublicarAsync(e, duenio)).Id, duenio, receptor);

    private static Task<CalificacionDto> CalificarAsync(EntornoNegocio e, Guid autor, Guid solicitudId, int estrellas)
        => e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(autor, solicitudId, new CalificarRequest { Estrellas = estrellas }));

    private static async Task<int> SaldoAsync(EntornoNegocio e, Guid id)
        => (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(id))).Saldo;

    private static async Task<IReadOnlyList<MovimientoEcoPuntosDto>> MovimientosAsync(EntornoNegocio e, Guid id, string concepto)
        => (await e.EnScopeAsync<IEcoPuntosService, PaginaDto<MovimientoEcoPuntosDto>>(s => s.MovimientosAsync(id, 1, 50)))
            .Items.Where(m => m.Concepto == concepto).ToList();

    private static async Task<IReadOnlyList<NotificacionDto>> AvisosAsync(EntornoNegocio e, Guid id)
        => (await e.EnScopeAsync<INotificacionService, PaginaDto<NotificacionDto>>(s => s.ListarAsync(id, false, 1, 50))).Items;

    /// <summary>Lo que hace el mantenimiento periódico: entrega los bonos pendientes de cada persona en su propio scope.</summary>
    private static async Task CorrerBonosAsync(EntornoNegocio e)
    {
        var ids = await e.EnScopeAsync<IBonosEcoPuntosService, IReadOnlyList<Guid>>(s => s.ListarPendientesAsync(200));
        foreach (var id in ids) await e.EnScopeAsync<IBonosEcoPuntosService>(s => s.OtorgarPendientesAsync(id));
    }

    private static async Task<Guid> ModeradorAsync(EntornoNegocio e, string nombre = "moderadora")
    {
        var id = (await e.RegistrarAsync(nombre)).Usuario.Id;
        await e.HacerModeradorAsync(id);
        return id;
    }

    private static Task<DenunciaCreadaDto> DenunciarAsync(EntornoNegocio e, Guid denunciante, Guid publicacionId)
        => e.EnScopeAsync<IDenunciaService, DenunciaCreadaDto>(s => s.CrearAsync(denunciante, new CrearDenunciaRequest
        { Tipo = TipoDenunciaDto.Publicacion, ObjetivoId = publicacionId, Motivo = MotivoDenunciaDto.Spam }));

    private static Task ResolverAsync(EntornoNegocio e, Guid moderador, Guid denunciaId, AccionDenunciaDto accion = AccionDenunciaDto.MarcarRevisada)
        => e.EnScopeAsync<IDenunciaService>(s => s.ResolverAsync(moderador, denunciaId, accion, "Revisado"));

    // ------------------------------------------------------------------ Política
    [Fact]
    public void La_politica_publica_lee_las_nuevas_formas_de_ganar_en_vivo()
    {
        using var e = new EntornoNegocio();
        using var scope = e.Proveedor.CreateScope();
        var p = scope.ServiceProvider.GetRequiredService<IEcoPuntosService>().ObtenerPolitica();
        Assert.Equal((20, 24, 30), (p.PuntosPrimeraPublicacion, p.HorasPrimeraPublicacionActiva, p.PuntosPerfilCompleto));
        Assert.Equal((15, 8, 30), (p.PuntosCalificacionCincoEstrellas, p.PuntosCalificacionCuatroEstrellas, p.DiasEntreCalificacionesMismaPareja));
        Assert.Equal((20, 3, 30), (p.PuntosDenunciaConfirmada, p.MaxDenunciasRecompensadasPorCiclo, p.DiasCicloDenuncias));
    }

    [Theory]
    [InlineData(5, 15)]
    [InlineData(4, 8)]
    [InlineData(3, 0)]
    [InlineData(1, 0)]
    public void Solo_las_calificaciones_de_4_y_5_estrellas_dan_puntos(int estrellas, int puntos)
        => Assert.Equal(puntos, PoliticaEcoPuntos.PuntosPorCalificacion(estrellas));

    // ------------------------------------------------------------------ Perfil completo
    [Fact]
    public void El_perfil_completo_da_puntos_una_sola_vez_al_completar_el_ultimo_requisito()
    {
        var u = new Usuario("Ana Pérez", "Kennedy", "ana@t.co", "Clave12345");
        u.CambiarFoto("https://almacen.test/imagenes/ana/perfil.jpg");
        Assert.False(u.BonoPerfilCompletoOtorgado); // falta verificar el correo
        Assert.Equal(0, u.SaldoEcoPuntos);

        u.MarcarCorreoVerificado(DateTime.UtcNow); // último requisito
        Assert.True(u.BonoPerfilCompletoOtorgado);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida + PoliticaEcoPuntos.PuntosPerfilCompleto, u.SaldoEcoPuntos);
        Assert.Equal(u.SaldoEcoPuntos, u.PuntosParticipacion); // cuenta para el nivel

        // quitar y volver a poner la foto (o editar el perfil) no lo repite
        u.QuitarFoto();
        u.CambiarFoto("https://almacen.test/imagenes/ana/otra.jpg");
        u.ActualizarPerfil("Ana Pérez", "Suba");
        Assert.False(u.EvaluarPerfilCompleto());
        Assert.Single(u.MovimientosEcoPuntos, m => m.Concepto == ConceptoEcoPuntos.PerfilCompleto);
    }

    [Fact]
    public void Sin_foto_no_hay_bono_de_perfil_completo()
    {
        var u = new Usuario("Ana Pérez", "Kennedy", "ana@t.co", "Clave12345");
        u.MarcarCorreoVerificado(DateTime.UtcNow);
        u.ActualizarPerfil("Ana Pérez", "Suba");
        Assert.False(u.BonoPerfilCompletoOtorgado);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, u.SaldoEcoPuntos);
    }

    [Fact]
    public async Task Con_Google_el_bono_llega_al_escribir_la_localidad_y_se_avisa()
    {
        using var e = new EntornoNegocio();
        Guid id;
        using (var scope = e.Proveedor.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            var u = Usuario.CrearDesdeGoogle("Ana Pérez", $"ana{Guid.NewGuid():N}@gmail.com", "sub-" + Guid.NewGuid(), DateTime.UtcNow);
            u.CambiarFoto("https://almacen.test/imagenes/ana/perfil.jpg"); // foto importada de Google
            Assert.False(u.BonoPerfilCompletoOtorgado); // localidad "Sin especificar"
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            id = u.Id;
        }

        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilAsync(id, new ActualizarPerfilRequest { NombreCompleto = "Ana Pérez", Localidad = "Suba" }));
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida + PoliticaEcoPuntos.PuntosPerfilCompleto, await SaldoAsync(e, id));
        Assert.Single(await AvisosAsync(e, id), n => n.Tipo == TiposNotificacion.EcoPuntosGanados && n.Mensaje.Contains("perfil"));

        // guardar el perfil otra vez (doble clic) no da más
        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilAsync(id, new ActualizarPerfilRequest { NombreCompleto = "Ana Pérez", Localidad = "Suba" }));
        Assert.Single(await MovimientosAsync(e, id, "PerfilCompleto"));
    }

    [Fact]
    public async Task El_mantenimiento_entrega_el_bono_a_perfiles_completos_de_antes_pero_no_a_suspendidos()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        // Simula cuentas completas de antes del bono: se retira la marca y los puntos directamente en la base.
        foreach (var id in new[] { ana, beto })
            await e.ModificarUsuarioAsync(id, u =>
            {
                typeof(Usuario).GetProperty(nameof(Usuario.BonoPerfilCompletoOtorgado))!.SetValue(u, false);
                u.DebitarEcoPuntos(PoliticaEcoPuntos.PuntosPerfilCompleto, ConceptoEcoPuntos.Impulsar);
            });
        await e.ModificarUsuarioAsync(beto, u => u.Suspender("Pruebas", null, DateTime.UtcNow));

        await CorrerBonosAsync(e);
        await CorrerBonosAsync(e); // idempotente

        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse, await SaldoAsync(e, ana));
        Assert.Equal(2, (await MovimientosAsync(e, ana, "PerfilCompleto")).Count); // el original (retirado en la simulación) y el nuevo
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, await SaldoAsync(e, beto));
    }

    // ------------------------------------------------------------------ Primera publicación
    [Fact]
    public async Task La_primera_publicacion_da_puntos_al_cumplir_24_horas_activa_una_sola_vez()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var pub = await PublicarAsync(e, ana);

        reloj.Avanzar(TimeSpan.FromHours(23));
        await CorrerBonosAsync(e);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse, await SaldoAsync(e, ana)); // aún no cumple 24 h

        reloj.Avanzar(TimeSpan.FromHours(2));
        await CorrerBonosAsync(e);
        await CorrerBonosAsync(e); // otra pasada (o dos instancias): no repite
        var mov = Assert.Single(await MovimientosAsync(e, ana, "PrimeraPublicacion"));
        Assert.Equal((PoliticaEcoPuntos.PuntosPrimeraPublicacion, pub.Id), (mov.Puntos, mov.ReferenciaId));
        Assert.Single(await AvisosAsync(e, ana), n => n.Tipo == TiposNotificacion.EcoPuntosGanados && n.RecursoId == pub.Id);

        // una segunda publicación que también cumple 24 h no da más
        await PublicarAsync(e, ana, "Lámpara de mesa");
        reloj.Avanzar(TimeSpan.FromHours(25));
        await CorrerBonosAsync(e);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + PoliticaEcoPuntos.PuntosPrimeraPublicacion, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task Publicar_y_borrar_no_da_puntos()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var pub = await PublicarAsync(e, ana);
        await e.EnScopeAsync<IPublicacionService>(s => s.CancelarAsync(ana, pub.Id, "Ya no la tengo"));

        reloj.Avanzar(TimeSpan.FromHours(25));
        await CorrerBonosAsync(e);
        Assert.Empty(await MovimientosAsync(e, ana, "PrimeraPublicacion"));
    }

    [Fact]
    public async Task Una_publicacion_oculta_por_moderacion_no_da_puntos()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var mod = await ModeradorAsync(e);
        var pub = await PublicarAsync(e, ana);
        var denuncia = await DenunciarAsync(e, beto, pub.Id);
        await ResolverAsync(e, mod, denuncia.Id, AccionDenunciaDto.OcultarContenido);

        reloj.Avanzar(TimeSpan.FromHours(25));
        await CorrerBonosAsync(e);
        Assert.Empty(await MovimientosAsync(e, ana, "PrimeraPublicacion"));
    }

    [Fact]
    public async Task Si_se_intercambia_antes_de_24_horas_el_bono_llega_con_el_intercambio_y_no_se_repite()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var pub = await PublicarAsync(e, ana);
        await PublicarAsync(e, ana, "Lámpara de mesa"); // otra activa que cumplirá 24 h
        await IntercambiarAsync(e, pub.Id, ana, beto);

        var puntosTrueke = PoliticaEcoPuntos.PuntosPorModo(ModoTransaccion.Trueke);
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + puntosTrueke + PoliticaEcoPuntos.PuntosPrimeraPublicacion, await SaldoAsync(e, ana));
        Assert.Equal(EntornoNegocio.SaldoAlRegistrarse + puntosTrueke, await SaldoAsync(e, beto)); // beto no publicó nada

        reloj.Avanzar(TimeSpan.FromHours(25));
        await CorrerBonosAsync(e);
        Assert.Single(await MovimientosAsync(e, ana, "PrimeraPublicacion"));
    }

    [Fact]
    public async Task Un_intercambio_sin_puntos_por_anti_farmeo_no_da_el_bono_de_primera_publicacion()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        await CompletarTruekeAsync(e, beto, ana);  // primer intercambio de la pareja: con puntos (ana recibe)
        await CompletarTruekeAsync(e, ana, beto);  // misma pareja en la ventana: sin puntos

        Assert.Empty(await MovimientosAsync(e, ana, "PrimeraPublicacion"));
    }

    // ------------------------------------------------------------------ Calificaciones
    [Fact]
    public async Task Cinco_estrellas_dan_15_puntos_y_cuatro_dan_8_con_aviso()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var sol = await CompletarTruekeAsync(e, ana, beto);
        var antesAna = await SaldoAsync(e, ana);
        var antesBeto = await SaldoAsync(e, beto);

        await CalificarAsync(e, beto, sol.Id, 5);
        await CalificarAsync(e, ana, sol.Id, 4);

        Assert.Equal(antesAna + PoliticaEcoPuntos.PuntosCalificacionCincoEstrellas, await SaldoAsync(e, ana));
        Assert.Equal(antesBeto + PoliticaEcoPuntos.PuntosCalificacionCuatroEstrellas, await SaldoAsync(e, beto));
        Assert.Equal(sol.Id, Assert.Single(await MovimientosAsync(e, ana, "CalificacionRecibida")).ReferenciaId);
        Assert.Contains(await AvisosAsync(e, ana), n => n.Tipo == TiposNotificacion.CalificacionRecibida
            && n.Mensaje.Contains($"{PoliticaEcoPuntos.PuntosCalificacionCincoEstrellas} Eco-Puntos"));
        // (los saldos de arriba también prueban que calificar no da puntos a quien califica)
    }

    [Fact]
    public async Task Tres_estrellas_o_menos_no_dan_puntos()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var sol = await CompletarTruekeAsync(e, ana, beto);
        var antes = await SaldoAsync(e, ana);
        await CalificarAsync(e, beto, sol.Id, 3);
        Assert.Equal(antes, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task Calificar_dos_veces_el_mismo_intercambio_no_da_puntos_dos_veces()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var sol = await CompletarTruekeAsync(e, ana, beto);
        var antes = await SaldoAsync(e, ana);

        await CalificarAsync(e, beto, sol.Id, 5);
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(() => CalificarAsync(e, beto, sol.Id, 5)); // doble clic

        Assert.Equal(antes + PoliticaEcoPuntos.PuntosCalificacionCincoEstrellas, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task La_misma_pareja_solo_da_puntos_por_una_calificacion_en_la_ventana()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var primero = await CompletarTruekeAsync(e, ana, beto);
        var segundo = await CompletarTruekeAsync(e, ana, beto);

        await CalificarAsync(e, beto, primero.Id, 5);
        await CalificarAsync(e, beto, segundo.Id, 5);

        Assert.Single(await MovimientosAsync(e, ana, "CalificacionRecibida"));
    }

    [Fact]
    public async Task Las_calificaciones_con_puntos_tienen_tope_diario()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var tope = PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia;
        var intercambios = new List<(Guid Receptor, Guid Solicitud)>();
        for (var i = 0; i <= tope; i++)
        {
            var vecino = (await e.RegistrarAsync($"vecino{i}")).Usuario.Id;
            intercambios.Add((vecino, (await CompletarTruekeAsync(e, ana, vecino)).Id));
        }

        foreach (var (receptor, solicitud) in intercambios) await CalificarAsync(e, receptor, solicitud, 5);
        Assert.Equal(tope, (await MovimientosAsync(e, ana, "CalificacionRecibida")).Count); // la 6ª del día no suma

        // al día siguiente vuelve a sumar
        reloj.Avanzar(TimeSpan.FromHours(25));
        var otro = (await e.RegistrarAsync("otro")).Usuario.Id;
        await CalificarAsync(e, otro, (await CompletarTruekeAsync(e, ana, otro)).Id, 5);
        Assert.Equal(tope + 1, (await MovimientosAsync(e, ana, "CalificacionRecibida")).Count);
    }

    [Fact]
    public async Task Calificar_a_quien_suba_de_nivel_le_llega_el_aviso()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var sol = await CompletarTruekeAsync(e, ana, beto);
        await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(195 - u.PuntosParticipacion, ConceptoEcoPuntos.Intercambio)); // a 5 de Brote

        await CalificarAsync(e, beto, sol.Id, 5);
        Assert.Single(await AvisosAsync(e, ana), n => n.Tipo == TiposNotificacion.NivelAlcanzado && n.Mensaje.Contains("Brote"));
    }

    // ------------------------------------------------------------------ Denuncias confirmadas
    [Fact]
    public async Task Una_denuncia_procedente_da_puntos_a_quien_denuncio_sin_revelarlo()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var infractor = (await e.RegistrarAsync("infractor")).Usuario.Id;
        var mod = await ModeradorAsync(e);
        var pub = await PublicarAsync(e, infractor, "Producto falso");
        var antes = await SaldoAsync(e, ana);

        var denuncia = await DenunciarAsync(e, ana, pub.Id);
        await ResolverAsync(e, mod, denuncia.Id);

        Assert.Equal(antes + PoliticaEcoPuntos.PuntosDenunciaConfirmada, await SaldoAsync(e, ana));
        var mov = Assert.Single(await MovimientosAsync(e, ana, "DenunciaConfirmada"));
        Assert.Equal(denuncia.Id, mov.ReferenciaId);                 // la denuncia propia, no la publicación ni la persona denunciada
        Assert.DoesNotContain("Producto falso", mov.Detalle ?? "");
        Assert.Contains(await AvisosAsync(e, ana), n => n.Tipo == TiposNotificacion.DenunciaRevisada && n.Mensaje.Contains("Ganaste"));

        // la persona denunciada no gana nada ni se entera de quién fue
        Assert.Empty(await MovimientosAsync(e, infractor, "DenunciaConfirmada"));
        var aviso = Assert.Single(await AvisosAsync(e, infractor), n => n.Tipo == TiposNotificacion.DenunciaRecibida);
        Assert.DoesNotContain("Ana", aviso.Mensaje);
        Assert.DoesNotContain("Eco-Puntos", aviso.Mensaje);
    }

    [Fact]
    public async Task Una_denuncia_descartada_no_da_puntos()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var mod = await ModeradorAsync(e);
        var denuncia = await DenunciarAsync(e, ana, (await PublicarAsync(e, beto)).Id);

        await ResolverAsync(e, mod, denuncia.Id, AccionDenunciaDto.Descartar);
        Assert.Empty(await MovimientosAsync(e, ana, "DenunciaConfirmada"));
    }

    [Fact]
    public async Task Resolver_dos_veces_la_misma_denuncia_no_da_puntos_dos_veces()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var mod = await ModeradorAsync(e);
        var denuncia = await DenunciarAsync(e, ana, (await PublicarAsync(e, beto)).Id);

        await ResolverAsync(e, mod, denuncia.Id);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ResolverAsync(e, mod, denuncia.Id)); // doble clic
        Assert.Single(await MovimientosAsync(e, ana, "DenunciaConfirmada"));
    }

    [Fact]
    public async Task Quien_resuelve_no_gana_puntos_por_su_propia_denuncia()
    {
        using var e = new EntornoNegocio();
        var mod = await ModeradorAsync(e);
        var beto = (await e.RegistrarAsync("beto")).Usuario.Id;
        var denuncia = await DenunciarAsync(e, mod, (await PublicarAsync(e, beto)).Id);

        await ResolverAsync(e, mod, denuncia.Id);
        Assert.Empty(await MovimientosAsync(e, mod, "DenunciaConfirmada"));
    }

    [Fact]
    public async Task Las_denuncias_con_puntos_tienen_tope_mensual()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var infractor = (await e.RegistrarAsync("infractor")).Usuario.Id;
        var mod = await ModeradorAsync(e);
        var tope = PoliticaEcoPuntos.MaxDenunciasRecompensadasPorCiclo;

        for (var i = 0; i <= tope; i++)
            await ResolverAsync(e, mod, (await DenunciarAsync(e, ana, (await PublicarAsync(e, infractor, $"Spam número {i + 1}")).Id)).Id);
        Assert.Equal(tope, (await MovimientosAsync(e, ana, "DenunciaConfirmada")).Count);
        Assert.Contains(await AvisosAsync(e, ana), n => n.Tipo == TiposNotificacion.DenunciaRevisada && !n.Mensaje.Contains("Ganaste"));

        // pasado el ciclo vuelve a sumar
        reloj.Avanzar(TimeSpan.FromDays(PoliticaEcoPuntos.DiasCicloDenuncias + 1));
        await ResolverAsync(e, mod, (await DenunciarAsync(e, ana, (await PublicarAsync(e, infractor, "Spam de nuevo")).Id)).Id);
        Assert.Equal(tope + 1, (await MovimientosAsync(e, ana, "DenunciaConfirmada")).Count);
    }
}
