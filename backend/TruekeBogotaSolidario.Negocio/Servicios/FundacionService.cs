using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Fundaciones verificadas: la cuenta pide la verificación con su NIT y el certificado de la Cámara de Comercio, un SuperUsuario
/// la revisa y, si la aprueba, la cuenta lleva la insignia, tiene página pública y publica "Lo que necesitamos". La cuenta sigue
/// siendo normal: no cambian las reglas de intercambio ni los Eco-Puntos (donarle a una fundación da los mismos puntos).
/// </summary>
public interface IFundacionService
{
    /// <summary>null si la cuenta nunca pidió ser fundación.</summary>
    Task<MiFundacionDto?> MiaAsync(Guid actorId);
    Task<MiFundacionDto> SolicitarAsync(Guid actorId, SolicitarFundacionRequest r, CancellationToken ct = default);
    Task<MiFundacionDto> EditarAsync(Guid actorId, EditarFundacionRequest r);
    /// <summary>La cuenta deja de ser fundación: borra el perfil, las necesidades y el certificado si aún estaba en revisión.</summary>
    Task EliminarAsync(Guid actorId);
    Task<NecesidadDto> CrearNecesidadAsync(Guid actorId, GuardarNecesidadRequest r);
    Task<NecesidadDto> ActualizarNecesidadAsync(Guid actorId, Guid id, GuardarNecesidadRequest r);
    Task EliminarNecesidadAsync(Guid actorId, Guid id);
    /// <summary>Público: fundaciones verificadas de cuentas activas (se calcula como mucho cada 2 minutos).</summary>
    Task<IReadOnlyList<FundacionResumenDto>> ListarAsync();
    /// <summary>Público: 404 si la cuenta no es una fundación verificada visible.</summary>
    Task<FundacionPublicaDto> ObtenerAsync(Guid usuarioId);
    /// <summary>Al suprimir la cuenta (Ley 1581): quita su fundación sin guardar (lo guarda quien llama).</summary>
    Task QuitarDeCuentaAsync(Guid usuarioId);

    // Solo SuperUsuario
    Task<IReadOnlyList<FundacionAdminDto>> ListarParaRevisionAsync(Guid actorId, EstadoFundacionDto estado);
    Task AprobarAsync(Guid actorId, Guid usuarioId);
    Task RechazarAsync(Guid actorId, Guid usuarioId, string motivo);
    Task RetirarAsync(Guid actorId, Guid usuarioId, string motivo);
}

public sealed class FundacionService : IFundacionService
{
    public const int MaxListado = 200;
    private const int MaxRevision = 100;
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(2);
    private const string ClaveCache = "fundaciones:lista";

    private readonly IFundacionRepository _fundaciones;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICategoriaRepository _categorias;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAlmacenArchivos _almacen;
    private readonly INotificador _notificador;
    private readonly IMemoryCache _cache;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly ILogger<FundacionService> _log;

    public FundacionService(IFundacionRepository fundaciones, IUsuarioRepository usuarios, ICategoriaRepository categorias,
        IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, IAlmacenArchivos almacen, INotificador notificador, IMemoryCache cache,
        IOptions<UrlsOpciones> urls, TimeProvider reloj, ILogger<FundacionService> log)
    {
        _fundaciones = fundaciones; _usuarios = usuarios; _categorias = categorias; _auditoria = auditoria; _uow = uow;
        _almacen = almacen; _notificador = notificador; _cache = cache; _urls = urls.Value; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    // ---------------- La propia cuenta ----------------
    public async Task<MiFundacionDto?> MiaAsync(Guid actorId)
    {
        var f = await _fundaciones.DeUsuarioAsync(actorId);
        return f is null ? null : await AMiaAsync(f);
    }

    public async Task<MiFundacionDto> SolicitarAsync(Guid actorId, SolicitarFundacionRequest r, CancellationToken ct = default)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(u); // la foto de perfil hace de logo y la localidad dice dónde atiende
        if (!r.DeclaroRepresentacion)
            throw new ReglaDeNegocioException("Confirma que representas legalmente a la fundación o que su representante te autorizó.");
        var nit = Fundacion.NormalizarNitEsal(r.Nit);
        if (await _fundaciones.NitEnUsoAsync(nit, actorId))
            throw new ReglaDeNegocioException("Otra cuenta ya registró ese NIT. Si es un error, escríbenos por PQRS.");

        var f = await _fundaciones.DeUsuarioAsync(actorId);
        if (f?.Estado == EstadoFundacion.Pendiente) throw new ReglaDeNegocioException("Ya tienes una solicitud en revisión.");
        if (f?.Estado == EstadoFundacion.Aprobada) throw new ReglaDeNegocioException("Tu fundación ya está verificada.");

        var certificado = r.CertificadoUrl;
        if (_almacen.Habilitado) certificado = await _almacen.ValidarArchivoPropioAsync(r.CertificadoUrl, actorId, TipoArchivoDto.Documento, ct);
        else ValidadorUrls.ExigirHostPermitido(r.CertificadoUrl, _urls.HostsPermitidosDocumentos, "El certificado");

        var nueva = f is null;
        f ??= new Fundacion(actorId, Ahora);
        f.Solicitar(r.RazonSocial, nit, r.Descripcion, r.MostrarNit, certificado, Ahora);
        if (nueva) _fundaciones.Agregar(f);
        Auditar(actorId, "FUNDACION_SOLICITADA", actorId, f.Nit);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Solicitud de fundación de {UsuarioId}", actorId);

        foreach (var super in await _usuarios.ListarIdsSuperUsuariosAsync())
            if (super != actorId)
                await _notificador.NotificarAsync(super, TiposNotificacion.FundacionPorRevisar, $"La fundación \"{f.RazonSocial}\" pidió su verificación.", actorId);
        return await AMiaAsync(f);
    }

