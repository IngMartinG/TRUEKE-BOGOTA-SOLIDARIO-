using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IDenunciaService
{
    Task<DenunciaCreadaDto> CrearAsync(Guid actorId, CrearDenunciaRequest r);
    Task<IReadOnlyList<DenunciaAgrupadaDto>> ListarAsync(Guid moderadorId, EstadoDenunciaDto estado);
    Task ResolverAsync(Guid moderadorId, Guid denunciaId, AccionDenunciaDto accion, string? nota);
}

public sealed class DenunciaService : IDenunciaService
{
    public const int MaxDenunciasPorDia = 10;

    private readonly IDenunciaRepository _denuncias;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _pubs;
    private readonly IComentarioRepository _comentarios;
    private readonly IConversacionRepository _conversaciones;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DenunciaService> _log;

    public DenunciaService(IDenunciaRepository denuncias, IUsuarioRepository usuarios, IPublicacionRepository pubs, IComentarioRepository comentarios,
        IConversacionRepository conversaciones, IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, INotificador notificador,
        TimeProvider reloj, ILogger<DenunciaService> log)
    {
        _denuncias = denuncias; _usuarios = usuarios; _pubs = pubs; _comentarios = comentarios; _conversaciones = conversaciones;
        _auditoria = auditoria; _uow = uow; _notificador = notificador; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Solo se denuncia lo que el usuario puede ver (si no, "no existe": 404) y nunca lo propio.
    /// Devuelve el autor del objetivo para notificarlo si se oculta.
    /// </summary>
    private async Task ValidarObjetivoAsync(Guid actorId, TipoObjetoDenuncia tipo, Guid objetivoId)
    {
        const string propio = "No puedes denunciar tu propio contenido.";
        switch (tipo)
        {
            case TipoObjetoDenuncia.Publicacion:
            {
                var p = await _pubs.ObtenerPorIdAsync(objetivoId);
                if (p is null || p.EstaOculta) throw new NoEncontradoException("Publicación no encontrada.");
                if (p.PropietarioId == actorId) throw new ReglaDeNegocioException(propio);
                break;
            }
            case TipoObjetoDenuncia.Comentario:
            {
                var c = await _comentarios.ObtenerPorIdAsync(objetivoId);
                if (c is null || c.EstaOculto) throw new NoEncontradoException("Comentario no encontrado.");
                if (c.AutorId == actorId) throw new ReglaDeNegocioException(propio);
                break;
            }
            case TipoObjetoDenuncia.Mensaje:
            {
                var m = await _conversaciones.ObtenerMensajeAsync(objetivoId);
                if (m is null || m.EstaOculto || !m.Conversacion!.EsParticipante(actorId)) throw new NoEncontradoException("Mensaje no encontrado.");
                if (m.AutorId == actorId) throw new ReglaDeNegocioException(propio);
                break;
            }
            case TipoObjetoDenuncia.Usuario:
            {
                var u = await _usuarios.ObtenerPorIdAsync(objetivoId);
                if (u is null || u.EstaEliminado) throw new NoEncontradoException("Usuario no encontrado.");
                if (u.Id == actorId) throw new ReglaDeNegocioException("No puedes denunciarte a ti mismo.");
                break;
            }
            default:
                throw new ReglaDeNegocioException("Denuncia no válida.");
        }
    }

    public async Task<DenunciaCreadaDto> CrearAsync(Guid actorId, CrearDenunciaRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        var tipo = (TipoObjetoDenuncia)(int)r.Tipo;
        await ValidarObjetivoAsync(actorId, tipo, r.ObjetivoId);

        if (await _denuncias.ExisteAsync(actorId, tipo, r.ObjetivoId))
            throw new ConflictoDeConcurrenciaException("Ya denunciaste este contenido. Nuestro equipo lo está revisando.");
        if (await _denuncias.ContarDelDenuncianteDesdeAsync(actorId, Ahora.AddHours(-24)) >= MaxDenunciasPorDia)
            throw new ReglaDeNegocioException($"Puedes enviar como máximo {MaxDenunciasPorDia} denuncias por día.");

        var d = new Denuncia(actorId, tipo, r.ObjetivoId, (MotivoDenuncia)(int)r.Motivo, r.Detalle, Ahora);
        _denuncias.Agregar(d);
        await _uow.GuardarCambiosAsync(); // índice único: dos envíos simultáneos → 409
        _log.LogInformation("Denuncia {DenunciaId} sobre {Tipo} {ObjetivoId}", d.Id, tipo, r.ObjetivoId);
        return new DenunciaCreadaDto(d.Id, d.Estado.ToString(), d.FechaUtc);
    }

    private async Task<Usuario> ExigirModeradorAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
        return actor;
    }

    private static string? Recortar(string? texto) => texto is null ? null : texto.Length > 200 ? texto[..200] + "…" : texto;

    private async Task<(bool Existe, string? Vista)> VistaPreviaAsync(TipoObjetoDenuncia tipo, Guid id) => tipo switch
    {
        TipoObjetoDenuncia.Publicacion => await _pubs.ObtenerPorIdAsync(id) is { } p ? (true, Recortar($"{p.Titulo}: {p.Descripcion}")) : (false, null),
        TipoObjetoDenuncia.Comentario => await _comentarios.ObtenerPorIdAsync(id) is { } c ? (true, Recortar(c.Texto)) : (false, null),
        TipoObjetoDenuncia.Mensaje => await _conversaciones.ObtenerMensajeAsync(id) is { } m ? (true, Recortar(m.Texto)) : (false, null),
        TipoObjetoDenuncia.Usuario => await _usuarios.ObtenerPorIdAsync(id) is { } u ? (true, Mapeos.NombrePublico(u.NombreCompleto)) : (false, null),
        _ => (false, null)
    };

