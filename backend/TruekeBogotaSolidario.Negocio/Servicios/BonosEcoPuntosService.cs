using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Bonos de Eco-Puntos de una sola vez que no nacen de una petición: la primera publicación que cumple 24 h activa. También
/// entrega el de perfil completo a las cuentas que ya lo tenían antes de que existiera el bono. Lo llama el mantenimiento periódico.
/// </summary>
public interface IBonosEcoPuntosService
{
    Task<IReadOnlyList<Guid>> ListarPendientesAsync(int maximo);
    /// <summary>Entrega los bonos que la persona ya ganó y aún no recibió (idempotente: una marca en la cuenta + RowVersion).</summary>
    Task OtorgarPendientesAsync(Guid usuarioId);
}

public sealed class BonosEcoPuntosService : IBonosEcoPuntosService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _publicaciones;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<BonosEcoPuntosService> _log;

    public BonosEcoPuntosService(IUsuarioRepository usuarios, IPublicacionRepository publicaciones, IUnidadDeTrabajo uow,
        INotificador notificador, TimeProvider reloj, ILogger<BonosEcoPuntosService> log)
    {
        _usuarios = usuarios; _publicaciones = publicaciones; _uow = uow; _notificador = notificador; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;
    private DateTime PublicadaHasta => Ahora.AddHours(-PoliticaEcoPuntos.HorasPrimeraPublicacionActiva);

    public Task<IReadOnlyList<Guid>> ListarPendientesAsync(int maximo)
        => _usuarios.ListarConBonosPendientesAsync(PublicadaHasta, Ahora, maximo);

    public async Task OtorgarPendientesAsync(Guid usuarioId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (u is null || u.EstaEliminado || u.SuspensionVigente(Ahora)) return;
        var avisosPuntos = new AvisosEcoPuntos();
        avisosPuntos.Observar(u);
        var perfil = u.EvaluarPerfilCompleto();
        var primera = false;
        if (!u.BonoPrimeraPublicacionOtorgado && await _publicaciones.PrimeraActivaDesdeAsync(u.Id, PublicadaHasta) is { } pub)
            primera = u.RecibirBonoPrimeraPublicacion(pub.Id, pub.Titulo);
        if (!perfil && !primera) return;
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Bonos de Eco-Puntos entregados a {UsuarioId}: perfil {Perfil}, primera publicación {Primera}", u.Id, perfil, primera);
        await avisosPuntos.EnviarAsync(_notificador);
    }
}
