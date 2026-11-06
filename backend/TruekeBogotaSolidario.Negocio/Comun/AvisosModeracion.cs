using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Moderacion;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Todo lo que espera a un moderador le llega a la campana, en vivo al panel (insignias) y por push a sus dispositivos.
/// Se llama SIEMPRE después de confirmar la operación de negocio y nunca la hace fallar.
/// </summary>
public interface IAvisosModeracion
{
    /// <summary>
    /// Avisa a todos los moderadores (o solo a los SuperUsuarios: fundaciones y empresas) y actualiza las insignias del panel.
    /// <paramref name="excluir"/> evita avisarle a quien originó el pendiente (un moderador que denuncia o pide verificarse).
    /// </summary>
    Task PendienteAsync(string tipo, string mensaje, Guid? recursoId = null, bool soloSuperUsuarios = false, Guid? excluir = null);
    /// <summary>Cambió lo que espera revisión (alguien lo resolvió o entró algo sin aviso propio): solo las insignias.</summary>
    Task ActualizarContadoresAsync();
    /// <summary>Pendientes por categoría según el rol de quien pregunta (fundaciones y empresas solo para el SuperUsuario).</summary>
    Task<PendientesModeracionDto> ContarAsync(Guid actorId);
}

public sealed class AvisosModeracion : IAvisosModeracion
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPendientesModeracionRepository _pendientes;
    private readonly INotificador _notificador;
    private readonly IEmisorTiempoReal _emisor;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AvisosModeracion> _log;

    public AvisosModeracion(IUsuarioRepository usuarios, IPendientesModeracionRepository pendientes, INotificador notificador,
        IEmisorTiempoReal emisor, TimeProvider reloj, ILogger<AvisosModeracion> log)
    {
        _usuarios = usuarios; _pendientes = pendientes; _notificador = notificador; _emisor = emisor; _reloj = reloj; _log = log;
    }

    public async Task PendienteAsync(string tipo, string mensaje, Guid? recursoId = null, bool soloSuperUsuarios = false, Guid? excluir = null)
    {
        try
        {
            var destinatarios = soloSuperUsuarios ? await _usuarios.ListarIdsSuperUsuariosAsync() : await _usuarios.ListarIdsModeradoresAsync();
            foreach (var id in destinatarios.Where(id => id != excluir))
                await _notificador.NotificarAsync(id, tipo, mensaje, recursoId);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo avisar a la moderación ({Tipo})", tipo);
        }
        await ActualizarContadoresAsync();
    }

    /// <summary>Si falla, las insignias se ven igual al recargar el panel.</summary>
    public async Task ActualizarContadoresAsync()
    {
        try
        {
            var conteo = await _pendientes.ContarAsync(_reloj.GetUtcNow().UtcDateTime);
            var supers = (await _usuarios.ListarIdsSuperUsuariosAsync()).ToHashSet();
            var admins = (await _usuarios.ListarIdsModeradoresAsync()).Where(id => !supers.Contains(id)).ToList();
            if (supers.Count > 0) await _emisor.ColaModeracionAsync(supers, PendientesModeracionDto.Desde(conteo, esSuperUsuario: true));
            if (admins.Count > 0) await _emisor.ColaModeracionAsync(admins, PendientesModeracionDto.Desde(conteo, esSuperUsuario: false));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron actualizar los contadores de moderación");
        }
    }

    public async Task<PendientesModeracionDto> ContarAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
        var conteo = await _pendientes.ContarAsync(_reloj.GetUtcNow().UtcDateTime);
        return PendientesModeracionDto.Desde(conteo, actor.Rol == RolUsuarioEnum.SuperUsuario);
    }
}

/// <summary>
/// Lo último que se le contó por correo a cada moderador. Vive en la memoria de la instancia: si se reinicia, el
/// siguiente resumen puede repetir los mismos números una vez (no se pierde nada).
/// </summary>
public sealed class EstadoResumenModeracion
{
    private readonly ConcurrentDictionary<Guid, string> _ultimo = new();

