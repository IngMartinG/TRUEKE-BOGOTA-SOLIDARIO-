using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IComentarioService
{
    /// <summary>Público. Los moderadores también ven los ocultos (marcados); el resto nunca.</summary>
    Task<PaginaDto<ComentarioDto>> ListarAsync(Guid? actorId, Guid publicacionId, int pagina, int tamano);
    Task<ComentarioDto> CrearAsync(Guid actorId, Guid publicacionId, CrearComentarioRequest r);
    Task OcultarAsync(Guid actorId, Guid comentarioId, string motivo);
    Task MostrarAsync(Guid actorId, Guid comentarioId);
}

public sealed class ComentarioService : IComentarioService
{
    private readonly IComentarioRepository _comentarios;
    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly ILogger<ComentarioService> _log;

    public ComentarioService(IComentarioRepository comentarios, IPublicacionRepository pubs, IUsuarioRepository usuarios,
        IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, TimeProvider reloj, INotificador notificador, ILogger<ComentarioService> log)
    {
        _comentarios = comentarios; _pubs = pubs; _usuarios = usuarios; _auditoria = auditoria; _uow = uow; _reloj = reloj;
        _notificador = notificador; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    /// <summary>Misma regla de visibilidad que el detalle de la publicación: lo que no puede ver "no existe".</summary>
    private async Task<(Publicacion Pub, Usuario? Actor, bool EsModerador)> CargarPublicacionVisibleAsync(Guid? actorId, Guid publicacionId)
    {
        var pub = await _pubs.ObtenerPorIdAsync(publicacionId) ?? throw new NoEncontradoException("Publicación no encontrada.");
        var actor = actorId.HasValue ? await _usuarios.ObtenerPorIdAsync(actorId.Value) : null;
        var esModerador = actor is not null && actor.Rol >= RolUsuarioEnum.Administrador;
        var esDuenio = actor is not null && pub.PropietarioId == actor.Id;
        var visible = !pub.EstaOculta && pub.Estado is EstadoPublicacionEnum.Disponible or EstadoPublicacionEnum.EnNegociacion;
        if (!visible && !esDuenio && !esModerador) throw new NoEncontradoException("Publicación no encontrada.");
        return (pub, actor, esModerador);
    }

    public async Task<PaginaDto<ComentarioDto>> ListarAsync(Guid? actorId, Guid publicacionId, int pagina, int tamano)
    {
        var (_, actor, esModerador) = await CargarPublicacionVisibleAsync(actorId, publicacionId);
        var ahora = Ahora;
        var (items, total) = await _comentarios.ListarPorPublicacionAsync(publicacionId, incluirOcultos: esModerador, pagina, tamano);
        var dtos = items.Select(c => ADto(c, actor?.Id, esModerador, ahora)).ToList();
        return new PaginaDto<ComentarioDto>(dtos, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    public async Task<ComentarioDto> CrearAsync(Guid actorId, Guid publicacionId, CrearComentarioRequest r)
    {
        var (pub, actor, _) = await CargarPublicacionVisibleAsync(actorId, publicacionId);
        if (actor is null) throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        if (pub.EstaOculta) throw new ReglaDeNegocioException("No se puede comentar una publicación oculta por moderación.");
        if (pub.Estado is EstadoPublicacionEnum.Cancelada or EstadoPublicacionEnum.Intercambiada)
            throw new ReglaDeNegocioException("La publicación ya no admite comentarios.");

        var ahora = Ahora;
        if (await _comentarios.ContarDelAutorDesdeAsync(actorId, ahora.AddHours(-1)) >= Limites.MaxComentariosPorHora)
            throw new ReglaDeNegocioException($"Puedes publicar como máximo {Limites.MaxComentariosPorHora} comentarios por hora.");

        var comentario = new Comentario(pub.Id, actorId, r.Texto, ahora);
        _comentarios.Agregar(comentario);
        await _uow.GuardarCambiosAsync();
        return ADto(comentario, actor, actorId, esModerador: false, ahora);
    }

    public async Task OcultarAsync(Guid actorId, Guid comentarioId, string motivo)
    {
        await ExigirModeradorAsync(actorId);
        var c = await _comentarios.ObtenerPorIdAsync(comentarioId) ?? throw new NoEncontradoException("Comentario no encontrado.");
        c.Ocultar(motivo);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "COMENTARIO_OCULTADO", "Comentario", c.Id, motivo, Ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogWarning("Comentario {ComentarioId} ocultado por moderador {ActorId}", c.Id, actorId);
        await _notificador.NotificarAsync(c.AutorId, TiposNotificacion.ComentarioOcultado,
            "Uno de tus comentarios fue ocultado por moderación.", c.PublicacionId);
    }

    public async Task MostrarAsync(Guid actorId, Guid comentarioId)
    {
        await ExigirModeradorAsync(actorId);
        var c = await _comentarios.ObtenerPorIdAsync(comentarioId) ?? throw new NoEncontradoException("Comentario no encontrado.");
        c.Mostrar();
        _auditoria.Agregar(new AuditoriaEvento(actorId, "COMENTARIO_RESTAURADO", "Comentario", c.Id, null, Ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Comentario {ComentarioId} restaurado por moderador {ActorId}", c.Id, actorId);
    }

    /// <summary>Defensa en profundidad: el rol se comprueba contra la base de datos, no contra el token.</summary>
    private async Task ExigirModeradorAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
    }

    private static ComentarioDto ADto(Comentario c, Guid? actorId, bool esModerador, DateTime ahora)
        => ADto(c, c.Autor!, actorId, esModerador, ahora);

    private static ComentarioDto ADto(Comentario c, Usuario autor, Guid? actorId, bool esModerador, DateTime ahora)
        => new(c.Id, c.PublicacionId, Mapeos.APerfilPublico(autor, ahora), c.Texto, c.FechaUtc,
            actorId.HasValue && c.AutorId == actorId.Value,
            esModerador && c.EstaOculto, esModerador ? c.MotivoOcultamiento : null);
}
