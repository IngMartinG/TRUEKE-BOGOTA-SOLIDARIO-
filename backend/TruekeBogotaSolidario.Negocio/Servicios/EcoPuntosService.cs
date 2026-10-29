using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IEcoPuntosService
{
    Task<EcoPuntosResumenDto> ResumenAsync(Guid actorId);
    /// <summary>Historial propio, del más reciente al más antiguo.</summary>
    Task<PaginaDto<MovimientoEcoPuntosDto>> MovimientosAsync(Guid actorId, int pagina, int tamano);
    PoliticaEcoPuntosDto ObtenerPolitica();
    /// <summary>Activa la prueba gratis de Premium (una vez por cuenta; exige la cuenta completa).</summary>
    Task<EcoPuntosResumenDto> ActivarPruebaPremiumAsync(Guid actorId);
    /// <summary>Invita y ganan los dos: el código propio (se crea la primera vez) y el contador de invitaciones logradas.</summary>
    Task<InvitacionDto> InvitacionAsync(Guid actorId);
}

public sealed class EcoPuntosService : IEcoPuntosService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ITransaccionRepository _transacciones;
    private readonly IMovimientoEcoPuntosRepository _movimientos;
    private readonly TimeProvider _reloj;
    private readonly FacturacionOpciones _facturacion;
    private readonly IUnidadDeTrabajo _uow;

    public EcoPuntosService(IUsuarioRepository usuarios, ITransaccionRepository transacciones, IMovimientoEcoPuntosRepository movimientos,
        TimeProvider reloj, IOptions<FacturacionOpciones> facturacion, IUnidadDeTrabajo uow)
    {
        _usuarios = usuarios; _transacciones = transacciones; _movimientos = movimientos; _reloj = reloj; _facturacion = facturacion.Value;
        _uow = uow;
    }

    public async Task<EcoPuntosResumenDto> ActivarPruebaPremiumAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(u, _reloj.GetUtcNow().UtcDateTime);
        u.ActivarPruebaPremium(_reloj.GetUtcNow().UtcDateTime);
        await _uow.GuardarCambiosAsync();
        return await ResumenAsync(actorId);
    }

    public async Task<InvitacionDto> InvitacionAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (u.CodigoInvitacion is null)
        {
            string codigo;
            var intentos = 0;
            do
            {
                if (++intentos > 10) throw new InvalidOperationException("No se pudo generar un código de invitación único.");
                codigo = CodigosInvitacion.Generar();
            } while (await _usuarios.ExisteCodigoInvitacionAsync(codigo));
            u.AsignarCodigoInvitacion(codigo);
            await _uow.GuardarCambiosAsync(); // el índice único cubre el caso (muy raro) de dos códigos iguales a la vez
        }
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var logradas = await _usuarios.ContarInvitacionesRecompensadasAsync(actorId, null);
        var enCiclo = await _usuarios.ContarInvitacionesRecompensadasAsync(actorId, ahora.AddDays(-PoliticaEcoPuntos.DiasCicloInvitaciones));
        return new InvitacionDto(u.CodigoInvitacion!, logradas, enCiclo, PoliticaEcoPuntos.MaxInvitacionesRecompensadasPorCiclo,
            PoliticaEcoPuntos.PuntosInvitacion, PoliticaEcoPuntos.DiasCicloInvitaciones);
    }

    public async Task<EcoPuntosResumenDto> ResumenAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var hoy = await _transacciones.ContarDesdeAsync(actorId, ahora.AddHours(-24));
        var plan = u.PlanEfectivo(ahora);
        var planVigente = plan != TipoCuenta.Individual;
        return new EcoPuntosResumenDto(u.SaldoEcoPuntos, u.Reputacion, hoy,
            Math.Max(0, PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia - hoy), plan.ToString(),
            planVigente ? u.FechaVencimientoSuscripcion : null, u.DestacadosGratisDisponibles(ahora),
            PoliticaEcoPuntos.MaxPublicacionesActivas(plan), PoliticaEcoPuntos.DescuentoPlan(plan),
            planVigente ? u.ProximaRecargaDestacadosVigente(ahora) : null, u.PuedeUsarPruebaPremium, u.PruebaPremiumVigente(ahora),
            Mapeos.ANivelDto(u));
    }

    public async Task<PaginaDto<MovimientoEcoPuntosDto>> MovimientosAsync(Guid actorId, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var (items, total) = await _movimientos.ListarPorUsuarioAsync(actorId, pagina, tamano);
        return new PaginaDto<MovimientoEcoPuntosDto>(
            items.Select(m => new MovimientoEcoPuntosDto(m.Id, m.Concepto.ToString(), m.Puntos, m.Detalle, m.ReferenciaId, m.SaldoResultante, m.FechaUtc)).ToList(),
            total, pagina, tamano);
    }

    /// <summary>Leída en vivo de PoliticaEcoPuntos (única fuente de verdad); nada se hardcodea aquí.</summary>
    public PoliticaEcoPuntosDto ObtenerPolitica()
    {
        var ganancias = new[] { ModoTransaccion.Donacion, ModoTransaccion.Trueke, ModoTransaccion.Compra }
            .Select(m => new GananciaDto(m.ToString(), PoliticaEcoPuntos.PuntosPorModo(m), PoliticaEcoPuntos.ReputacionPorModo(m))).ToList();
        return new PoliticaEcoPuntosDto(PoliticaEcoPuntos.PuntosBienvenida, ganancias, PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia,
            PoliticaEcoPuntos.ReputacionMaxima, PoliticaEcoPuntos.PrecioDestacarCop, PoliticaEcoPuntos.DuracionDestacadoDias,
            PoliticaEcoPuntos.PuntosParaCubrir(PoliticaEcoPuntos.PrecioDestacarCop), PoliticaEcoPuntos.PrecioVerificarCop,
            PoliticaEcoPuntos.PuntosParaCubrir(PoliticaEcoPuntos.PrecioVerificarCop),
            PoliticaEcoPuntos.PrecioPremiumCop, PoliticaEcoPuntos.DestacadosGratisPremium, PoliticaEcoPuntos.DescuentoPremiumPorcentaje,
            PoliticaEcoPuntos.PrecioEmpresaCop, PoliticaEcoPuntos.DuracionSuscripcionDias, PoliticaEcoPuntos.CopPorEcoPunto,
            PoliticaEcoPuntos.RecargaMinimaCop, PoliticaEcoPuntos.RecargaMaximaCop,
            PoliticaEcoPuntos.PuntosImpulsar, PoliticaEcoPuntos.HorasEntreImpulsos, PoliticaEcoPuntos.DestacadosGratisEmpresa,
            PoliticaEcoPuntos.DescuentoEmpresaPorcentaje, PoliticaEcoPuntos.MaxPublicacionesIndividual, PoliticaEcoPuntos.MaxPublicacionesPremium,
            PoliticaEcoPuntos.MaxPublicacionesEmpresa, PoliticaEcoPuntos.MaxVentasActivasSinIdentificar,
            PoliticaEcoPuntos.DiasEntreTransaccionesConPuntosMismaPareja, _facturacion.IvaEfectivo > 0, _facturacion.IvaEfectivo,
            PoliticaEcoPuntos.DiasRecordatorioVencimiento, PoliticaEcoPuntos.DiasCicloDestacadosGratis, PoliticaEcoPuntos.MinimoCobroCop,
            PoliticaEcoPuntos.DiasPruebaPremium, PoliticaEcoPuntos.DestacadosGratisPruebaPremium,
            PoliticaEcoPuntos.PrecioPremiumAnualCop, PoliticaEcoPuntos.PrecioEmpresaAnualCop, PoliticaEcoPuntos.DuracionSuscripcionAnualDias,
            PoliticaEcoPuntos.PaquetesRecarga
                .Select(p => new PaqueteRecargaDto(p.MontoCop, p.MontoCop / PoliticaEcoPuntos.CopPorEcoPunto, p.Bono)).ToList(),
            PoliticaEcoPuntos.PuntosInvitacion, PoliticaEcoPuntos.MaxInvitacionesRecompensadasPorCiclo, PoliticaEcoPuntos.DiasCicloInvitaciones,
            PoliticaEcoPuntos.Niveles.Select(n => new NivelPoliticaDto(n.Nivel.ToString(), n.PuntosMinimos)).ToList(),
            PoliticaEcoPuntos.PuntosPrimeraPublicacion, PoliticaEcoPuntos.HorasPrimeraPublicacionActiva, PoliticaEcoPuntos.PuntosPerfilCompleto,
            PoliticaEcoPuntos.PuntosCalificacionCincoEstrellas, PoliticaEcoPuntos.PuntosCalificacionCuatroEstrellas,
            PoliticaEcoPuntos.DiasEntreCalificacionesMismaPareja, PoliticaEcoPuntos.PuntosDenunciaConfirmada,
            PoliticaEcoPuntos.MaxDenunciasRecompensadasPorCiclo, PoliticaEcoPuntos.DiasCicloDenuncias);
    }
}
