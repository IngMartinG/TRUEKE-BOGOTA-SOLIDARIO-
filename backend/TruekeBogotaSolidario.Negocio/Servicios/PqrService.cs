using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Canal formal de atención (Ley 1755 de 2015 y Ley 1480 de 2011). El usuario recibe un radicado y una fecha límite
/// de respuesta; el retracto (5 días hábiles) y la reversión del pago se piden aquí y el equipo los resuelve.
/// </summary>
public interface IPqrService
{
    Task<PqrDto> CrearAsync(Guid actorId, CrearPqrRequest r);
    Task<IReadOnlyList<PqrDto>> ListarMiasAsync(Guid actorId);
    /// <summary>Canal público, sin cuenta: captcha, límite diario por correo y acuse con el radicado al correo.</summary>
    Task<PqrDto> CrearPublicaAsync(CrearPqrPublicaRequest r);
    /// <summary>Estado de una PQRS pública con su radicado y el correo con que se radicó (si no coinciden: 404 genérico).</summary>
    Task<PqrDto> ConsultarAsync(ConsultarPqrRequest r);
}

public sealed class PqrService : IPqrService
{
    private readonly IPqrRepository _pqrs;
    private readonly IPagoRepository _pagos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly ICorreoSaliente _correo;
    private readonly LegalOpciones _legal;
    private readonly IVerificadorCaptcha _captcha;
    private readonly TimeProvider _reloj;
    private readonly ILogger<PqrService> _log;

    public PqrService(IPqrRepository pqrs, IPagoRepository pagos, IUsuarioRepository usuarios, IUnidadDeTrabajo uow, INotificador notificador,
        ICorreoSaliente correo, IOptions<LegalOpciones> legal, IVerificadorCaptcha captcha, TimeProvider reloj, ILogger<PqrService> log)
    {
        _pqrs = pqrs; _pagos = pagos; _usuarios = usuarios; _uow = uow; _notificador = notificador; _correo = correo;
        _legal = legal.Value; _captcha = captcha; _reloj = reloj; _log = log;
    }

    public async Task<PqrDto> CrearAsync(Guid actorId, CrearPqrRequest r)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        if (await _pqrs.ContarDelUsuarioDesdeAsync(actorId, ahora.AddDays(-1)) >= Limites.MaxPqrPorDia)
            throw new ReglaDeNegocioException("Ya enviaste varias solicitudes hoy. Te responderemos pronto; si es urgente, agrega detalles a una existente.");

        var tipo = (TipoPqr)(int)r.Tipo;
        string? referencia = null;
        if (tipo is TipoPqr.Retracto or TipoPqr.ReversionPago || !string.IsNullOrWhiteSpace(r.PagoReferencia))
        {
            referencia = (r.PagoReferencia ?? "").Trim();
            var pago = await _pagos.ObtenerPorReferenciaAsync(referencia);
            if (pago is null || pago.UsuarioId != actorId) throw new ReglaDeNegocioException("No encontramos ese pago entre tus pagos.");
            if (tipo is TipoPqr.Retracto or TipoPqr.ReversionPago)
            {
                if (pago.Estado != EstadoPago.Aprobado) throw new ReglaDeNegocioException("Solo se puede pedir sobre un pago aprobado que no se haya reembolsado.");
                if (await _pqrs.ExisteAbiertaParaPagoAsync(referencia)) throw new ReglaDeNegocioException("Ya tienes una solicitud abierta sobre este pago.");
            }
            if (tipo == TipoPqr.Retracto && pago.FechaResolucionUtc is { } fecha
                && Pqr.SumarDiasHabiles(fecha, Limites.DiasHabilesRetracto) < ahora)
                throw new ReglaDeNegocioException($"El derecho de retracto se ejerce dentro de los {Limites.DiasHabilesRetracto} días hábiles siguientes a la compra. " +
                                                  "Si hubo un problema con el servicio, envía un Reclamo.");
        }

