using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Plan Negocio: directorio y vitrina públicos, sello "Negocio verificado" revisado por un moderador y panel de la cuenta.</summary>
public class NegociosTests
{
    private static async Task<Guid> NegocioAsync(EntornoNegocio e, string alias, string nit = "900.123.456")
    {
        var id = (await e.RegistrarAsync(alias)).Usuario.Id;
        await e.PagarAsync(id, new IniciarPagoRequest { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = ConceptoPagoDto.Empresa });
        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilEmpresaAsync(id, new PerfilEmpresaRequest
        {
            NombreComercial = "Muebles La Esquina " + alias, Nit = nit, MostrarNit = true,
            Descripcion = "Muebles de segunda restaurados en Kennedy.", Horario = "Lunes a sábado, 9 a. m. a 6 p. m."
        }));
        return id;
    }

    private static Task<IReadOnlyList<NegocioResumenDto>> DirectorioAsync(EntornoNegocio e)
        => e.EnScopeAsync<INegocioService, IReadOnlyList<NegocioResumenDto>>(s => s.ListarAsync());

    [Fact]
    public async Task El_directorio_y_la_vitrina_muestran_solo_negocios_vigentes_con_sus_datos()
    {
        using var e = new EntornoNegocio();
        var tienda = await NegocioAsync(e, "vitrina");
        var persona = (await e.RegistrarAsync("persona")).Usuario.Id;

        var item = Assert.Single(await DirectorioAsync(e), n => n.Id == tienda);
        Assert.StartsWith("Muebles La Esquina", item.Nombre);
        Assert.Equal("Lunes a sábado, 9 a. m. a 6 p. m.", item.Horario);
        Assert.False(item.Verificado);
        Assert.DoesNotContain(await DirectorioAsync(e), n => n.Id == persona);

        var vitrina = await e.EnScopeAsync<INegocioService, NegocioPublicoDto>(s => s.ObtenerAsync(tienda));
        Assert.Equal("Muebles de segunda restaurados en Kennedy.", vitrina.Descripcion);
        Assert.Equal("900123456-8", vitrina.Nit); // eligió mostrarlo
        await Assert.ThrowsAsync<NoEncontradoException>(() => e.EnScopeAsync<INegocioService, NegocioPublicoDto>(s => s.ObtenerAsync(persona)));
    }

    [Fact]
    public async Task Un_moderador_verifica_el_NIT_y_el_sello_se_pierde_si_cambia()
    {
        using var e = new EntornoNegocio();
        var tienda = await NegocioAsync(e, "sello");
        var mod = (await e.RegistrarAsync("moderador")).Usuario.Id;
        await e.HacerModeradorAsync(mod, RolUsuarioEnum.Administrador);
        var cliente = (await e.RegistrarAsync("cliente")).Usuario.Id;

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => e.EnScopeAsync<INegocioService>(s => s.VerificarNitAsync(cliente, tienda)));
        var fila = await e.EnScopeAsync<INegocioService, IReadOnlyList<NegocioPorVerificarDto>>(s => s.ListarPorVerificarAsync(mod));
        Assert.Contains(fila, n => n.UsuarioId == tienda && n.Nit == "900123456-8");

        await e.EnScopeAsync<INegocioService>(s => s.VerificarNitAsync(mod, tienda));
        Assert.True(Assert.Single(await DirectorioAsync(e), n => n.Id == tienda).Verificado);
        var perfil = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, tienda));
        Assert.True(perfil.NegocioVerificado);
        Assert.DoesNotContain(await e.EnScopeAsync<INegocioService, IReadOnlyList<NegocioPorVerificarDto>>(s => s.ListarPorVerificarAsync(mod)),
            n => n.UsuarioId == tienda);

        // Cambiar el NIT quita el sello y vuelve a la fila.
        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilEmpresaAsync(tienda,
            new PerfilEmpresaRequest { NombreComercial = "Muebles La Esquina sello", Nit = "900.765.432" }));
        var otra = await e.EnScopeAsync<INegocioService, NegocioPublicoDto>(s => s.ObtenerAsync(tienda));
        Assert.False(otra.Verificado);
        Assert.Contains(await e.EnScopeAsync<INegocioService, IReadOnlyList<NegocioPorVerificarDto>>(s => s.ListarPorVerificarAsync(mod)),
            n => n.UsuarioId == tienda);
    }

    [Fact]
    public async Task Un_NIT_rechazado_sale_de_la_fila_hasta_que_lo_corrigen()
    {
        using var e = new EntornoNegocio();
        var tienda = await NegocioAsync(e, "rechazo");
        var mod = (await e.RegistrarAsync("revisora")).Usuario.Id;
        await e.HacerModeradorAsync(mod, RolUsuarioEnum.Administrador);

        await e.EnScopeAsync<INegocioService>(s => s.RechazarNitAsync(mod, tienda, "El NIT no coincide con el RUES."));
        Assert.DoesNotContain(await e.EnScopeAsync<INegocioService, IReadOnlyList<NegocioPorVerificarDto>>(s => s.ListarPorVerificarAsync(mod)),
            n => n.UsuarioId == tienda);
        var avisos = await e.EnScopeAsync<INotificacionService, PaginaDto<NotificacionDto>>(s => s.ListarAsync(tienda, false, 1, 50));
        Assert.Contains(avisos.Items, n => n.Tipo == TiposNotificacion.NegocioNoVerificado && n.Mensaje.Contains("RUES"));

        await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ActualizarPerfilEmpresaAsync(tienda,
            new PerfilEmpresaRequest { NombreComercial = "Muebles La Esquina rechazo", Nit = "900.765.432" }));
        Assert.Contains(await e.EnScopeAsync<INegocioService, IReadOnlyList<NegocioPorVerificarDto>>(s => s.ListarPorVerificarAsync(mod)),
            n => n.UsuarioId == tienda);
    }

    [Fact]
    public async Task Las_estadisticas_de_la_cuenta_son_de_los_planes_pagos()
    {
        using var e = new EntornoNegocio();
        var tienda = await NegocioAsync(e, "estadisticas");
        var gratis = (await e.RegistrarAsync("gratis")).Usuario.Id;
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<INegocioService, EstadisticasCuentaDto>(s => s.EstadisticasCuentaAsync(gratis)));

        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(tienda, new CrearPublicacionRequest
        { Titulo = "Silla restaurada", Descripcion = "Madera de pino", CategoriaId = 4, Modo = ModoDto.Trueke, Condicion = CondicionDto.Reparado,
          DetalleCondicion = "Patas cambiadas", Localidad = "Kennedy" }));
        var otro = (await e.RegistrarAsync("interesado")).Usuario.Id;
        await e.EnScopeAsync<ISolicitudService, SolicitudDto>(s => s.CrearAsync(otro, new CrearSolicitudRequest { PublicacionId = pub.Id, Mensaje = "Me interesa" }));
        await e.EnScopeAsync<IPublicacionService>(s => s.MarcarFavoritaAsync(otro, pub.Id));

        var est = await e.EnScopeAsync<INegocioService, EstadisticasCuentaDto>(s => s.EstadisticasCuentaAsync(tienda));
        Assert.Equal(1, est.PublicacionesActivas);
        Assert.Equal(1, est.Solicitudes30Dias);
        Assert.Equal(1, est.Favoritos);
        Assert.Equal(30, est.Serie.Count);
    }
}
