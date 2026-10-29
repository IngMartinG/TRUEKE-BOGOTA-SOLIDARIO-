using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Moderacion;

/// <summary>
/// Revisión previa solo para las publicaciones de riesgo: el resto entra directo al catálogo. Así un equipo pequeño
/// revisa lo que importa (cuentas nuevas, contacto en el texto, posibles artículos prohibidos, varias denuncias).
/// </summary>
public interface IModeracionPublicacionesService
{
    /// <summary>Evalúa la publicación (sin guardar) y, si es de riesgo, la manda a revisión. Devuelve true si quedó esperando.</summary>
    Task<bool> EvaluarAsync(Publicacion p, Usuario duenio, bool esNueva);
    /// <summary>Tras guardar: avisa al dueño que su publicación espera revisión y actualiza el contador de los moderadores.</summary>
    Task AvisarEnRevisionAsync(Publicacion p);
    /// <summary>Saca del catálogo hasta revisarla una publicación denunciada por varias personas distintas (sin guardar).</summary>
    bool RevisarPorDenuncias(Publicacion p, int denunciantes);
    Task AvisarColaAsync();
    Task<PublicacionesEnRevisionDto> ListarAsync(Guid actorId);
    Task<ColaModeracionDto> ContarAsync(Guid actorId);
    /// <summary>Aprueba en lote; las que ya no esperan revisión se ignoran. Devuelve cuántas se aprobaron.</summary>
    Task<int> AprobarAsync(Guid actorId, IReadOnlyCollection<Guid> ids);
    Task RechazarAsync(Guid actorId, Guid id, string motivo);
}

public sealed class ModeracionPublicacionesService : IModeracionPublicacionesService
{
    private const int MaxFila = 100;
    private const int MesesDenunciasConfirmadas = 6;

    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly IDenunciaRepository _denuncias;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly IEmisorTiempoReal _emisor;
    private readonly ModeracionOpciones _opciones;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ModeracionPublicacionesService> _log;

    public ModeracionPublicacionesService(IPublicacionRepository pubs, IUsuarioRepository usuarios, IDenunciaRepository denuncias,
        IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, INotificador notificador, IEmisorTiempoReal emisor,
        IOptions<ModeracionOpciones> opciones, TimeProvider reloj, ILogger<ModeracionPublicacionesService> log)
    {
        _pubs = pubs; _usuarios = usuarios; _denuncias = denuncias; _auditoria = auditoria; _uow = uow; _notificador = notificador;
        _emisor = emisor; _opciones = opciones.Value; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<bool> EvaluarAsync(Publicacion p, Usuario duenio, bool esNueva)
    {
        // El equipo de moderación no espera a un colega para publicar.
        if (!_opciones.PreAprobacion || duenio.Rol >= RolUsuarioEnum.Administrador) return false;
        var ahora = Ahora;
        // Lo que un moderador rechazó vuelve a revisión al corregirlo, aunque el texto ya no tenga señales.
        var reenviada = p.EstadoModeracion == EstadoModeracion.Rechazada;
        var contexto = new ContextoRiesgo(duenio.FechaRegistro, duenio.EsVerificado, duenio.EstadoFoto,
            esNueva ? await _pubs.ContarAprobadasDePropietarioAsync(duenio.Id) : int.MaxValue,
            await _denuncias.ContarConfirmadasContraAsync(duenio.Id, ahora.AddMonths(-MesesDenunciasConfirmadas)), esNueva);
        var r = EvaluadorRiesgo.Evaluar(p.Titulo, p.Descripcion, p.DetalleCondicion, contexto, _opciones, ahora);
        if (!r.RequiereRevision && !reenviada && p.EstadoModeracion != EstadoModeracion.Pendiente) return false;
        var motivos = reenviada ? r.Motivos.Prepend("Corregida tras un rechazo") : r.Motivos;
        p.EnviarARevision(r.Puntaje, motivos, ahora);
        return true;
    }

    public async Task AvisarEnRevisionAsync(Publicacion p)
    {
        await _notificador.NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionEnRevision,
            $"\"{p.Titulo}\" quedó en revisión: un moderador la revisa antes de mostrarla en el catálogo (suele tardar pocas horas).", p.Id);
        await AvisarColaAsync();
    }

