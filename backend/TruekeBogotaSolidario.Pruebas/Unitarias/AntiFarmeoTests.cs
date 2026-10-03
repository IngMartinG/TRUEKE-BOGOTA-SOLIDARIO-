using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class AntiFarmeoTests
{
    private static Task<SesionDto> RegistrarAsync(EntornoNegocio e, string nombre) => e.RegistrarAsync(nombre);

    private static async Task<SolicitudDto> CompletarTruekeAsync(EntornoNegocio e, Guid oferente, Guid receptor)
    {
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(oferente, new CrearPublicacionRequest
        { Titulo = "Libro usado", Descripcion = "Buen estado", CategoriaId = 2, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Kennedy" }));
        var sol = await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(receptor, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "Me sirve" }));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.AceptarAsync(oferente, sol.Id));
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(oferente, sol.Id));
        return await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.ConfirmarEntregaAsync(receptor, sol.Id));
    }

    private static Task<EcoPuntosResumenDto> ResumenAsync(EntornoNegocio e, Guid id)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(id));

    private static readonly int PuntosTrueke = PoliticaEcoPuntos.PuntosPorModo(ModoTransaccion.Trueke);

    [Fact]
    public async Task Solo_las_primeras_5_transacciones_del_dia_otorgan_puntos_y_reputacion()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var inicial = await ResumenAsync(e, ana);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, inicial.Saldo);

        // con personas distintas (la misma pareja solo suma una vez: ver la prueba siguiente)
        for (var i = 0; i < PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia; i++)
            await CompletarTruekeAsync(e, ana, (await RegistrarAsync(e, $"vecino{i}")).Usuario.Id);

        var tras5 = await ResumenAsync(e, ana);
        Assert.Equal(inicial.Saldo + 5 * PuntosTrueke, tras5.Saldo);
        Assert.Equal(0, tras5.TransaccionesConPuntosRestantes);

        // la 6ª se completa (el intercambio es válido) pero no da puntos ni reputación
        await CompletarTruekeAsync(e, ana, (await RegistrarAsync(e, "sexto")).Usuario.Id);
        var tras6 = await ResumenAsync(e, ana);
        Assert.Equal(tras5.Saldo, tras6.Saldo);
        Assert.Equal(tras5.Reputacion, tras6.Reputacion);

        var perfil = await e.EnScopeAsync<IAuthService, UsuarioDto>(a => a.ObtenerPerfilAsync(ana));
        Assert.Equal(6, perfil.TruekesCompletados);              // el historial sí cuenta todo
    }

    [Fact]
    public async Task La_misma_pareja_solo_suma_puntos_y_reputacion_una_vez_por_ventana()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var beto = (await RegistrarAsync(e, "beto")).Usuario.Id;

        await CompletarTruekeAsync(e, ana, beto);
        var anaTras1 = await ResumenAsync(e, ana);
        var betoTras1 = await ResumenAsync(e, beto);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida + PuntosTrueke, anaTras1.Saldo);

        // segundo y tercer intercambio entre los mismos (en cualquier sentido): válidos, pero sin puntos ni reputación
        await CompletarTruekeAsync(e, beto, ana);
        await CompletarTruekeAsync(e, ana, beto);
        var anaTras3 = await ResumenAsync(e, ana);
        var betoTras3 = await ResumenAsync(e, beto);
        Assert.Equal(anaTras1.Saldo, anaTras3.Saldo);
        Assert.Equal(betoTras1.Saldo, betoTras3.Saldo);
        Assert.Equal(anaTras1.Reputacion, anaTras3.Reputacion);

        var perfil = await e.EnScopeAsync<IAuthService, UsuarioDto>(a => a.ObtenerPerfilAsync(ana));
        Assert.Equal(3, perfil.TruekesCompletados);

        // con otra persona sí vuelve a sumar
        await CompletarTruekeAsync(e, ana, (await RegistrarAsync(e, "carla")).Usuario.Id);
        Assert.Equal(anaTras3.Saldo + PuntosTrueke, (await ResumenAsync(e, ana)).Saldo);
    }

    [Fact]
    public async Task Solo_la_primera_calificacion_a_la_misma_persona_cuenta_en_el_promedio()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var beto = (await RegistrarAsync(e, "beto")).Usuario.Id;
        var s1 = await CompletarTruekeAsync(e, ana, beto);
        var s2 = await CompletarTruekeAsync(e, ana, beto);

        await e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(beto, s1.Id, new CalificarRequest { Estrellas = 1 }));
        await e.EnScopeAsync<ISolicitudService, CalificacionDto>(s => s.CalificarAsync(beto, s2.Id, new CalificarRequest { Estrellas = 5 }));

        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(ana));
        Assert.Equal(1, perfil.TotalCalificaciones);   // la segunda (5 estrellas) no infla el promedio
        Assert.Equal(1m, perfil.CalificacionPromedio);
    }

    [Fact]
    public async Task Los_puntos_de_bienvenida_llegan_al_verificar_el_correo_y_una_sola_vez()
    {
        using var e = new EntornoNegocio();
        var r = await e.EnScopeAsync<IAuthService, ResultadoAutenticacion>(a => a.RegistrarAsync(new RegistroRequest
        {
            NombreCompleto = "Sin Verificar", Localidad = "Kennedy", Correo = $"nuevo{Guid.NewGuid():N}@t.co", Clave = "Clave12345", AceptoPoliticaDatos = true
        }));
        var id = r.Sesion.Usuario.Id;
        Assert.Equal(0, r.Sesion.Usuario.SaldoEcoPuntos);

        await e.MarcarCorreoVerificadoAsync(id);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, (await ResumenAsync(e, id)).Saldo);
        await e.MarcarCorreoVerificadoAsync(id);
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, (await ResumenAsync(e, id)).Saldo);
    }

    [Theory]
    [InlineData("Ana.Perez@gmail.com", "anaperez+trueke@gmail.com")]
    [InlineData("anaperez@gmail.com", "ana.perez@googlemail.com")]
    [InlineData("juan@empresa.co", "juan+promo@empresa.co")]
    public async Task Un_alias_del_mismo_buzon_no_permite_otra_cuenta(string original, string alias)
    {
        using var e = new EntornoNegocio();
        RegistroRequest Registro(string correo) => new()
        {
            NombreCompleto = "Persona Prueba", Localidad = "Kennedy", Correo = correo, Clave = "Clave12345", AceptoPoliticaDatos = true
        };
        await e.EnScopeAsync<IAuthService, ResultadoAutenticacion>(a => a.RegistrarAsync(Registro(original)));
        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<IAuthService, ResultadoAutenticacion>(a => a.RegistrarAsync(Registro(alias))));
        Assert.Contains("Ya existe una cuenta", ex.Message);

        // y el alias sirve para iniciar sesión en la cuenta original (es el mismo buzón)
        var login = await e.EnScopeAsync<IAuthService, ResultadoAutenticacion>(a => a.LoginAsync(new LoginRequest { Correo = alias, Clave = "Clave12345" }));
        Assert.Equal(Usuario.NormalizarCorreo(original), login.Sesion.Usuario.Correo);
    }

    [Fact]
    public async Task Los_correos_desechables_no_pueden_registrarse()
    {
        using var e = new EntornoNegocio();
        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => e.EnScopeAsync<IAuthService, ResultadoAutenticacion>(a => a.RegistrarAsync(new RegistroRequest
        {
            NombreCompleto = "Farmer Falso", Localidad = "Kennedy", Correo = "x123@mailinator.com", Clave = "Clave12345", AceptoPoliticaDatos = true
        })));
        Assert.Contains("desechables", ex.Message);
    }

    [Fact]
    public async Task No_se_puede_solicitar_la_propia_publicacion()
    {
        using var e = new EntornoNegocio();
        var ana = (await RegistrarAsync(e, "ana")).Usuario.Id;
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(ana, new CrearPublicacionRequest
        { Titulo = "Silla", Descripcion = "De madera", CategoriaId = 4, Modo = ModoDto.Donacion, Condicion = CondicionDto.Usado, Localidad = "Kennedy" }));
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
