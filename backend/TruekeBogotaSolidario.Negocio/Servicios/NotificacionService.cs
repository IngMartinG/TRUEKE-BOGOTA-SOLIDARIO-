using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface INotificacionService
{
    Task<PaginaDto<NotificacionDto>> ListarAsync(Guid actorId, bool soloNoLeidas, int pagina, int tamano);
    Task<int> ContarNoLeidasAsync(Guid actorId);
    Task MarcarLeidaAsync(Guid actorId, Guid notificacionId);
    Task MarcarTodasLeidasAsync(Guid actorId);
}

public sealed class NotificacionService : INotificacionService
{
    private readonly INotificacionRepository _repo;
    private readonly IUnidadDeTrabajo _uow;
    private readonly TimeProvider _reloj;

    public NotificacionService(INotificacionRepository repo, IUnidadDeTrabajo uow, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _reloj = reloj;
    }

    public async Task<PaginaDto<NotificacionDto>> ListarAsync(Guid actorId, bool soloNoLeidas, int pagina, int tamano)
    {
        var (items, total) = await _repo.ListarAsync(actorId, soloNoLeidas, pagina, tamano);
        return new PaginaDto<NotificacionDto>(items.Select(NotificadorPersistente.ADto).ToList(), total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    public Task<int> ContarNoLeidasAsync(Guid actorId) => _repo.ContarNoLeidasAsync(actorId);

    public async Task MarcarLeidaAsync(Guid actorId, Guid notificacionId)
    {
        // Solo encuentra las del propio usuario: las ajenas "no existen" (404)
        var n = await _repo.ObtenerAsync(notificacionId, actorId) ?? throw new NoEncontradoException("Notificación no encontrada.");
        n.MarcarLeida(_reloj.GetUtcNow().UtcDateTime);
        await _uow.GuardarCambiosAsync();
    }

    public async Task MarcarTodasLeidasAsync(Guid actorId)
    {
        await _repo.MarcarTodasLeidasAsync(actorId, _reloj.GetUtcNow().UtcDateTime);
        await _uow.GuardarCambiosAsync();
    }
}
