using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class AntiFarmeoTests
{
    private static Task<SesionDto> RegistrarAsync(EntornoNegocio e, string nombre)
        => e.EnScopeAsync<IAuthService, SesionDto>(a => a.RegistrarAsync(new RegistroRequest
        { NombreCompleto = nombre + " Prueba", Localidad = "Kennedy", Correo = $"{nombre}{Guid.NewGuid():N}@t.co", Clave = "Clave12345" }));

    private static async Task CompletarTruekeAsync(EntornoNegocio e, Guid oferente, Guid receptor)
    {
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(oferente, new CrearPublicacionRequest
        { Titulo = "Libro usado", Descripcion = "Buen estado", CategoriaId = 2, Modo = ModoDto.Trueke, Localidad = "Kennedy" }));
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(receptor, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "Me sirve" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(oferente, sol.Id));
    }

    private static Task<EcoPuntosResumenDto> ResumenAsync(EntornoNegocio e, Guid id)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(id));

    [Fact]
    public async Task Solo_las_primeras_5_transacciones_del_dia_otorgan_puntos_y_reputacion()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var beto = (await RegistrarAsync(e, "beto")).Usuario.Id;
        var inicial = await ResumenAsync(e, ana);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, inicial.Saldo);

        for (var i = 0; i < PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia; i++)
            await CompletarTruekeAsync(e, ana, beto);

        var tras5 = await ResumenAsync(e, ana);
        var puntosTrueke = PoliticaEcoPuntos.PuntosPorModo(Datos.Entidades.ModoTransaccion.Trueke);
        Assert.Equal(inicial.Saldo + 5 * puntosTrueke, tras5.Saldo);
        Assert.Equal(0, tras5.TransaccionesConPuntosRestantes);

        // la 6ª se completa (el intercambio es válido) pero no da puntos ni reputación
        await CompletarTruekeAsync(e, ana, beto);
        var tras6 = await ResumenAsync(e, ana);
        Assert.Equal(tras5.Saldo, tras6.Saldo);
        Assert.Equal(tras5.Reputacion, tras6.Reputacion);

        var perfil = await e.EnScopeAsync<IAuthService, UsuarioDto>(a => a.ObtenerPerfilAsync(ana));
        Assert.Equal(6, perfil.TruekesCompletados);              // el historial sí cuenta todo
    }

    [Fact]
    public async Task No_se_puede_solicitar_la_propia_publicacion()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(ana, new CrearPublicacionRequest
        { Titulo = "Silla", Descripcion = "De madera", CategoriaId = 4, Modo = ModoDto.Donacion, Localidad = "Kennedy" }));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(ana, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "yo" })));
    }

    [Fact]
    public async Task La_reputacion_nunca_supera_el_tope()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var resumen = await ResumenAsync(e, ana);
        Assert.InRange(resumen.Reputacion, 0, PoliticaEcoPuntos.ReputacionMaxima);
    }
}
