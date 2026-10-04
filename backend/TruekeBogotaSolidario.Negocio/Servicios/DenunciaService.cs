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

    /// <summary>Denuncias procedentes contra el actor (sin revelar quién denunció).</summary>
    Task<IReadOnlyList<DenunciaRecibidaDto>> ListarRecibidasAsync(Guid actorId);
    Task<ApelacionDto> ApelarAsync(Guid actorId, Guid resolucionId, string texto);
    Task<IReadOnlyList<ApelacionAdminDto>> ListarApelacionesAsync(Guid moderadorId, EstadoApelacionDto estado);
    Task ResolverApelacionAsync(Guid moderadorId, Guid apelacionId, bool aceptar, string nota);
}

/// <summary>
/// Denuncias con debido proceso: el moderador decide, la persona denunciada recibe el aviso (sin saber quién la denunció)
/// y puede contar su versión una vez. Otro moderador (o un SuperUsuario) revisa la apelación y, si la acepta, se revierte la medida.
/// </summary>
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
    private readonly ICalificacionRepository _calificacionesRepo;
    private readonly ILogger<DenunciaService> _log;

    public DenunciaService(IDenunciaRepository denuncias, IUsuarioRepository usuarios, IPublicacionRepository pubs, IComentarioRepository comentarios,
        IConversacionRepository conversaciones, IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, INotificador notificador,
        TimeProvider reloj, ICalificacionRepository calificacionesRepo, ILogger<DenunciaService> log)
    {
        _calificacionesRepo = calificacionesRepo;
        _denuncias = denuncias; _usuarios = usuarios; _pubs = pubs; _comentarios = comentarios; _conversaciones = conversaciones;
        _auditoria = auditoria; _uow = uow; _notificador = notificador; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public static ApelacionDto AApelacionDto(Apelacion a)
        => new(a.Id, a.Estado.ToString(), a.Texto, a.FechaUtc, a.NotaResolucion, a.FechaResolucionUtc);

    private static string MotivoLegible(MotivoDenuncia m) => m switch
    {
        MotivoDenuncia.Spam => "spam",
        MotivoDenuncia.Fraude => "posible fraude",
        MotivoDenuncia.ContenidoInapropiado => "contenido inapropiado",
        MotivoDenuncia.ArticuloProhibido => "artículo prohibido",
        MotivoDenuncia.Acoso => "acoso",
        _ => "otro motivo"
    };

    private static string ObjetoLegible(TipoObjetoDenuncia t) => t switch
    {
        TipoObjetoDenuncia.Publicacion => "una de tus publicaciones",
        TipoObjetoDenuncia.Comentario => "uno de tus comentarios",
        TipoObjetoDenuncia.Mensaje => "uno de tus mensajes del chat",
        TipoObjetoDenuncia.Calificacion => "una de tus calificaciones",
        _ => "tu cuenta"
    };

    /// <summary>
    /// Solo se denuncia lo que el usuario puede ver (si no, "no existe": 404) y nunca lo propio.
    /// Devuelve el autor del objetivo (la persona denunciada).
    /// </summary>
    private async Task<Guid> ValidarObjetivoAsync(Guid actorId, TipoObjetoDenuncia tipo, Guid objetivoId)
    {
        const string propio = "No puedes denunciar tu propio contenido.";
        switch (tipo)
        {
            case TipoObjetoDenuncia.Publicacion:
            {
                var p = await _pubs.ObtenerPorIdAsync(objetivoId);
                if (p is null || p.EstaOculta) throw new NoEncontradoException("Publicación no encontrada.");
                if (p.PropietarioId == actorId) throw new ReglaDeNegocioException(propio);
                return p.PropietarioId;
            }
            case TipoObjetoDenuncia.Comentario:
            {
                var c = await _comentarios.ObtenerPorIdAsync(objetivoId);
                if (c is null || c.EstaOculto) throw new NoEncontradoException("Comentario no encontrado.");
                if (c.AutorId == actorId) throw new ReglaDeNegocioException(propio);
                return c.AutorId;
            }
            case TipoObjetoDenuncia.Mensaje:
            {
                var m = await _conversaciones.ObtenerMensajeAsync(objetivoId);
                if (m is null || m.EstaOculto || !m.Conversacion!.EsParticipante(actorId)) throw new NoEncontradoException("Mensaje no encontrado.");
                if (m.AutorId == actorId) throw new ReglaDeNegocioException(propio);
                return m.AutorId;
            }
            case TipoObjetoDenuncia.Calificacion:
            {
                var c = await _calificacionesRepo.ObtenerAsync(objetivoId);
                if (c is null || c.ComentarioOculto || c.Comentario is null) throw new NoEncontradoException("Calificación no encontrada.");
                if (c.AutorId == actorId) throw new ReglaDeNegocioException(propio);
                return c.AutorId;
            }
            case TipoObjetoDenuncia.Usuario:
            {
                var u = await _usuarios.ObtenerPorIdAsync(objetivoId);
                if (u is null || u.EstaEliminado) throw new NoEncontradoException("Usuario no encontrado.");
                if (u.Id == actorId) throw new ReglaDeNegocioException("No puedes denunciarte a ti mismo.");
                return u.Id;
            }
            default:
                throw new ReglaDeNegocioException("Denuncia no válida.");
        }
    }

    /// <summary>Autor del objetivo aunque esté oculto (para denuncias creadas antes de guardar DenunciadoId).</summary>
    private async Task<Guid?> AutorDelObjetivoAsync(TipoObjetoDenuncia tipo, Guid id) => tipo switch
    {
        TipoObjetoDenuncia.Publicacion => (await _pubs.ObtenerPorIdAsync(id))?.PropietarioId,
        TipoObjetoDenuncia.Comentario => (await _comentarios.ObtenerPorIdAsync(id))?.AutorId,
        TipoObjetoDenuncia.Mensaje => (await _conversaciones.ObtenerMensajeAsync(id))?.AutorId,
        TipoObjetoDenuncia.Calificacion => (await _calificacionesRepo.ObtenerAsync(id))?.AutorId,
        TipoObjetoDenuncia.Usuario => (await _usuarios.ObtenerPorIdAsync(id))?.Id,
        _ => null
    };

    public async Task<DenunciaCreadaDto> CrearAsync(Guid actorId, CrearDenunciaRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        var tipo = (TipoObjetoDenuncia)(int)r.Tipo;
        var denunciado = await ValidarObjetivoAsync(actorId, tipo, r.ObjetivoId);

        if (await _denuncias.ExisteAsync(actorId, tipo, r.ObjetivoId))
            throw new ConflictoDeConcurrenciaException("Ya denunciaste este contenido. Nuestro equipo lo está revisando.");
        if (await _denuncias.ContarDelDenuncianteDesdeAsync(actorId, Ahora.AddHours(-24)) >= MaxDenunciasPorDia)
            throw new ReglaDeNegocioException($"Puedes enviar como máximo {MaxDenunciasPorDia} denuncias por día.");

        var d = new Denuncia(actorId, tipo, r.ObjetivoId, (MotivoDenuncia)(int)r.Motivo, r.Detalle, Ahora, denunciado);
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
        TipoObjetoDenuncia.Calificacion => await _calificacionesRepo.ObtenerAsync(id) is { } k ? (true, Recortar($"{k.Estrellas}★ {k.Comentario}")) : (false, null),
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
        if (accion == AccionDenunciaDto.OcultarContenido)
        {
            switch (d.Tipo)
            {
                case TipoObjetoDenuncia.Publicacion:
                {
                    var p = await _pubs.ObtenerPorIdAsync(d.ObjetivoId) ?? throw new NoEncontradoException("La publicación ya no existe.");
                    if (!p.EstaOculta) p.Ocultar(nota!);
                    break;
                }
                case TipoObjetoDenuncia.Comentario:
                {
                    var c = await _comentarios.ObtenerPorIdAsync(d.ObjetivoId) ?? throw new NoEncontradoException("El comentario ya no existe.");
                    if (!c.EstaOculto) c.Ocultar(nota!);
                    break;
                }
                case TipoObjetoDenuncia.Mensaje:
                {
                    var m = await _conversaciones.ObtenerMensajeAsync(d.ObjetivoId) ?? throw new NoEncontradoException("El mensaje ya no existe.");
                    if (!m.EstaOculto) m.Ocultar(nota!);
                    break;
                }
                case TipoObjetoDenuncia.Calificacion:
                {
                    var k = await _calificacionesRepo.ObtenerAsync(d.ObjetivoId) ?? throw new NoEncontradoException("La calificación ya no existe.");
                    if (!k.ComentarioOculto) k.OcultarComentario(nota!);
                    break;
                }
                default:
                    throw new ReglaDeNegocioException("A un usuario no se le puede ocultar: usa MarcarRevisada y, si corresponde, suspende su cuenta.");
            }
        }

        var accionTomada = (AccionModeracion)(int)accion;
        var procedente = accionTomada != AccionModeracion.Descartar;
        var denunciado = d.DenunciadoId ?? await AutorDelObjetivoAsync(d.Tipo, d.ObjetivoId);
        var resolucionId = Guid.NewGuid();
        var grupo = await _denuncias.PendientesDelObjetivoAsync(d.Tipo, d.ObjetivoId);
        foreach (var x in grupo) x.Resolver(moderadorId, accionTomada, nota, ahora, resolucionId, denunciado);
        _auditoria.Agregar(new AuditoriaEvento(moderadorId, $"DENUNCIA_{accion.ToString().ToUpperInvariant()}", d.Tipo.ToString(), d.ObjetivoId, nota, ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Denuncias sobre {Tipo} {ObjetivoId} resueltas ({Accion}) por {ModeradorId}", d.Tipo, d.ObjetivoId, accion, moderadorId);

        foreach (var denunciante in grupo.Select(x => x.DenuncianteId).Distinct())
            await _notificador.NotificarAsync(denunciante, TiposNotificacion.DenunciaRevisada,
                procedente ? "Revisamos tu denuncia y tomamos medidas. ¡Gracias por cuidar la comunidad!" : "Revisamos tu denuncia y no encontramos una infracción.");

        // La otra parte: solo si la denuncia fue procedente (si se descarta no hay nada que defender y avisar solo invitaría a represalias).
        // Nunca se dice quién denunció.
        if (procedente && denunciado is { } idDenunciado)
        {
            var motivos = string.Join(", ", grupo.Select(x => x.Motivo).Distinct().Select(MotivoLegible));
            var medida = accionTomada == AccionModeracion.OcultarContenido
                ? "y lo ocultó"
                : "y lo consideró válido";
            await _notificador.NotificarAsync(idDenunciado, TiposNotificacion.DenunciaRecibida,
                $"Un moderador revisó un reporte por {motivos} sobre {ObjetoLegible(d.Tipo)} {medida}. " +
                $"Si no estás de acuerdo, cuéntanos tu versión en los próximos {Apelacion.DiasParaApelar} días.", resolucionId);
        }
    }

    // ---------------- La otra parte: denuncias recibidas y apelaciones ----------------

    public async Task<IReadOnlyList<DenunciaRecibidaDto>> ListarRecibidasAsync(Guid actorId)
    {
        var ahora = Ahora;
        var grupos = (await _denuncias.ListarRecibidasAsync(actorId, 500))
            .GroupBy(d => d.ResolucionId!.Value).Take(100).ToList();
        var apelaciones = await _denuncias.ApelacionesDeResolucionesAsync(grupos.Select(g => g.Key).ToList());

        var resultado = new List<DenunciaRecibidaDto>();
        foreach (var g in grupos)
        {
            var d = g.First();
            var (_, vista) = await VistaPreviaAsync(d.Tipo, d.ObjetivoId);
            apelaciones.TryGetValue(g.Key, out var apelacion);
            var limite = d.FechaResolucionUtc!.Value.AddDays(Apelacion.DiasParaApelar);
            resultado.Add(new DenunciaRecibidaDto(g.Key, d.Tipo.ToString(), d.ObjetivoId,
                g.Select(x => x.Motivo.ToString()).Distinct().ToList(), (d.Accion ?? AccionModeracion.MarcarRevisada).ToString(),
                d.NotaResolucion, vista, d.FechaResolucionUtc.Value,
                apelacion is null && limite > ahora ? limite : null,
                apelacion is null ? null : AApelacionDto(apelacion)));
        }
        return resultado;
    }

    public async Task<ApelacionDto> ApelarAsync(Guid actorId, Guid resolucionId, string texto)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        var grupo = await _denuncias.DeResolucionAsync(resolucionId);
        var d = grupo.FirstOrDefault();
        // Quien no es la persona denunciada ni siquiera sabe que la resolución existe
        if (d is null || d.DenunciadoId != actorId || d.Estado != EstadoDenuncia.Resuelta)
            throw new NoEncontradoException("Denuncia no encontrada.");

        var ahora = Ahora;
        if (d.FechaResolucionUtc!.Value.AddDays(Apelacion.DiasParaApelar) <= ahora)
            throw new ReglaDeNegocioException($"El plazo para apelar ({Apelacion.DiasParaApelar} días) ya venció.");
        if (await _denuncias.ExisteApelacionAsync(resolucionId))
            throw new ConflictoDeConcurrenciaException("Ya enviaste tu versión sobre esta denuncia. Nuestro equipo la está revisando.");

        var a = new Apelacion(resolucionId, actorId, d.Tipo, d.ObjetivoId, d.Accion ?? AccionModeracion.MarcarRevisada, texto, ahora);
        _denuncias.AgregarApelacion(a);
        await _uow.GuardarCambiosAsync(); // índice único por resolución: dos envíos simultáneos → 409
        _log.LogInformation("Apelación {ApelacionId} sobre la resolución {ResolucionId}", a.Id, resolucionId);

        foreach (var moderador in await _usuarios.ListarIdsModeradoresAsync())
            await _notificador.NotificarAsync(moderador, TiposNotificacion.ApelacionNueva,
                "Una persona denunciada envió su versión y pide revisar la decisión.", a.Id);
        return AApelacionDto(a);
    }

    /// <summary>Quien resolvió la denuncia no revisa su propia decisión (salvo un SuperUsuario).</summary>
    private static bool PuedeResolverApelacion(Usuario moderador, Denuncia original)
        => moderador.Rol == RolUsuarioEnum.SuperUsuario || original.ModeradorId != moderador.Id;

    public async Task<IReadOnlyList<ApelacionAdminDto>> ListarApelacionesAsync(Guid moderadorId, EstadoApelacionDto estado)
    {
        var moderador = await ExigirModeradorAsync(moderadorId);
        var resultado = new List<ApelacionAdminDto>();
        foreach (var a in await _denuncias.ListarApelacionesAsync((EstadoApelacion)(int)estado, 100))
        {
            var grupo = await _denuncias.DeResolucionAsync(a.ResolucionId);
            var original = grupo.First();
            var (_, vista) = await VistaPreviaAsync(a.Tipo, a.ObjetivoId);
            resultado.Add(new ApelacionAdminDto(a.Id, a.ResolucionId, a.Tipo.ToString(), a.ObjetivoId, a.AccionOriginal.ToString(),
                grupo.Select(x => x.Motivo.ToString()).Distinct().ToList(),
                grupo.Where(x => x.Detalle is not null).Select(x => x.Detalle!).Take(10).ToList(),
                original.NotaResolucion, vista, a.Texto, a.FechaUtc, a.Estado.ToString(), a.NotaResolucion,
                a.Estado == EstadoApelacion.Pendiente && PuedeResolverApelacion(moderador, original)));
        }
        return resultado;
    }

    public async Task ResolverApelacionAsync(Guid moderadorId, Guid apelacionId, bool aceptar, string nota)
    {
        var moderador = await ExigirModeradorAsync(moderadorId);
        var a = await _denuncias.ObtenerApelacionAsync(apelacionId) ?? throw new NoEncontradoException("Apelación no encontrada.");
        if (a.Estado != EstadoApelacion.Pendiente) throw new ReglaDeNegocioException("La apelación ya fue resuelta.");
        var grupo = await _denuncias.DeResolucionAsync(a.ResolucionId);
        if (!PuedeResolverApelacion(moderador, grupo.First()))
            throw new ReglaDeNegocioException("Otro moderador debe revisar esta apelación: tú resolviste la denuncia original.");

        var ahora = Ahora;
        // Aceptada: se revierte la medida. Si el contenido ya no existe o ya está visible, no hay nada que restaurar.
        if (aceptar && a.AccionOriginal == AccionModeracion.OcultarContenido)
        {
            switch (a.Tipo)
            {
                case TipoObjetoDenuncia.Publicacion:
                    if (await _pubs.ObtenerPorIdAsync(a.ObjetivoId) is { EstaOculta: true } p) p.Mostrar();
                    break;
                case TipoObjetoDenuncia.Comentario:
                    if (await _comentarios.ObtenerPorIdAsync(a.ObjetivoId) is { EstaOculto: true } c) c.Mostrar();
                    break;
                case TipoObjetoDenuncia.Mensaje:
                    if (await _conversaciones.ObtenerMensajeAsync(a.ObjetivoId) is { EstaOculto: true } m) m.Mostrar();
                    break;
                case TipoObjetoDenuncia.Calificacion:
                    if (await _calificacionesRepo.ObtenerAsync(a.ObjetivoId) is { ComentarioOculto: true } k) k.MostrarComentario();
                    break;
            }
        }

        a.Resolver(moderadorId, aceptar, nota, ahora);
        _auditoria.Agregar(new AuditoriaEvento(moderadorId, aceptar ? "APELACION_ACEPTADA" : "APELACION_RECHAZADA", a.Tipo.ToString(), a.ObjetivoId, nota, ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Apelación {ApelacionId} {Resultado} por {ModeradorId}", a.Id, aceptar ? "aceptada" : "rechazada", moderadorId);

        await _notificador.NotificarAsync(a.UsuarioId, TiposNotificacion.ApelacionResuelta,
            aceptar
                ? (a.AccionOriginal == AccionModeracion.OcultarContenido
                    ? "Revisamos tu versión y te damos la razón: tu contenido vuelve a estar visible."
                    : "Revisamos tu versión y te damos la razón: retiramos la denuncia.") + $" {a.NotaResolucion}"
                : $"Revisamos tu versión y mantenemos la decisión. {a.NotaResolucion}",
            a.ResolucionId);

        // Las dos partes se enteran del resultado final: si se revierte, también los denunciantes.
        if (aceptar)
            foreach (var denunciante in grupo.Select(x => x.DenuncianteId).Distinct())
                await _notificador.NotificarAsync(denunciante, TiposNotificacion.DenunciaRevisada,
                    "Revisamos de nuevo una denuncia que hiciste, esta vez con la versión de la otra parte, y revertimos la medida.");
    }
}