    /// <summary>true si estos números aún no se le contaron (y los registra).</summary>
    public bool EsNuevo(Guid moderadorId, string firma)
    {
        if (_ultimo.TryGetValue(moderadorId, out var anterior) && anterior == firma) return false;
        _ultimo[moderadorId] = firma;
        return true;
    }

    /// <summary>Sin pendientes: el próximo pendiente vuelve a avisar aunque los números coincidan con los de antes.</summary>
    public void Olvidar(Guid moderadorId) => _ultimo.TryRemove(moderadorId, out _);
}

/// <summary>
/// Cada <see cref="ModeracionOpciones.ResumenCorreoHoras"/> horas, un correo a cada moderador que no está en línea con
/// cuántas cosas esperan revisión, solo si hay pendientes y cambiaron desde el último correo. Lleva solo números y un
/// enlace al panel: nunca el contenido denunciado ni datos de terceros.
/// </summary>
public sealed class ResumenModeracionHostedService : BackgroundService
{
    private readonly EstadoResumenModeracion _estado;
    private readonly IServiceScopeFactory _fabrica;
    private readonly ModeracionOpciones _opciones;
    private readonly ILogger<ResumenModeracionHostedService> _log;

    public ResumenModeracionHostedService(EstadoResumenModeracion estado, IServiceScopeFactory fabrica, IOptions<ModeracionOpciones> opciones,
        ILogger<ResumenModeracionHostedService> log)
    {
        _estado = estado; _fabrica = fabrica; _opciones = opciones.Value; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (_opciones.ResumenCorreoHoras <= 0) return; // apagado
        var intervalo = TimeSpan.FromHours(_opciones.ResumenCorreoHoras);
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(intervalo, ct); }
            catch (OperationCanceledException) { break; }
            await EnviarAsync();
        }
    }

    /// <returns>Cuántos correos se encolaron.</returns>
    internal async Task<int> EnviarAsync()
    {
        try
        {
            using var scope = _fabrica.CreateScope();
            var sp = scope.ServiceProvider;
            var usuarios = sp.GetRequiredService<IUsuarioRepository>();
            var presencia = sp.GetRequiredService<IPresencia>();
            var correo = sp.GetRequiredService<ICorreoSaliente>();
            var front = sp.GetRequiredService<IOptions<UrlsOpciones>>().Value.Frontend.TrimEnd('/');
            var conteo = await sp.GetRequiredService<IPendientesModeracionRepository>()
                .ContarAsync(sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);

            var moderadores = await usuarios.ListarIdsModeradoresAsync();
            var enLinea = await presencia.EnLineaAsync(moderadores);
            var enviados = 0;
            foreach (var id in moderadores)
            {
                var u = await usuarios.ObtenerPorIdAsync(id);
                if (u is null) continue;
                var dto = PendientesModeracionDto.Desde(conteo, u.Rol == RolUsuarioEnum.SuperUsuario);
                if (dto.Total == 0)
                {
                    _estado.Olvidar(id);
                    continue;
                }
                // En línea ya ve las insignias del panel: esos números cuentan como avisados.
                if (!_estado.EsNuevo(id, dto.Firma()) || enLinea.Contains(id) || u.EstaEliminado || !u.CorreoVerificado) continue;
                correo.Encolar(PlantillaCorreo.Crear(u.Correo,
                    dto.Total == 1 ? "Hay 1 pendiente en moderación" : $"Hay {dto.Total} pendientes en moderación",
                    $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                    dto.Lineas().Prepend("Esto espera revisión en el panel de administración:"),
                    ("Abrir el panel", $"{front}/admin"),
                    "Recibes este resumen porque eres parte del equipo de moderación. Solo llega si hay pendientes nuevos y no estás en línea."));
                enviados++;
            }
            return enviados;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo preparar el resumen de moderación por correo");
            return 0;
        }
    }
}