    public bool RevisarPorDenuncias(Publicacion p, int denunciantes)
    {
        if (!_opciones.PreAprobacion || denunciantes < _opciones.DenunciantesParaRevisar || !p.EsVisible) return false;
        p.EnviarARevision(_opciones.UmbralRiesgo, [$"Denunciada por {denunciantes} personas"], Ahora);
        return true;
    }

    /// <summary>El contador del panel se actualiza en vivo; si falla, la fila se ve igual al recargar.</summary>
    public async Task AvisarColaAsync()
    {
        try
        {
            var cola = new ColaModeracionDto(await _pubs.ContarPendientesModeracionAsync());
            await _emisor.ColaModeracionAsync(await _usuarios.ListarIdsModeradoresAsync(), cola);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo actualizar el contador de moderación");
        }
    }

    private async Task ExigirModeradorAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
    }

    public async Task<PublicacionesEnRevisionDto> ListarAsync(Guid actorId)
    {
        await ExigirModeradorAsync(actorId);
        var lista = await _pubs.ListarPendientesModeracionAsync(MaxFila);
        var items = lista.Select(p => new PublicacionEnRevisionDto(p.Id, p.Titulo, p.Descripcion, p.Condicion.ToString(), p.DetalleCondicion,
                p.Categoria!.NombreCategoria, p.Modo.ToString(), p.PrecioReferenciaCop, p.AceptaTrueke, p.Localidad,
                Divipola.NombreCompleto(p.MunicipioCodigo), p.Imagenes.OrderBy(i => i.Orden).Select(i => i.Url).ToList(),
                new AutorEnRevisionDto(p.PropietarioId, p.Propietario!.NombreCompleto, p.Propietario.FotoUrl, p.Propietario.FechaRegistro,
                    p.Propietario.EsVerificado),
                p.PuntajeRiesgo, p.MotivosRiesgo?.Split("; ") ?? [], p.EnRevisionDesdeUtc ?? p.FechaPublicacion))
            .ToList();
        return new PublicacionesEnRevisionDto(items, await _pubs.ContarPendientesModeracionAsync());
    }

    public async Task<ColaModeracionDto> ContarAsync(Guid actorId)
    {
        await ExigirModeradorAsync(actorId);
        return new ColaModeracionDto(await _pubs.ContarPendientesModeracionAsync());
    }

    public async Task<int> AprobarAsync(Guid actorId, IReadOnlyCollection<Guid> ids)
    {
        await ExigirModeradorAsync(actorId);
        var ahora = Ahora;
        var aprobadas = new List<Publicacion>();
        foreach (var id in ids.Distinct())
        {
            var p = await _pubs.ObtenerPorIdAsync(id);
            if (p is null || p.EstadoModeracion != EstadoModeracion.Pendiente) continue; // otro moderador ya la resolvió
            if (p.PropietarioId == actorId) throw new ReglaDeNegocioException("Otro moderador debe revisar tus publicaciones.");
            p.AprobarModeracion(ahora);
            _auditoria.Agregar(new AuditoriaEvento(actorId, "PUBLICACION_APROBADA", "Publicacion", p.Id, null, ahora));
            aprobadas.Add(p);
        }
        if (aprobadas.Count == 0) return 0;
        await _uow.GuardarCambiosAsync(); // RowVersion: si el dueño la editó en ese instante, 409 y se revisa de nuevo
        _log.LogInformation("{Cantidad} publicaciones aprobadas por {ActorId}", aprobadas.Count, actorId);
        foreach (var p in aprobadas)
            await _notificador.NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionAprobada,
                $"\"{p.Titulo}\" ya está en el catálogo.", p.Id);
        await AvisarColaAsync();
        return aprobadas.Count;
    }

    public async Task RechazarAsync(Guid actorId, Guid id, string motivo)
    {
        await ExigirModeradorAsync(actorId);
        var p = await _pubs.ObtenerPorIdAsync(id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        if (p.PropietarioId == actorId) throw new ReglaDeNegocioException("Otro moderador debe revisar tus publicaciones.");
        var ahora = Ahora;
        p.RechazarModeracion(motivo);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "PUBLICACION_RECHAZADA", "Publicacion", p.Id, p.MotivoRechazo, ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Publicación {PublicacionId} rechazada por {ActorId}", p.Id, actorId);
        await _notificador.NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionRechazada,
            $"\"{p.Titulo}\" no se publicó: {p.MotivoRechazo!.TrimEnd('.')}. Puedes corregirla desde Mis publicaciones y la revisaremos de nuevo.", p.Id);
        await AvisarColaAsync();
    }
}
