using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IBloqueoService
{
    Task BloquearAsync(Guid actorId, Guid usuarioId);
    Task DesbloquearAsync(Guid actorId, Guid usuarioId);
    Task<IReadOnlyList<BloqueadoDto>> ListarAsync(Guid actorId);
    Task<PreferenciasAvisosDto> PreferenciasAsync(Guid actorId);
    Task<PreferenciasAvisosDto> ActualizarPreferenciasAsync(Guid actorId, ActualizarPreferenciasAvisosRequest r);
}

/// <summary>
/// Herramientas de convivencia del usuario: bloquear a alguien y elegir qué avisos le llegan por correo.
/// El bloqueo no se le notifica a la otra persona (como en las apps de mensajería): solo deja de poder escribir.
/// </summary>
public sealed class BloqueoService : IBloqueoService
{
    private readonly IBloqueoRepository _bloqueos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly TimeProvider _reloj;

    public BloqueoService(IBloqueoRepository bloqueos, IUsuarioRepository usuarios, IUnidadDeTrabajo uow, TimeProvider reloj)
    {
        _bloqueos = bloqueos; _usuarios = usuarios; _uow = uow; _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private async Task<Usuario> ActorAsync(Guid id) => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    public async Task BloquearAsync(Guid actorId, Guid usuarioId)
    {
        await ActorAsync(actorId);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes bloquearte a ti mismo.");
        var otro = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (otro is null || otro.EstaEliminado) throw new NoEncontradoException("Usuario no encontrado.");
        if (await _bloqueos.ObtenerAsync(actorId, usuarioId) is not null) return; // idempotente
        _bloqueos.Agregar(new Bloqueo(actorId, usuarioId, Ahora));
        await _uow.GuardarCambiosAsync();
    }

    public async Task DesbloquearAsync(Guid actorId, Guid usuarioId)
    {
        if (await _bloqueos.ObtenerAsync(actorId, usuarioId) is not { } b) return;
        _bloqueos.Quitar(b);
        await _uow.GuardarCambiosAsync();
    }

    public async Task<IReadOnlyList<BloqueadoDto>> ListarAsync(Guid actorId)
    {
        var ahora = Ahora;
        return (await _bloqueos.ListarDeAsync(actorId))
            .Where(b => b.Bloqueado is not null)
            .Select(b => new BloqueadoDto(Mapeos.APerfilPublico(b.Bloqueado!, ahora), b.FechaUtc)).ToList();
    }

    private static PreferenciasAvisosDto ADto(Usuario u)
        => new(u.AvisosCorreoIntercambios, u.AvisosCorreoMensajes, u.AvisosCorreoPlanes, u.AceptaNovedades);

    public async Task<PreferenciasAvisosDto> PreferenciasAsync(Guid actorId) => ADto(await ActorAsync(actorId));

    public async Task<PreferenciasAvisosDto> ActualizarPreferenciasAsync(Guid actorId, ActualizarPreferenciasAvisosRequest r)
    {
        var u = await ActorAsync(actorId);
        u.ActualizarPreferenciasAvisos(r.Intercambios, r.Mensajes, r.Planes, r.Novedades);
        await _uow.GuardarCambiosAsync();
        return ADto(u);
    }
}
