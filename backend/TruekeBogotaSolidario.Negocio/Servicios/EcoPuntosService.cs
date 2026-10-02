using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IEcoPuntosService
{
    Task<EcoPuntosResumenDto> ResumenAsync(Guid actorId);
    PoliticaEcoPuntosDto ObtenerPolitica();
}

public sealed class EcoPuntosService : IEcoPuntosService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ITransaccionRepository _transacciones;
    private readonly TimeProvider _reloj;

    public EcoPuntosService(IUsuarioRepository usuarios, ITransaccionRepository transacciones, TimeProvider reloj)
    {
        _usuarios = usuarios; _transacciones = transacciones; _reloj = reloj;
    }

    public async Task<EcoPuntosResumenDto> ResumenAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var hoy = await _transacciones.ContarDesdeAsync(actorId, ahora.AddHours(-24));
        var planVigente = u.PremiumVigente(ahora) || u.EmpresaVigente(ahora);
        return new EcoPuntosResumenDto(u.SaldoEcoPuntos, u.Reputacion, hoy,
            Math.Max(0, PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia - hoy), Mapeos.TipoCuentaEfectivo(u, ahora),
            planVigente ? u.FechaVencimientoSuscripcion : null, u.PremiumVigente(ahora) ? u.DestacadosGratisRestantes : 0);
    }

    /// <summary>Leída en vivo de PoliticaEcoPuntos (única fuente de verdad); nada se hardcodea aquí.</summary>
    public PoliticaEcoPuntosDto ObtenerPolitica()
    {
        var ganancias = new[] { ModoTransaccion.Donacion, ModoTransaccion.Trueke, ModoTransaccion.Compra }
            .Select(m => new GananciaDto(m.ToString(), PoliticaEcoPuntos.PuntosPorModo(m), PoliticaEcoPuntos.ReputacionPorModo(m))).ToList();
        static List<EscalonDto> Mapear(IEnumerable<EscalonDescuento> e) => e.OrderBy(x => x.PuntosMinimos).Select(x => new EscalonDto(x.PuntosMinimos, x.DescuentoPorcentaje)).ToList();
        return new PoliticaEcoPuntosDto(PoliticaEcoPuntos.PuntosBienvenida, ganancias, PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia,
            PoliticaEcoPuntos.ReputacionMaxima, PoliticaEcoPuntos.PrecioDestacarCop, PoliticaEcoPuntos.DuracionDestacadoDias,
            Mapear(PoliticaEcoPuntos.EscalonesDestacar), PoliticaEcoPuntos.PrecioVerificarCop, Mapear(PoliticaEcoPuntos.EscalonesVerificar),
            PoliticaEcoPuntos.PrecioPremiumCop, PoliticaEcoPuntos.DestacadosGratisPremium, PoliticaEcoPuntos.DescuentoPremiumPorcentaje,
            PoliticaEcoPuntos.PrecioEmpresaCop, PoliticaEcoPuntos.DuracionSuscripcionDias, PoliticaEcoPuntos.CopPorEcoPunto,
            PoliticaEcoPuntos.RecargaMinimaCop, PoliticaEcoPuntos.RecargaMaximaCop);
    }
}
