using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Moderacion;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>
/// Lo que espera a la moderación avisa a los moderadores (campana y push), se cuenta por pestaña del panel y llega en un
/// correo resumen a quien no está en línea, sin inundar a nadie ni revelar el contenido.
/// </summary>
public class AvisosModeracionTests
{
    private sealed record Equipo(EntornoNegocio E, CorreoCapturado Correo, Guid Super, string CorreoSuper, Guid Admin, string CorreoAdmin);

    private static async Task<Equipo> EquipoAsync()
    {
        var correo = new CorreoCapturado();
        var e = new EntornoNegocio(configurar: s => s.Replace(ServiceDescriptor.Singleton<ICorreoSaliente>(correo)));
        var super = (await e.RegistrarAsync("superu")).Usuario;
        await e.HacerModeradorAsync(super.Id);
        var admin = (await e.RegistrarAsync("adminu")).Usuario;
        await e.HacerModeradorAsync(admin.Id, RolUsuarioEnum.Administrador);
        return new Equipo(e, correo, super.Id, super.Correo, admin.Id, admin.Correo);
    }

    private static List<Notificacion> Avisos(EntornoNegocio e, Guid usuario, string tipo)
    {
        using var scope = e.Proveedor.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Notificaciones
            .Where(n => n.UsuarioId == usuario && n.Tipo == tipo).ToList();
    }

    private static Task DenunciarAsync(EntornoNegocio e, Guid denunciante, Guid cuenta, string? detalle = null)
        => e.EnScopeAsync<IDenunciaService, DenunciaCreadaDto>(s => s.CrearAsync(denunciante, new CrearDenunciaRequest
        {
            Tipo = TipoDenunciaDto.Usuario, ObjetivoId = cuenta, Motivo = MotivoDenunciaDto.Fraude, Detalle = detalle
        }));

    private static Task<PendientesModeracionDto> PendientesAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IAvisosModeracion, PendientesModeracionDto>(s => s.ContarAsync(actor));

    /// <summary>Solo los resúmenes de moderación (el registro también envía correos).</summary>
    private static List<MensajeCorreo> Resumenes(Equipo q, string correo)
        => q.Correo.Para(correo).Where(m => m.Asunto.EndsWith("en moderación", StringComparison.Ordinal)).ToList();

