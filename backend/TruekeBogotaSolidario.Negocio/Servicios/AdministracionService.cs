using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IAdministracionService
{
    // Moderación (Administrador y SuperUsuario)
    Task OcultarPublicacionAsync(Guid actorId, Guid publicacionId, string motivo);
    Task MostrarPublicacionAsync(Guid actorId, Guid publicacionId);
    Task<IReadOnlyList<VerificacionPendienteDto>> ListarVerificacionesPendientesAsync(Guid actorId);
    Task AprobarVerificacionAsync(Guid actorId, Guid usuarioId);
    Task RechazarVerificacionAsync(Guid actorId, Guid usuarioId, string motivo);
    // Solo SuperUsuario
    Task<UsuarioDto> CambiarRolAsync(Guid actorId, Guid usuarioObjetivoId, RolDto nuevoRol);
}

/// <summary>
/// Defensa en profundidad: aunque Presentacion ya exige el rol en el endpoint, aquí se vuelve a comprobar
/// contra la base de datos (nunca contra lo que dice el token).
/// </summary>
public sealed class AdministracionService : IAdministracionService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _pubs;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAuditoriaRepository _auditoria;
    private readonly ISesionService _sesiones;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly ILogger<AdministracionService> _log;

    public AdministracionService(IUsuarioRepository usuarios, IPublicacionRepository pubs, IUnidadDeTrabajo uow,
        IAuditoriaRepository auditoria, ISesionService sesiones, TimeProvider reloj, INotificador notificador, ILogger<AdministracionService> log)
    {
        _usuarios = usuarios; _pubs = pubs; _uow = uow; _auditoria = auditoria; _sesiones = sesiones; _reloj = reloj;
        _notificador = notificador; _log = log;
    }

    private Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid? recursoId)
        => _notificador.NotificarAsync(usuarioId, new NotificacionDto(tipo, mensaje, recursoId, _reloj.GetUtcNow().UtcDateTime));

    private async Task<Usuario> ExigirRolAsync(Guid actorId, RolUsuarioEnum minimo)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < minimo) throw new AccesoDenegadoException();
        return actor;
    }

    private void Auditar(Guid actorId, string accion, string tipo, Guid objetivoId, string? detalle)
        => _auditoria.Agregar(new AuditoriaEvento(actorId, accion, tipo, objetivoId, detalle, _reloj.GetUtcNow().UtcDateTime));

    public async Task OcultarPublicacionAsync(Guid actorId, Guid publicacionId, string motivo)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var p = await _pubs.ObtenerPorIdAsync(publicacionId) ?? throw new NoEncontradoException("Publicación no encontrada.");
        p.Ocultar(motivo);
        Auditar(actorId, "PUBLICACION_OCULTADA", "Publicacion", publicacionId, motivo);
        await _uow.GuardarCambiosAsync();
        _log.LogWarning("Publicación {PublicacionId} ocultada por moderador {ActorId}", publicacionId, actorId);
        await NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionOcultada, $"Tu publicación \"{p.Titulo}\" fue ocultada por moderación.", p.Id);
    }

    public async Task MostrarPublicacionAsync(Guid actorId, Guid publicacionId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var p = await _pubs.ObtenerPorIdAsync(publicacionId) ?? throw new NoEncontradoException("Publicación no encontrada.");
        p.Mostrar();
        Auditar(actorId, "PUBLICACION_RESTAURADA", "Publicacion", publicacionId, null);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Publicación {PublicacionId} restaurada por moderador {ActorId}", publicacionId, actorId);
        await NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionRestaurada, $"Tu publicación \"{p.Titulo}\" volvió a ser visible.", p.Id);
    }

    public async Task<IReadOnlyList<VerificacionPendienteDto>> ListarVerificacionesPendientesAsync(Guid actorId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var lista = await _usuarios.ObtenerPorVerificacionAsync(EstadoVerificacion.Pendiente);
        return lista.Select(u => new VerificacionPendienteDto(u.Id, u.NombreCompleto, u.Correo, u.DocumentoVerificacionUrl ?? "", u.FechaRegistro)).ToList();
    }

    public async Task AprobarVerificacionAsync(Guid actorId, Guid usuarioId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes resolver tu propia verificación.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        u.AprobarVerificacion();
        Auditar(actorId, "VERIFICACION_APROBADA", "Usuario", usuarioId, null);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Verificación aprobada {UsuarioId} por {ActorId}", usuarioId, actorId);
        await NotificarAsync(usuarioId, TiposNotificacion.VerificacionAprobada, "Tu cuenta fue verificada.", null);
    }

    public async Task RechazarVerificacionAsync(Guid actorId, Guid usuarioId, string motivo)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes resolver tu propia verificación.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        u.RechazarVerificacion(motivo);
        Auditar(actorId, "VERIFICACION_RECHAZADA", "Usuario", usuarioId, motivo);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Verificación rechazada {UsuarioId} por {ActorId}", usuarioId, actorId);
        await NotificarAsync(usuarioId, TiposNotificacion.VerificacionRechazada, "Tu solicitud de verificación fue rechazada. Revisa el motivo en tu perfil.", null);
    }

    public async Task<UsuarioDto> CambiarRolAsync(Guid actorId, Guid usuarioObjetivoId, RolDto nuevoRol)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        if (actorId == usuarioObjetivoId) throw new ReglaDeNegocioException("No puedes cambiar tu propio rol.");
        var objetivo = await _usuarios.ObtenerPorIdAsync(usuarioObjetivoId) ?? throw new NoEncontradoException("Usuario no encontrado.");

        var rolAnterior = objetivo.Rol;
        objetivo.CambiarRol((RolUsuarioEnum)(int)nuevoRol); // sube VersionSeguridad: sus tokens anteriores quedan revocados
        if (rolAnterior == RolUsuarioEnum.SuperUsuario && objetivo.Rol != RolUsuarioEnum.SuperUsuario
            && await _usuarios.ContarPorRolAsync(RolUsuarioEnum.SuperUsuario) <= 1)
            throw new ReglaDeNegocioException("Debe existir al menos un SuperUsuario.");

        Auditar(actorId, "ROL_CAMBIADO", "Usuario", objetivo.Id, $"{rolAnterior}->{objetivo.Rol}");
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(objetivo.Id);
        _log.LogWarning("Rol de {UsuarioId} cambiado {Anterior}→{Nuevo} por {ActorId}", objetivo.Id, rolAnterior, objetivo.Rol, actorId);
        return Mapeos.AUsuarioDto(objetivo, _reloj.GetUtcNow().UtcDateTime);
    }
}