    public async Task<MiFundacionDto> EditarAsync(Guid actorId, EditarFundacionRequest r)
    {
        var f = await _fundaciones.DeUsuarioAsync(actorId) ?? throw new NoEncontradoException("Tu cuenta no tiene un perfil de fundación.");
        f.EditarPerfil(r.Descripcion, r.MostrarNit);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        return await AMiaAsync(f);
    }

    public async Task EliminarAsync(Guid actorId)
    {
        var f = await _fundaciones.DeUsuarioAsync(actorId) ?? throw new NoEncontradoException("Tu cuenta no tiene un perfil de fundación.");
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var certificado = f.CertificadoUrl;
        _fundaciones.Quitar(f); // las necesidades se borran en cascada
        u.QuitarFundacionVerificada();
        Auditar(actorId, "FUNDACION_ELIMINADA", actorId, null);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        if (certificado is not null) await _almacen.EliminarDocumentoAsync(certificado);
    }

    public async Task QuitarDeCuentaAsync(Guid usuarioId)
    {
        if (await _fundaciones.DeUsuarioAsync(usuarioId) is { } f) _fundaciones.Quitar(f);
        _cache.Remove(ClaveCache);
    }

    // ---------------- Lo que necesitamos ----------------
    public async Task<NecesidadDto> CrearNecesidadAsync(Guid actorId, GuardarNecesidadRequest r)
    {
        var f = await AprobadaPropiaAsync(actorId);
        await ValidarCategoriaAsync(r.CategoriaId);
        var actuales = await _fundaciones.NecesidadesAsync(f.Id);
        if (actuales.Count >= NecesidadFundacion.MaxPorFundacion)
            throw new ReglaDeNegocioException($"Puedes tener hasta {NecesidadFundacion.MaxPorFundacion} necesidades. Borra las que ya se cubrieron.");
        var n = new NecesidadFundacion(f.Id, r.Texto, r.CategoriaId, r.Cantidad, r.Urgente, Ahora);
        _fundaciones.AgregarNecesidad(n);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        return ANecesidadDto(n, await NombresCategoriasAsync());
    }

    public async Task<NecesidadDto> ActualizarNecesidadAsync(Guid actorId, Guid id, GuardarNecesidadRequest r)
    {
        var f = await AprobadaPropiaAsync(actorId);
        var n = (await _fundaciones.NecesidadesAsync(f.Id)).FirstOrDefault(x => x.Id == id) ?? throw new NoEncontradoException("Necesidad no encontrada.");
        await ValidarCategoriaAsync(r.CategoriaId);
        n.Editar(r.Texto, r.CategoriaId, r.Cantidad, r.Urgente);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        return ANecesidadDto(n, await NombresCategoriasAsync());
    }

    public async Task EliminarNecesidadAsync(Guid actorId, Guid id)
    {
        // Sin exigir que siga aprobada: si le retiraron la insignia, igual puede limpiar su lista
        var f = await _fundaciones.DeUsuarioAsync(actorId) ?? throw new NoEncontradoException("Necesidad no encontrada.");
        var n = (await _fundaciones.NecesidadesAsync(f.Id)).FirstOrDefault(x => x.Id == id) ?? throw new NoEncontradoException("Necesidad no encontrada.");
        _fundaciones.QuitarNecesidad(n);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
    }