        var pqr = new Pqr(actorId, tipo, r.Asunto, r.Descripcion, referencia, ahora);
        _pqrs.Agregar(pqr);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("PQR {Radicado} ({Tipo}) creada por {UsuarioId}", pqr.Radicado, pqr.Tipo, actorId);
        await AvisarRadicadoAsync(pqr, u.Correo, Mapeos.NombrePublico(u.NombreCompleto));
        return Mapeos.APqrDto(pqr);
    }

    public async Task<PqrDto> CrearPublicaAsync(CrearPqrPublicaRequest r)
    {
        await _captcha.ExigirAsync(r.CaptchaToken, AccionesCaptcha.Pqr);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var correo = Usuario.NormalizarCorreo(r.Correo);
        if (await _pqrs.ContarPublicasDelCorreoDesdeAsync(correo, ahora.AddDays(-1)) >= Limites.MaxPqrPorDia)
            throw new ReglaDeNegocioException("Ya enviaste varias solicitudes hoy desde este correo. Te responderemos pronto.");
        var pqr = Pqr.CrearPublica(r.Nombre, correo, (TipoPqr)(int)r.Tipo, r.Asunto, r.Descripcion, ahora);
        _pqrs.Agregar(pqr);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("PQR pública {Radicado} ({Tipo}) radicada", pqr.Radicado, pqr.Tipo);
        await AvisarRadicadoAsync(pqr, correo, pqr.NombreSolicitante!.Split(' ')[0]);
        return Mapeos.APqrDto(pqr);
    }

    public async Task<PqrDto> ConsultarAsync(ConsultarPqrRequest r)
    {
        await _captcha.ExigirAsync(r.CaptchaToken, AccionesCaptcha.Pqr);
        var pqr = await _pqrs.ObtenerPorRadicadoAsync(r.Radicado.Trim().ToUpperInvariant());
        // Mismo mensaje si no existe o si el correo no coincide: no se revela qué radicados existen.
        if (pqr is null || pqr.CorreoSolicitante is null || pqr.CorreoSolicitante != Usuario.NormalizarCorreo(r.Correo))
            throw new NoEncontradoException("No encontramos una solicitud con ese radicado y ese correo.");
        return Mapeos.APqrDto(pqr);
    }

    /// <summary>Acuse con el radicado y la fecha límite al correo de quien la radicó, y aviso al equipo de moderación.</summary>
    private async Task AvisarRadicadoAsync(Pqr pqr, string correo, string nombre)
    {
        _correo.Encolar(PlantillaCorreo.Crear(correo, $"Recibimos tu solicitud {pqr.Radicado}",
            $"Hola {nombre}:",
            new[]
            {
                $"Radicamos tu {Etiqueta(pqr.Tipo).ToLowerInvariant()} con el número {pqr.Radicado}.",
                $"Asunto: {pqr.Asunto}",
                $"Te responderemos a más tardar el {pqr.FechaLimiteUtc.AddHours(-5):yyyy-MM-dd} ({Pqr.DiasHabilesPara(pqr.Tipo)} días hábiles).",
                pqr.UsuarioId is null
                    ? "Puedes consultar el estado en la página de PQRS con este radicado y tu correo."
                    : "Puedes ver el estado en tu cuenta, en Soporte y PQR."
            },
            pie: string.IsNullOrWhiteSpace(_legal.CorreoContacto) ? null : $"También puedes escribirnos a {_legal.CorreoContacto}."));
        foreach (var moderador in await _usuarios.ListarIdsModeradoresAsync())
            await _notificador.NotificarAsync(moderador, TiposNotificacion.PqrNueva, $"Nueva {Etiqueta(pqr.Tipo).ToLowerInvariant()} {pqr.Radicado}: {pqr.Asunto}", pqr.Id);
    }

    public async Task<IReadOnlyList<PqrDto>> ListarMiasAsync(Guid actorId)
        => (await _pqrs.ListarPorUsuarioAsync(actorId, 100)).Select(Mapeos.APqrDto).ToList();

    public static string Etiqueta(TipoPqr t) => t switch
    {
        TipoPqr.Peticion => "Petición",
        TipoPqr.Queja => "Queja",
        TipoPqr.Reclamo => "Reclamo",
        TipoPqr.Sugerencia => "Sugerencia",
        TipoPqr.Retracto => "Solicitud de retracto",
        TipoPqr.ReversionPago => "Solicitud de reversión del pago",
        TipoPqr.Consulta => "Consulta sobre tus datos personales",
        _ => t.ToString()
    };
}
