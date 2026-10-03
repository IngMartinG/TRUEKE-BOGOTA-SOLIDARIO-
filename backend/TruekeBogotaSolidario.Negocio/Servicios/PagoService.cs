using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Pagos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IPagoService
{
    Task<CotizacionDto?> CotizarAsync(Guid actorId, ConceptoPagoDto concepto);
    Task<PagoIniciadoDto> IniciarAsync(Guid actorId, IniciarPagoRequest r, CancellationToken ct = default);
    Task<PagoEstadoDto> ObtenerEstadoAsync(Guid actorId, string referencia, CancellationToken ct = default);
    Task DestacarGratisAsync(Guid actorId, Guid publicacionId);
    /// <summary>Webhook de Wompi. Firma inválida → AutenticacionException. Idempotente.</summary>
    Task ProcesarEventoWompiAsync(string cuerpoJson, CancellationToken ct = default);
    /// <summary>Solo con proveedor Simulado (desarrollo/pruebas).</summary>
    Task<PagoEstadoDto> SimularResultadoAsync(Guid actorId, string referencia, bool aprobado);
    Task<IReadOnlyList<Guid>> ListarPagosParaReconciliarAsync(int maximo);
    Task ReconciliarAsync(Guid pagoId, CancellationToken ct = default);
}

public sealed class PagoService : IPagoService
{
    private const string Moneda = "COP";

    private readonly IPagoRepository _pagos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _pubs;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IProveedorPagos _proveedor;
    private readonly PagosOpciones _opciones;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly IAlmacenArchivos _almacen;
    private readonly IFacturaRepository _facturas;
    private readonly FacturacionOpciones _facturacion;
    private readonly ILogger<PagoService> _log;