    public async Task<IReadOnlyList<DenunciaAgrupadaDto>> ListarAsync(Guid moderadorId, EstadoDenunciaDto estado)
    {
        await ExigirModeradorAsync(moderadorId);
        var lista = await _denuncias.ListarPorEstadoAsync((EstadoDenuncia)(int)estado, 500);
        var resultado = new List<DenunciaAgrupadaDto>();
        foreach (var g in lista.GroupBy(d => (d.Tipo, d.ObjetivoId)).OrderByDescending(g => g.Count()).ThenBy(g => g.Min(d => d.FechaUtc)).Take(100))
        {
            var (existe, vista) = await VistaPreviaAsync(g.Key.Tipo, g.Key.ObjetivoId);
            var primera = g.OrderBy(d => d.FechaUtc).First();
            resultado.Add(new DenunciaAgrupadaDto(primera.Id, g.Key.Tipo.ToString(), g.Key.ObjetivoId, g.Count(),
                g.Select(d => d.Motivo.ToString()).Distinct().ToList(),
                g.Where(d => d.Detalle is not null).Select(d => d.Detalle!).Take(10).ToList(),
                vista, existe, primera.FechaUtc, g.Max(d => d.FechaUtc), primera.Estado.ToString()));
        }
        return resultado;
    }

    public async Task ResolverAsync(Guid moderadorId, Guid denunciaId, AccionDenunciaDto accion, string? nota)
    {
        await ExigirModeradorAsync(moderadorId);
        var d = await _denuncias.ObtenerAsync(denunciaId) ?? throw new NoEncontradoException("Denuncia no encontrada.");
        if (d.Estado != EstadoDenuncia.Pendiente) throw new ReglaDeNegocioException("La denuncia ya fue resuelta.");
        if (accion == AccionDenunciaDto.OcultarContenido && string.IsNullOrWhiteSpace(nota))
            throw new ReglaDeNegocioException("Indica el motivo para ocultar el contenido (lo verá su autor).");

        var ahora = Ahora;
        (Guid AutorId, string Tipo, string Mensaje, Guid? Recurso)? avisoAutor = null;

        if (accion == AccionDenunciaDto.OcultarContenido)
        {
            switch (d.Tipo)
            {
                case TipoObjetoDenuncia.Publicacion:
                {
                    var p = await _pubs.ObtenerPorIdAsync(d.ObjetivoId) ?? throw new NoEncontradoException("La publicación ya no existe.");
                    if (!p.EstaOculta) p.Ocultar(nota!);
                    avisoAutor = (p.PropietarioId, TiposNotificacion.PublicacionOcultada, $"Tu publicación \"{p.Titulo}\" fue ocultada tras una denuncia.", p.Id);
                    break;
                }
                case TipoObjetoDenuncia.Comentario:
                {
                    var c = await _comentarios.ObtenerPorIdAsync(d.ObjetivoId) ?? throw new NoEncontradoException("El comentario ya no existe.");
                    if (!c.EstaOculto) c.Ocultar(nota!);
                    avisoAutor = (c.AutorId, TiposNotificacion.ComentarioOcultado, "Uno de tus comentarios fue ocultado tras una denuncia.", c.PublicacionId);
                    break;
                }
                case TipoObjetoDenuncia.Mensaje:
                {
                    var m = await _conversaciones.ObtenerMensajeAsync(d.ObjetivoId) ?? throw new NoEncontradoException("El mensaje ya no existe.");
                    if (!m.EstaOculto) m.Ocultar(nota!);
                    avisoAutor = (m.AutorId, TiposNotificacion.MensajeOcultado, "Uno de tus mensajes fue ocultado tras una denuncia.", m.ConversacionId);
                    break;
                }
                default:
                    throw new ReglaDeNegocioException("A un usuario no se le puede ocultar: usa MarcarRevisada y, si corresponde, gestiona su cuenta.");
            }
        }

        var procedente = accion != AccionDenunciaDto.Descartar;
        var grupo = await _denuncias.PendientesDelObjetivoAsync(d.Tipo, d.ObjetivoId);
        foreach (var x in grupo) x.Resolver(moderadorId, procedente, nota, ahora);
        _auditoria.Agregar(new AuditoriaEvento(moderadorId, $"DENUNCIA_{accion.ToString().ToUpperInvariant()}", d.Tipo.ToString(), d.ObjetivoId, nota, ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Denuncias sobre {Tipo} {ObjetivoId} resueltas ({Accion}) por {ModeradorId}", d.Tipo, d.ObjetivoId, accion, moderadorId);

        foreach (var denunciante in grupo.Select(x => x.DenuncianteId).Distinct())
            await _notificador.NotificarAsync(denunciante, TiposNotificacion.DenunciaRevisada,
                procedente ? "Revisamos tu denuncia y tomamos medidas. ¡Gracias por cuidar la comunidad!" : "Revisamos tu denuncia y no encontramos una infracción.");
        if (avisoAutor is { } a) await _notificador.NotificarAsync(a.AutorId, a.Tipo, a.Mensaje, a.Recurso);
    }
}