    private static ResumenModeracionHostedService Resumen(EntornoNegocio e)
        => new(e.Proveedor.GetRequiredService<EstadoResumenModeracion>(), e.Proveedor.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModeracionOpciones()), NullLogger<ResumenModeracionHostedService>.Instance);

    [Fact]
    public async Task Una_denuncia_nueva_avisa_a_todos_los_moderadores_una_sola_vez_por_contenido()
    {
        var q = await EquipoAsync();
        var sospechoso = (await q.E.RegistrarAsync("sospechoso")).Usuario.Id;
        var a = (await q.E.RegistrarAsync("vecinaa")).Usuario.Id;
        var b = (await q.E.RegistrarAsync("vecinob")).Usuario.Id;

        await DenunciarAsync(q.E, a, sospechoso, "Me pidió pagar por adelantado");
        await DenunciarAsync(q.E, b, sospechoso); // la misma cuenta: la fila del panel la agrupa y no se repite el aviso

        foreach (var moderador in new[] { q.Super, q.Admin })
        {
            var aviso = Assert.Single(Avisos(q.E, moderador, TiposNotificacion.DenunciaNueva));
            Assert.Equal("Nueva denuncia sobre una cuenta: revísala en Denuncias.", aviso.Mensaje);
            Assert.DoesNotContain("adelantado", aviso.Mensaje); // ni el contenido ni quién denunció viajan en el aviso
        }
        Assert.Equal(1, (await PendientesAsync(q.E, q.Super)).Denuncias);

        // Otra cuenta denunciada sí es un aviso nuevo; un moderador que denuncia no se avisa a sí mismo.
        var otro = (await q.E.RegistrarAsync("otro")).Usuario.Id;
        await DenunciarAsync(q.E, q.Admin, otro);
        Assert.Equal(2, Avisos(q.E, q.Super, TiposNotificacion.DenunciaNueva).Count);
        Assert.Single(Avisos(q.E, q.Admin, TiposNotificacion.DenunciaNueva));
        Assert.Equal(2, (await PendientesAsync(q.E, q.Admin)).Denuncias);
    }

    [Fact]
    public async Task Fundaciones_y_empresas_solo_le_llegan_y_le_cuentan_al_SuperUsuario()
    {
        var q = await EquipoAsync();
        await q.E.EnScopeAsync<IAvisosModeracion>(s => s.PendienteAsync(TiposNotificacion.FundacionPorRevisar, "La fundación \"X\" pidió su verificación.",
            Guid.NewGuid(), soloSuperUsuarios: true));
        Assert.Single(Avisos(q.E, q.Super, TiposNotificacion.FundacionPorRevisar));
        Assert.Empty(Avisos(q.E, q.Admin, TiposNotificacion.FundacionPorRevisar));

        var cliente = (await q.E.RegistrarAsync("cliente")).Usuario.Id;
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => PendientesAsync(q.E, cliente));
    }

    [Fact]
    public void Para_un_Administrador_fundaciones_y_empresas_van_en_cero()
    {
        var c = new Datos.Repositorios.ConteoPendientesModeracion(1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 3, 4, 0);
        Assert.Equal(10, PendientesModeracionDto.Desde(c, esSuperUsuario: true).Total);
        var admin = PendientesModeracionDto.Desde(c, esSuperUsuario: false);
        Assert.Equal((0, 0, 3), (admin.Fundaciones, admin.Empresas, admin.Total));
        Assert.Equal(["• 1 publicación por aprobar", "• 2 contenidos denunciados"], admin.Lineas());
    }

    [Fact]
    public async Task El_correo_resumen_llega_solo_a_quien_no_esta_en_linea_y_solo_si_los_pendientes_cambiaron()
    {
        var q = await EquipoAsync();
        var resumen = Resumen(q.E);
        Assert.Equal(0, await resumen.EnviarAsync()); // sin pendientes, nada

        var sospechoso = (await q.E.RegistrarAsync("sospechoso")).Usuario.Id;
        var vecina = (await q.E.RegistrarAsync("vecina")).Usuario.Id;
        await DenunciarAsync(q.E, vecina, sospechoso, "Texto privado de la denuncia");
        await q.E.Proveedor.GetRequiredService<IPresencia>().ConectarAsync(q.Super, "conexion-1"); // el SuperUsuario ve el panel en vivo

        Assert.Equal(1, await resumen.EnviarAsync());
        Assert.Empty(Resumenes(q, q.CorreoSuper));
        var correo = Assert.Single(Resumenes(q, q.CorreoAdmin));
        Assert.Equal("Hay 1 pendiente en moderación", correo.Asunto);
        Assert.Contains("1 contenido denunciado", correo.Texto);
        Assert.Contains("/admin", correo.Texto);
        Assert.DoesNotContain("Texto privado", correo.Texto);

        Assert.Equal(0, await resumen.EnviarAsync()); // los mismos números no se repiten
        await q.E.Proveedor.GetRequiredService<IPresencia>().DesconectarAsync(q.Super, "conexion-1");
        Assert.Equal(0, await resumen.EnviarAsync()); // al SuperUsuario ya se le contaron en vivo

        var otro = (await q.E.RegistrarAsync("otro")).Usuario.Id;
        await DenunciarAsync(q.E, vecina, otro);
        Assert.Equal(2, await resumen.EnviarAsync()); // cambiaron: llega a los dos
        Assert.Contains("2 contenidos denunciados", Resumenes(q, q.CorreoSuper).Single().Texto);
    }
}
