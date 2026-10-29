using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IFotoPerfilService
{
    Task<UsuarioDto> CambiarAsync(Guid actorId, string url);
    Task<UsuarioDto> QuitarAsync(Guid actorId);
    /// <summary>Revisa un lote de fotos anteriores a la regla. Devuelve cuántas tomó del lote (para saber si quedan más).</summary>
    Task<int> RevisarFotosAnterioresAsync(int lote, CancellationToken ct = default);

    // Moderación
    Task<FotosEnRevisionDto> ListarEnRevisionAsync(Guid actorId);
    Task AprobarAsync(Guid actorId, Guid usuarioId);
    Task RechazarAsync(Guid actorId, Guid usuarioId, string motivo);
}

/// <summary>
/// Foto de perfil: misma ruta que las fotos de publicaciones (subida directa con URL firmada, validación de que el archivo es
/// del usuario y copia limpia sin GPS ni metadatos). La foto anterior se borra del almacenamiento.
/// Además debe mostrar el rostro de la persona: el detector local la aprueba, la rechaza en el momento (sin rostro) o la deja
/// en revisión para un moderador.
/// </summary>
public sealed class FotoPerfilService : IFotoPerfilService
{
    /// <summary>Lo que ve la persona cuando el proceso en segundo plano o un moderador rechaza su foto.</summary>
    public const string MotivoAutomatico = "No encontramos un rostro en tu foto de perfil.";

    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAlmacenArchivos _almacen;
    private readonly IRevisorFotoPerfil _revisor;
    private readonly IAuditoriaRepository _auditoria;
    private readonly UrlsOpciones _urls;
    private readonly FotosPerfilOpciones _opciones;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly ILogger<FotoPerfilService> _log;