    // ---------------- Páginas públicas ----------------
    public async Task<IReadOnlyList<FundacionResumenDto>> ListarAsync()
    {
        if (_cache.TryGetValue(ClaveCache, out IReadOnlyList<FundacionResumenDto>? enCache) && enCache is not null) return enCache;
        var visibles = await _fundaciones.ListarVisiblesAsync(MaxListado);
        var conteos = await _fundaciones.ContarNecesidadesAsync(visibles.Select(v => v.Fundacion.Id).ToList());
        var lista = visibles.Select(v =>
        {
            var (total, urgentes) = conteos.TryGetValue(v.Fundacion.Id, out var c) ? c : (0, 0);
            return new FundacionResumenDto(v.Usuario.Id, v.Fundacion.RazonSocial, Mapeos.FotoVisible(v.Usuario), v.Usuario.Localidad,
                Divipola.NombreCompleto(v.Usuario.MunicipioCodigo), v.Fundacion.Descripcion, total, urgentes);
        }).ToList();
        _cache.Set(ClaveCache, (IReadOnlyList<FundacionResumenDto>)lista, DuracionCache);
        return lista;
    }

    public async Task<FundacionPublicaDto> ObtenerAsync(Guid usuarioId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        var f = u is null || u.EstaEliminado || u.EstaSuspendido ? null : await _fundaciones.DeUsuarioAsync(usuarioId);
        if (u is null || f is null || !f.EstaAprobada) throw new NoEncontradoException("Fundación no encontrada.");
        var categorias = await NombresCategoriasAsync();
        var necesidades = (await _fundaciones.NecesidadesAsync(f.Id)).Select(n => ANecesidadDto(n, categorias)).ToList();
        return new FundacionPublicaDto(u.Id, f.RazonSocial, Mapeos.FotoVisible(u), u.Localidad, Divipola.NombreCompleto(u.MunicipioCodigo),
            f.Descripcion, f.MostrarNit ? f.Nit : null, f.FechaDecisionUtc ?? f.FechaSolicitudUtc, u.CalificacionPromedio,
            u.CalificacionesTotal, necesidades);
    }

    // ---------------- Revisión (SuperUsuario con verificación en dos pasos) ----------------
    public async Task<IReadOnlyList<FundacionAdminDto>> ListarParaRevisionAsync(Guid actorId, EstadoFundacionDto estado)
    {
        await ExigirSuperUsuarioAsync(actorId);
        var lista = await _fundaciones.PorEstadoAsync((EstadoFundacion)(int)estado, MaxRevision);
        var resultado = new List<FundacionAdminDto>(lista.Count);
        foreach (var f in lista)
        {
            var u = await _usuarios.ObtenerPorIdAsync(f.UsuarioId);
            if (u is null) continue;
            // El certificado vive en un bucket privado: el enlace caduca en 5 minutos
            var enlace = f.CertificadoUrl is null ? null : await _almacen.UrlLecturaTemporalAsync(f.CertificadoUrl);
            resultado.Add(new FundacionAdminDto(u.Id, u.NombreCompleto, u.Correo, f.RazonSocial, f.Nit, f.Descripcion,
                (EstadoFundacionDto)(int)f.Estado, enlace, f.MotivoDecision, f.FechaSolicitudUtc, f.FechaDecisionUtc));
        }
        return resultado;
    }