    public PagoService(IPagoRepository pagos, IUsuarioRepository usuarios, IPublicacionRepository pubs, IUnidadDeTrabajo uow,
        IProveedorPagos proveedor, IOptions<PagosOpciones> opciones, IOptions<UrlsOpciones> urls, TimeProvider reloj,
        INotificador notificador, IAlmacenArchivos almacen, IFacturaRepository facturas, IOptions<FacturacionOpciones> facturacion,
        ILogger<PagoService> log)
    {
        _pagos = pagos; _usuarios = usuarios; _pubs = pubs; _uow = uow; _proveedor = proveedor;
        _opciones = opciones.Value; _urls = urls.Value; _reloj = reloj; _notificador = notificador; _almacen = almacen;
        _facturas = facturas; _facturacion = facturacion.Value; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    /// <summary>Confirma el resultado de la pasarela y, SOLO después de guardar, avisa al usuario (y al equipo si hay que revisar).</summary>
    private async Task GuardarYNotificarAsync(Pago pago)
    {
        await _uow.GuardarCambiosAsync();
        var (tipo, mensaje) = pago.Estado switch
        {
            EstadoPago.Aprobado => (TiposNotificacion.PagoAprobado, $"Tu pago de {pago.MontoCop:N0} COP ({pago.Concepto}) fue aprobado."),
            EstadoPago.Rechazado => (TiposNotificacion.PagoRechazado, $"Tu pago ({pago.Concepto}) fue rechazado. Los Eco-Puntos reservados se devolvieron."),
            EstadoPago.RequiereRevision => (TiposNotificacion.PagoEnRevision, $"Tu pago ({pago.Concepto}) está en revisión por nuestro equipo."),
            _ => (null, null)
        };
        if (tipo is not null)
            await _notificador.NotificarAsync(pago.UsuarioId, tipo, mensaje!);

        // Dinero cobrado sin beneficio aplicado: el equipo debe reembolsar o aplicar a mano. Se avisa a los moderadores.
        if (pago.Estado == EstadoPago.RequiereRevision)
            foreach (var moderador in await _usuarios.ListarIdsModeradoresAsync())
                await _notificador.NotificarAsync(moderador, TiposNotificacion.AlertaPagoEnRevision,
                    $"Pago {pago.Referencia} de {pago.MontoCop:N0} COP requiere revisión: {pago.NotaInterna}");
    }

    private decimal IvaIncluido(int totalCop)
    {
        var iva = _facturacion.IvaEfectivo;
        return iva <= 0 ? 0 : totalCop - Math.Round(totalCop / (1 + iva / 100m), 2, MidpointRounding.AwayFromZero);
    }

    private CotizacionDto ADto(CotizacionBeneficio c)
        => new(c.PrecioBaseCop, c.DescuentoPorcentaje, c.PuntosACanjear, c.TotalCop, IvaIncluido(c.TotalCop));

    private async Task<Usuario> CargarUsuarioAsync(Guid id)
        => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    // ------------------------------------------------------------------ Cotizar
    public async Task<CotizacionDto?> CotizarAsync(Guid actorId, ConceptoPagoDto concepto)
    {
        var u = await CargarUsuarioAsync(actorId);
        var c = CotizarInterno(u, (ConceptoPago)(int)concepto, Ahora);
        return c is null ? null : ADto(c);
    }

    /// <summary>Destacar y Verificar admiten descuento por Eco-Puntos y por plan (Premium 15 %, Empresa 20 %).</summary>
    private static CotizacionBeneficio? CotizarInterno(Usuario u, ConceptoPago concepto, DateTime ahora)
    {
        var descuentoPlan = PoliticaEcoPuntos.DescuentoPlan(u.PlanEfectivo(ahora));
        return concepto switch
        {
            ConceptoPago.Destacar => PoliticaEcoPuntos.Cotizar(PoliticaEcoPuntos.PrecioDestacarCop, PoliticaEcoPuntos.EscalonesDestacar, u.SaldoEcoPuntos, descuentoPlan),
            ConceptoPago.Verificar => PoliticaEcoPuntos.Cotizar(PoliticaEcoPuntos.PrecioVerificarCop, PoliticaEcoPuntos.EscalonesVerificar, u.SaldoEcoPuntos, descuentoPlan),
            _ => null
        };
    }

    // ------------------------------------------------------------------ Iniciar (VALIDAR primero, cobrar después)
    public async Task<PagoIniciadoDto> IniciarAsync(Guid actorId, IniciarPagoRequest r, CancellationToken ct = default)
    {
        var ahora = Ahora;
        var u = await CargarUsuarioAsync(actorId);
        Guardas.ExigirCorreoVerificado(u);
        var concepto = (ConceptoPago)(int)r.Concepto;
        if (!Enum.IsDefined(concepto)) throw new ReglaDeNegocioException("Concepto de pago no válido.");

        if (await _pagos.ContarPendientesAsync(actorId, ahora.AddHours(-1)) >= Limites.MaxPagosPendientesPorHora)
            throw new ReglaDeNegocioException("Tienes demasiados pagos pendientes. Complétalos o espera a que expiren.");
        if (concepto != ConceptoPago.Recarga && await _pagos.ExistePendienteAsync(actorId, concepto, r.PublicacionId))
            throw new ReglaDeNegocioException("Ya tienes un pago pendiente de este tipo. Complétalo o espera a que expire.");

        Guid? publicacionId = null;
        string? documento = null;
        int monto;
        CotizacionBeneficio? cotizacion = null;

        switch (concepto)
        {
            case ConceptoPago.Destacar:
            {
                if (r.PublicacionId is null) throw new ReglaDeNegocioException("Indica la publicación a destacar.");
                var pub = await _pubs.ObtenerPorIdAsync(r.PublicacionId.Value);
                if (pub is null || pub.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");
                pub.ValidarPuedeDestacarse(ahora);
                publicacionId = pub.Id;
                cotizacion = CotizarInterno(u, concepto, ahora)!;
                monto = cotizacion.TotalCop;
                break;
            }
            case ConceptoPago.Verificar:
            {
                if (r.DocumentoUrl is null) throw new ReglaDeNegocioException("Indica el enlace del documento de identidad.");
                documento = r.DocumentoUrl;
                if (_almacen.Habilitado) documento = await _almacen.ValidarArchivoPropioAsync(r.DocumentoUrl, actorId, TipoArchivoDto.Documento, ct);
                else ValidadorUrls.ExigirHostPermitido(r.DocumentoUrl, _urls.HostsPermitidosDocumentos, "El documento");
                u.ValidarPuedeSolicitarVerificacion(documento);
                cotizacion = CotizarInterno(u, concepto, ahora)!;
                monto = cotizacion.TotalCop;
                break;
            }
            case ConceptoPago.Premium:
                u.ValidarPuedeSuscribirse(TipoCuenta.Premium, ahora);
                monto = PoliticaEcoPuntos.PrecioPremiumCop;
                break;
            case ConceptoPago.Empresa:
                u.ValidarPuedeSuscribirse(TipoCuenta.Empresa, ahora);
                monto = PoliticaEcoPuntos.PrecioEmpresaCop;
                break;
            case ConceptoPago.Recarga:
                if (r.MontoRecargaCop is null) throw new ReglaDeNegocioException("Indica el monto de la recarga.");
                monto = r.MontoRecargaCop.Value;
                if (monto < PoliticaEcoPuntos.RecargaMinimaCop || monto > PoliticaEcoPuntos.RecargaMaximaCop)
                    throw new ReglaDeNegocioException($"La recarga debe estar entre {PoliticaEcoPuntos.RecargaMinimaCop:N0} y {PoliticaEcoPuntos.RecargaMaximaCop:N0} COP.");
                if (monto % PoliticaEcoPuntos.CopPorEcoPunto != 0)
                    throw new ReglaDeNegocioException($"El monto debe ser múltiplo de {PoliticaEcoPuntos.CopPorEcoPunto} COP.");
                break;
            default:
                throw new ReglaDeNegocioException("Concepto de pago no válido.");
        }

        var puntos = cotizacion?.PuntosACanjear ?? 0;
        if (puntos > 0) u.DebitarEcoPuntos(puntos); // se reservan ya; si el pago falla o expira se devuelven

        var referencia = "TRK-" + Guid.NewGuid().ToString("N");
        var pago = new Pago(actorId, concepto, monto, referencia, puntos, publicacionId, documento, ahora);
        _pagos.Agregar(pago);
        await _uow.GuardarCambiosAsync();

        var expira = ahora.AddMinutes(_opciones.MinutosParaExpirarPendientes);
        return new PagoIniciadoDto(referencia, concepto.ToString(), monto, pago.MontoEnCentavos, Moneda, _proveedor.Nombre,
            _proveedor.LlavePublica, _proveedor.FirmaIntegridad(referencia, pago.MontoEnCentavos, Moneda),
            cotizacion is null ? null : ADto(cotizacion), expira);
    }

    // ------------------------------------------------------------------ Estado (solo el dueño; reconcilia con la pasarela si sigue pendiente)
    public async Task<PagoEstadoDto> ObtenerEstadoAsync(Guid actorId, string referencia, CancellationToken ct = default)
    {
        var pago = await _pagos.ObtenerPorReferenciaAsync(referencia);
        if (pago is null || pago.UsuarioId != actorId) throw new NoEncontradoException("Pago no encontrado.");

        if (pago.Estado == EstadoPago.Pendiente && !_opciones.EsSimulado)
        {
            try
            {
                var tx = await _proveedor.ConsultarPorReferenciaAsync(pago.Referencia, ct);
                if (tx is not null && await ResolverAsync(pago, tx)) await GuardarYNotificarAsync(pago);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.LogWarning(ex, "No se pudo consultar la pasarela para {Referencia}", pago.Referencia);
            }
        }
        return ADto(pago);
    }

    // ------------------------------------------------------------------ Destacado gratis (Premium)
    public async Task DestacarGratisAsync(Guid actorId, Guid publicacionId)
    {
        var ahora = Ahora;
        var u = await CargarUsuarioAsync(actorId);
        Guardas.ExigirCorreoVerificado(u);
        var pub = await _pubs.ObtenerPorIdAsync(publicacionId);
        if (pub is null || pub.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");
        pub.ValidarPuedeDestacarse(ahora);   // validar ANTES de consumir el beneficio
        u.UsarDestacadoGratis(ahora);
        pub.MarcarDestacada(ahora);
        await _uow.GuardarCambiosAsync();
    }

    // ------------------------------------------------------------------ Webhook
    public async Task ProcesarEventoWompiAsync(string cuerpoJson, CancellationToken ct = default)
    {
        if (_opciones.EsSimulado) throw new AccesoDenegadoException("Webhook no disponible en modo simulado.");
        var evento = VerificadorEventosWompi.Verificar(cuerpoJson, _opciones.Wompi.SecretoEventos)
                     ?? throw new AutenticacionException("Firma inválida.");
        if (evento.Evento != "transaction.updated") return;

        // El evento solo dispara la consulta: la verdad (estado, monto, referencia) se pide a la pasarela.
        var tx = await _proveedor.ConsultarPorIdAsync(evento.TransaccionId, ct);
        if (tx is null) { _log.LogWarning("Evento de pago para transacción desconocida"); return; }

        var pago = await _pagos.ObtenerPorReferenciaAsync(tx.Referencia);
        if (pago is null) { _log.LogWarning("Evento de pago con referencia desconocida"); return; }

        if (await ResolverAsync(pago, tx)) await GuardarYNotificarAsync(pago);
    }

    // ------------------------------------------------------------------ Simulación (solo dev/pruebas)
    public async Task<PagoEstadoDto> SimularResultadoAsync(Guid actorId, string referencia, bool aprobado)
    {
        if (!_opciones.EsSimulado) throw new AccesoDenegadoException();
        var pago = await _pagos.ObtenerPorReferenciaAsync(referencia);
        if (pago is null || pago.UsuarioId != actorId) throw new NoEncontradoException("Pago no encontrado.");
        var tx = new TransaccionProveedor("SIM-" + pago.Id.ToString("N"), pago.Referencia, pago.MontoEnCentavos, Moneda, aprobado ? "APPROVED" : "DECLINED");
        if (await ResolverAsync(pago, tx)) await GuardarYNotificarAsync(pago);
        return ADto(pago);
    }

    // ------------------------------------------------------------------ Reconciliación / expiración (tarea en segundo plano)
    public async Task<IReadOnlyList<Guid>> ListarPagosParaReconciliarAsync(int maximo)
    {
        var limite = Ahora.AddMinutes(-_opciones.MinutosParaExpirarPendientes);
        var lista = await _pagos.ListarPendientesAnterioresAAsync(limite, maximo);
        return lista.Select(p => p.Id).ToList();
    }

    public async Task ReconciliarAsync(Guid pagoId, CancellationToken ct = default)
    {
        var limite = Ahora.AddMinutes(-_opciones.MinutosParaExpirarPendientes);
        var candidatos = await _pagos.ListarPendientesAnterioresAAsync(limite, 500);
        var pago = candidatos.FirstOrDefault(p => p.Id == pagoId);
        if (pago is null || pago.Estado != EstadoPago.Pendiente) return;

        if (!_opciones.EsSimulado)
        {
            var tx = await _proveedor.ConsultarPorReferenciaAsync(pago.Referencia, ct); // puede lanzar: se reintenta en el próximo ciclo
            if (tx is not null)
            {
                if (await ResolverAsync(pago, tx)) await GuardarYNotificarAsync(pago);
                return; // la pasarela conoce el pago (resuelto, pendiente o inconsistente): nunca expirar a ciegas
            }
        }

        var u = await CargarUsuarioAsync(pago.UsuarioId);
        DevolverPuntos(pago, u);
        pago.Expirar(Ahora);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Pago {Referencia} expirado", pago.Referencia);
    }

    // ------------------------------------------------------------------ Núcleo: aplicar el resultado de la pasarela (idempotente)
    /// <returns>true si hubo cambios que guardar.</returns>
    private async Task<bool> ResolverAsync(Pago pago, TransaccionProveedor tx)
    {
        var ahora = Ahora;
        if (tx.Referencia != pago.Referencia || tx.MontoCentavos != pago.MontoEnCentavos || tx.Moneda != Moneda)
        {
            _log.LogError("Transacción {TxId} no coincide con el pago {Referencia} (monto/moneda/referencia). Ignorada.", tx.Id, pago.Referencia);
            return false;
        }

        switch (tx.Estado)
        {
            case "APPROVED":
                switch (pago.Estado)
                {
                    case EstadoPago.Pendiente:
                        return await AprobarAsync(pago, tx, ahora);
                    case EstadoPago.Expirado:
                        pago.MarcarRequiereRevision("Aprobado por la pasarela después de expirar: requiere reembolso manual.", tx.Id, ahora);
                        _log.LogError("Pago {Referencia} aprobado tras expirar: reembolso manual requerido", pago.Referencia);
                        return true;
                    default:
                        return false; // ya resuelto: webhook repetido
                }
            case "DECLINED" or "ERROR" or "VOIDED":
                if (pago.Estado != EstadoPago.Pendiente) return false;
                DevolverPuntos(pago, await CargarUsuarioAsync(pago.UsuarioId));
                pago.Rechazar(tx.Id, ahora);
                return true;
            default:
                return false; // PENDING u otro: esperar
        }
    }

    private async Task<bool> AprobarAsync(Pago pago, TransaccionProveedor tx, DateTime ahora)
    {
        var u = await CargarUsuarioAsync(pago.UsuarioId);
        try
        {
            await AplicarBeneficioAsync(pago, u, ahora);
            pago.Aprobar(tx.Id, ahora);
            // La factura nace en la MISMA transacción que aprueba el pago: ninguna venta queda sin facturar.
            if (await _facturas.ObtenerPorPagoAsync(pago.Id) is null)
                _facturas.Agregar(new Factura(pago, u, Mapeos.DescripcionConcepto(pago.Concepto), _facturacion.IvaEfectivo, ahora));
        }
        catch (ReglaDeNegocioException ex)
        {
            // Se cobró pero el beneficio ya no es aplicable (p. ej. la publicación cambió de estado): no se pierde nada en silencio.
            DevolverPuntos(pago, u);
            pago.MarcarRequiereRevision("Beneficio no aplicable: " + ex.Message, tx.Id, ahora);
            _log.LogError("Pago {Referencia} cobrado pero no aplicable: {Motivo}", pago.Referencia, ex.Message);
        }
        return true;
    }

    private async Task AplicarBeneficioAsync(Pago pago, Usuario u, DateTime ahora)
    {
        switch (pago.Concepto)
        {
            case ConceptoPago.Destacar:
            {
                var pub = pago.PublicacionId is null ? null : await _pubs.ObtenerPorIdAsync(pago.PublicacionId.Value);
                if (pub is null || pub.PropietarioId != u.Id) throw new ReglaDeNegocioException("La publicación ya no existe.");
                pub.MarcarDestacada(ahora);
                break;
            }
            case ConceptoPago.Verificar:
                u.SolicitarVerificacion(pago.DocumentoUrl ?? "");
                break;
            case ConceptoPago.Premium:
                u.ActivarPremium(ahora);
                break;
            case ConceptoPago.Empresa:
                u.ActivarEmpresa(ahora);
                break;
            case ConceptoPago.Recarga:
                u.AcreditarEcoPuntos(pago.MontoCop / PoliticaEcoPuntos.CopPorEcoPunto);
                break;
            default:
                throw new ReglaDeNegocioException("Concepto de pago no válido.");
        }
    }

    private static void DevolverPuntos(Pago pago, Usuario u)
    {
        if (pago.PuntosCanjeados > 0) u.AcreditarEcoPuntos(pago.PuntosCanjeados);
    }

    private static PagoEstadoDto ADto(Pago p)
        => new(p.Referencia, p.Concepto.ToString(), p.MontoCop, p.Estado.ToString(), p.FechaUtc, p.FechaResolucionUtc);
}