    public FotoPerfilService(IUsuarioRepository usuarios, IUnidadDeTrabajo uow, IAlmacenArchivos almacen, IRevisorFotoPerfil revisor,
        IAuditoriaRepository auditoria, IOptions<UrlsOpciones> urls, IOptions<FotosPerfilOpciones> opciones, TimeProvider reloj,
        INotificador notificador, ILogger<FotoPerfilService> log)
    {
        _usuarios = usuarios; _uow = uow; _almacen = almacen; _revisor = revisor; _auditoria = auditoria; _urls = urls.Value;
        _opciones = opciones.Value; _reloj = reloj; _notificador = notificador; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private async Task<Usuario> CargarAsync(Guid id)
        => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    public async Task<UsuarioDto> CambiarAsync(Guid actorId, string url)
    {
        var u = await CargarAsync(actorId);
        string limpia;
        var estado = EstadoFotoPerfil.SinRevisar;
        if (_almacen.Habilitado)
        {
            limpia = await _almacen.ValidarArchivoPropioAsync(url, actorId, TipoArchivoDto.Imagen);
            if (await _revisor.RevisarAsync(limpia) is not { } revisada)
            {
                if (limpia != u.FotoUrl) await _almacen.EliminarImagenPropiaAsync(limpia, actorId); // no se conserva
                throw new ReglaDeNegocioException(ReglaFotoPerfil.MensajeSinRostro);
            }
            estado = revisada;
        }
        else
        {
            ValidadorUrls.ExigirHostPermitido(url, _urls.HostsPermitidosImagenes, "La foto");
            limpia = url;
        }
        var avisosPuntos = new AvisosEcoPuntos();
        avisosPuntos.Observar(u);
        var anterior = u.CambiarFoto(limpia, estado, Ahora); // puede completar el perfil (Eco-Puntos una sola vez)
        await _uow.GuardarCambiosAsync();
        if (anterior is not null && anterior != limpia) await _almacen.EliminarImagenPropiaAsync(anterior, actorId);
        await avisosPuntos.EnviarAsync(_notificador);
        return Mapeos.AUsuarioDto(u, Ahora);
    }

    public async Task<UsuarioDto> QuitarAsync(Guid actorId)
    {
        var u = await CargarAsync(actorId);
        var anterior = u.QuitarFoto();
        await _uow.GuardarCambiosAsync();
        if (anterior is not null) await _almacen.EliminarImagenPropiaAsync(anterior, actorId);
        return Mapeos.AUsuarioDto(u, Ahora);
    }

    public async Task<int> RevisarFotosAnterioresAsync(int lote, CancellationToken ct = default)
    {
        var ahora = Ahora;
        // Las que no se pudieron leer se reintentan al día siguiente, no en cada lote.
        var pendientes = await _usuarios.ListarPorEstadoFotoAsync(EstadoFotoPerfil.SinRevisar, lote, ahora.AddDays(-1));
        var rechazadas = new List<Guid>();
        foreach (var u in pendientes)
        {
            ct.ThrowIfCancellationRequested();
            var resultado = await _revisor.RevisarAsync(u.FotoUrl!, ct);
            if (resultado is { } estado) u.RegistrarRevisionFoto(estado, ahora);
            else
            {
                u.RechazarFoto(MotivoAutomatico, ahora, ahora.AddDays(_opciones.DiasPlazoCambio));
                rechazadas.Add(u.Id);
            }
        }
        await _uow.GuardarCambiosAsync();
        foreach (var id in rechazadas)
            await _notificador.NotificarAsync(id, TiposNotificacion.FotoPerfilRechazada, MensajeRechazo(_opciones.DiasPlazoCambio, MotivoAutomatico));
        return pendientes.Count;
    }

    // ---------------- Moderación ----------------

    private async Task ExigirModeradorAsync(Guid actorId)
    {
        var actor = await CargarAsync(actorId);
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
    }

    public async Task<FotosEnRevisionDto> ListarEnRevisionAsync(Guid actorId)
    {
        await ExigirModeradorAsync(actorId);
        var lista = await _usuarios.ListarPorEstadoFotoAsync(EstadoFotoPerfil.EnRevision, 100);
        var items = lista.Select(u => new FotoEnRevisionDto(u.Id, u.NombreCompleto, u.FotoUrl!, u.FotoRevisadaUtc ?? u.FechaRegistro)).ToList();
        return new FotosEnRevisionDto(items, await _usuarios.ContarPorEstadoFotoAsync(EstadoFotoPerfil.EnRevision),
            await _usuarios.ContarPorEstadoFotoAsync(EstadoFotoPerfil.SinRevisar));
    }

    public async Task AprobarAsync(Guid actorId, Guid usuarioId)
    {
        await ExigirModeradorAsync(actorId);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("Otro moderador debe revisar tu foto.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var estabaRechazada = u.EstadoFoto == EstadoFotoPerfil.Rechazada;
        u.AprobarFoto(Ahora);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "FOTO_PERFIL_APROBADA", "Usuario", usuarioId, null, Ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Foto de perfil de {UsuarioId} aprobada por {ActorId}", usuarioId, actorId);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.FotoPerfilAprobada,
            estabaRechazada ? "Revisamos de nuevo tu foto de perfil y quedó aprobada." : "Tu foto de perfil quedó aprobada.");
    }

    public async Task RechazarAsync(Guid actorId, Guid usuarioId, string motivo)
    {
        await ExigirModeradorAsync(actorId);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("Otro moderador debe revisar tu foto.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var ahora = Ahora;
        u.RechazarFoto(motivo, ahora, ahora.AddDays(_opciones.DiasPlazoCambio));
        _auditoria.Agregar(new AuditoriaEvento(actorId, "FOTO_PERFIL_RECHAZADA", "Usuario", usuarioId, motivo, ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Foto de perfil de {UsuarioId} rechazada por {ActorId}", usuarioId, actorId);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.FotoPerfilRechazada, MensajeRechazo(_opciones.DiasPlazoCambio, u.MotivoRechazoFoto!));
    }

    /// <summary>Texto del aviso (y del correo) de foto rechazada. También lo usa la denuncia "la foto no es de una persona".</summary>
    public static string MensajeRechazo(int diasPlazo, string motivo)
        => $"{motivo.TrimEnd('.')}. Cámbiala por una foto tuya donde se vea tu cara en los próximos {diasPlazo} días: " +
           "después no podrás publicar, solicitar ni chatear hasta hacerlo.";
}