    public async Task AprobarAsync(Guid actorId, Guid usuarioId)
    {
        var (f, u) = await ParaDecidirAsync(actorId, usuarioId);
        var certificado = f.CertificadoUrl;
        if (await _fundaciones.NitEnUsoAsync(f.Nit, usuarioId)) // dos solicitudes simultáneas con el mismo NIT
            throw new ReglaDeNegocioException("Otra cuenta ya registró ese NIT.");
        f.Aprobar(Ahora);
        u.MarcarFundacionVerificada(f.RazonSocial);
        Auditar(actorId, "FUNDACION_APROBADA", usuarioId, f.Nit);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        if (certificado is not null) await _almacen.EliminarDocumentoAsync(certificado);
        _log.LogInformation("Fundación {UsuarioId} aprobada por {ActorId}", usuarioId, actorId);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.FundacionAprobada,
            "Tu fundación fue verificada: ya tienes la insignia y tu página pública. Cuenta lo que necesitan.", usuarioId);
    }

    public async Task RechazarAsync(Guid actorId, Guid usuarioId, string motivo)
    {
        var (f, _) = await ParaDecidirAsync(actorId, usuarioId);
        var certificado = f.CertificadoUrl;
        f.Rechazar(motivo, Ahora);
        Auditar(actorId, "FUNDACION_RECHAZADA", usuarioId, f.MotivoDecision);
        await _uow.GuardarCambiosAsync();
        if (certificado is not null) await _almacen.EliminarDocumentoAsync(certificado);
        _log.LogInformation("Fundación {UsuarioId} rechazada por {ActorId}", usuarioId, actorId);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.FundacionRechazada,
            "No pudimos verificar tu fundación. Revisa el motivo en tu cuenta y vuelve a intentarlo.", usuarioId);
    }

    public async Task RetirarAsync(Guid actorId, Guid usuarioId, string motivo)
    {
        await ExigirSuperUsuarioAsync(actorId);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes resolver la verificación de tu propia fundación.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var f = await _fundaciones.DeUsuarioAsync(usuarioId) ?? throw new NoEncontradoException("Esta cuenta no tiene un perfil de fundación.");
        f.Retirar(motivo, Ahora);
        u.QuitarFundacionVerificada();
        Auditar(actorId, "FUNDACION_RETIRADA", usuarioId, f.MotivoDecision);
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        _log.LogWarning("Insignia de fundación de {UsuarioId} retirada por {ActorId}", usuarioId, actorId);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.FundacionRetirada,
            "Se retiró la verificación de tu fundación. Revisa el motivo en tu cuenta.", usuarioId);
    }

    // ---------------- Apoyo ----------------
    private async Task<(Fundacion, Usuario)> ParaDecidirAsync(Guid actorId, Guid usuarioId)
    {
        await ExigirSuperUsuarioAsync(actorId);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes resolver la verificación de tu propia fundación.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var f = await _fundaciones.DeUsuarioAsync(usuarioId) ?? throw new NoEncontradoException("Esta cuenta no tiene un perfil de fundación.");
        return (f, u);
    }

    /// <summary>Defensa en profundidad: la política del endpoint ya lo exige; aquí se comprueba contra la base de datos.</summary>
    private async Task ExigirSuperUsuarioAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.SuperUsuario) throw new AccesoDenegadoException();
    }

    private async Task<Fundacion> AprobadaPropiaAsync(Guid actorId)
    {
        var f = await _fundaciones.DeUsuarioAsync(actorId);
        if (f is null || !f.EstaAprobada)
            throw new ReglaDeNegocioException("\"Lo que necesitamos\" está disponible cuando tu fundación está verificada.");
        return f;
    }

    private void Auditar(Guid actorId, string accion, Guid usuarioId, string? detalle)
        => _auditoria.Agregar(new AuditoriaEvento(actorId, accion, "Fundacion", usuarioId, detalle, Ahora));

    private async Task ValidarCategoriaAsync(int? categoriaId)
    {
        if (categoriaId is { } c && await _categorias.ObtenerPorIdAsync(c) is null) throw new ReglaDeNegocioException("La categoría no existe.");
    }

    private async Task<IReadOnlyDictionary<int, string>> NombresCategoriasAsync()
        => (await _categorias.ListarAsync()).ToDictionary(c => c.Id, c => c.NombreCategoria);

    private static NecesidadDto ANecesidadDto(NecesidadFundacion n, IReadOnlyDictionary<int, string> categorias)
        => new(n.Id, n.Texto, n.CategoriaId, n.CategoriaId is { } c && categorias.TryGetValue(c, out var nombre) ? nombre : null,
            n.Cantidad, n.Urgente, n.FechaUtc);

    private async Task<MiFundacionDto> AMiaAsync(Fundacion f)
    {
        var categorias = await NombresCategoriasAsync();
        var necesidades = (await _fundaciones.NecesidadesAsync(f.Id)).Select(n => ANecesidadDto(n, categorias)).ToList();
        return new MiFundacionDto((EstadoFundacionDto)(int)f.Estado, f.RazonSocial, f.Nit, f.Descripcion, f.MostrarNit, f.MotivoDecision,
            f.FechaSolicitudUtc, f.FechaDecisionUtc, necesidades);
    }
}
