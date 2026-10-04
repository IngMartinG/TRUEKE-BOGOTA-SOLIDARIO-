using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Avisos OPCIONALES por correo cuando la persona no tiene la app abierta: novedades de sus intercambios y mensajes nuevos.
/// Respeta sus preferencias (Cuenta → Avisos) y nunca incluye el texto de un mensaje privado.
/// </summary>
public interface IAvisosCorreo
{
    Task NotificacionAsync(Guid usuarioId, string tipo, string mensaje);
    Task MensajeChatAsync(Guid receptorId, Guid conversacionId, string nombreAutor);
}

/// <summary>Como mucho un correo por conversación cada 2 horas (memoria de la instancia).</summary>
public sealed class LimitadorAvisosCorreo
{
    public static readonly TimeSpan Ventana = TimeSpan.FromHours(2);
    private readonly ConcurrentDictionary<(Guid, Guid), DateTime> _ultimos = new();

    public bool Permitir(Guid usuarioId, Guid conversacionId, DateTime ahoraUtc)
    {
        if (_ultimos.Count > 50_000) _ultimos.Clear(); // tope de memoria
        var clave = (usuarioId, conversacionId);
        if (_ultimos.TryGetValue(clave, out var previo) && ahoraUtc - previo < Ventana) return false;
        _ultimos[clave] = ahoraUtc;
        return true;
    }
}

public sealed class AvisosCorreo : IAvisosCorreo
{
    private static readonly HashSet<string> TiposIntercambio = new(StringComparer.Ordinal)
    {
        TiposNotificacion.SolicitudNueva, TiposNotificacion.SolicitudAceptada, TiposNotificacion.SolicitudRechazada,
        TiposNotificacion.SolicitudCancelada, TiposNotificacion.EntregaConfirmada, TiposNotificacion.IntercambioCompletado,
        TiposNotificacion.IntercambioNoConcretado, TiposNotificacion.CalificacionRecibida
    };
    private const string Pie = "Puedes elegir qué correos recibes en Cuenta → Avisos.";

    private readonly IUsuarioRepository _usuarios;
    private readonly IPresencia _presencia;
    private readonly ICorreoSaliente _correo;
    private readonly LimitadorAvisosCorreo _limitador;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AvisosCorreo> _log;

    public AvisosCorreo(IUsuarioRepository usuarios, IPresencia presencia, ICorreoSaliente correo, LimitadorAvisosCorreo limitador,
        IOptions<UrlsOpciones> urls, TimeProvider reloj, ILogger<AvisosCorreo> log)
    {
        _usuarios = usuarios; _presencia = presencia; _correo = correo; _limitador = limitador; _urls = urls.Value; _reloj = reloj; _log = log;
    }

    private string Front => _urls.Frontend.TrimEnd('/');

    private async Task<bool> EnLineaAsync(Guid usuarioId) => (await _presencia.EnLineaAsync(new[] { usuarioId })).Contains(usuarioId);

    public async Task NotificacionAsync(Guid usuarioId, string tipo, string mensaje)
    {
        if (!TiposIntercambio.Contains(tipo)) return;
        try
        {
            var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
            if (u is null || u.EstaEliminado || !u.CorreoVerificado || !u.AvisosCorreoIntercambios) return;
            if (await EnLineaAsync(usuarioId)) return; // ya lo vio en la app
            _correo.Encolar(PlantillaCorreo.Crear(u.Correo, "Novedades en tu intercambio", $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                new[] { mensaje, "Entra a Trueke para ver los detalles y responder." },
                ("Ver mis intercambios", $"{Front}/intercambios"), Pie));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo preparar el aviso por correo {Tipo} para {UsuarioId}", tipo, usuarioId);
        }
    }

    public async Task MensajeChatAsync(Guid receptorId, Guid conversacionId, string nombreAutor)
    {
        try
        {
            var u = await _usuarios.ObtenerPorIdAsync(receptorId);
            if (u is null || u.EstaEliminado || !u.CorreoVerificado || !u.AvisosCorreoMensajes) return;
            if (await EnLineaAsync(receptorId)) return;
            if (!_limitador.Permitir(receptorId, conversacionId, _reloj.GetUtcNow().UtcDateTime)) return;
            // Nunca se copia el texto del mensaje: el correo puede leerlo alguien más.
            _correo.Encolar(PlantillaCorreo.Crear(u.Correo, $"Tienes un mensaje nuevo de {nombreAutor}", $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                new[] { $"{nombreAutor} te escribió en Trueke. Responde desde el chat de la plataforma: así no compartes tu número ni tu correo." },
                ("Abrir la conversación", $"{Front}/mensajes/{conversacionId}"), Pie));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo preparar el aviso de mensaje para {UsuarioId}", receptorId);
        }
    }
}
