using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Ciclo: Pendiente → (Aceptada → Completada | NoConcretada) | Rechazada | Cancelada.
/// Mientras está Pendiente se negocia por turnos: quien recibe la propuesta vigente la acepta, la contraoferta (precio en
/// una Compra, objetos en un Trueke) o se retira (el dueño rechaza, quien solicita cancela).
/// Aceptar NO completa el intercambio: las partes coordinan por chat y ambas confirman la entrega. Los Eco-Puntos se
/// otorgan al completarse (cuando las dos confirman, o automáticamente si una confirmó y pasan los días de gracia).
/// </summary>
public class Solicitud
{
    /// <summary>Objetos que se pueden ofrecer a cambio de uno en un trueke (decisión del dueño).</summary>
    public const int MaxObjetosOfrecidos = 3;
    /// <summary>Contraofertas por solicitud entre las dos partes: después solo queda aceptar o retirarse.</summary>
    public const int MaxContraofertas = 5;

    private readonly List<SolicitudObjeto> _ofrecidas = new();

    private Solicitud() { Mensaje = ""; } // EF Core

    /// <param name="ofrecidas">Trueke: publicaciones propias que se ofrecen a cambio (opcional, hasta <see cref="MaxObjetosOfrecidos"/>).</param>
    /// <param name="precioPropuestoCop">Compra: precio que propone quien solicita (opcional; sin él, vale el publicado).</param>
    public Solicitud(Guid publicacionId, Guid solicitanteId, string mensaje, IReadOnlyList<Guid>? ofrecidas = null, decimal? precioPropuestoCop = null)
    {
        mensaje = (mensaje ?? "").Trim();
        if (mensaje.Length is < 1 or > 500) throw new ReglaDeNegocioException("El mensaje es obligatorio (máximo 500 caracteres).");
        Id = Guid.NewGuid();
        PublicacionId = publicacionId;
        SolicitanteId = solicitanteId;
        Mensaje = mensaje;
        FechaSolicitud = DateTime.UtcNow;
        Estado = EstadoSolicitud.Pendiente;
        TurnoDuenio = true;
        PonerOfrecidas(ofrecidas ?? Array.Empty<Guid>());
        PrecioPropuestoCop = precioPropuestoCop;
    }

    public Guid Id { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Publicacion? Publicacion { get; private set; }
    /// <summary>
    /// Trueke: publicaciones de quien solicita en la propuesta vigente. Al aceptar, también quedan reservadas; al completarse,
    /// también se intercambian; si no se concreta, vuelven al catálogo.
    /// </summary>
    public IReadOnlyList<SolicitudObjeto> Ofrecidas => _ofrecidas;
    /// <summary>Compra: precio de la propuesta vigente (null = el publicado). Se paga en persona, como el publicado.</summary>
    public decimal? PrecioPropuestoCop { get; private set; }
    /// <summary>A quién le toca responder la propuesta vigente: al dueño (al crearse) o a quien solicita (tras una contraoferta del dueño).</summary>
    public bool TurnoDuenio { get; private set; }
    public int Contraofertas { get; private set; }
    public Guid SolicitanteId { get; private set; }
    public Usuario? Solicitante { get; private set; }
    public DateTime FechaSolicitud { get; private set; }
    public string Mensaje { get; private set; }
    public EstadoSolicitud Estado { get; private set; }
    /// <summary>Motivo de rechazo o de "no concretada".</summary>
    public string? MotivoRechazo { get; private set; }
    public DateTime? FechaAceptacionUtc { get; private set; }
    public DateTime? ConfirmadaPorDuenioUtc { get; private set; }
    public DateTime? ConfirmadaPorSolicitanteUtc { get; private set; }
    public DateTime? FechaCierreUtc { get; private set; }
    /// <summary>Aviso de "¿ya se hizo la entrega?" a quien no había confirmado (una sola vez).</summary>
    public DateTime? RecordatorioEntregaUtc { get; private set; }
    /// <summary>Aviso de "mañana se cierra solo" a quien no había confirmado (una sola vez).</summary>
    public DateTime? RecordatorioCierreUtc { get; private set; }
    public uint RowVersion { get; private set; }

    public bool EstaEnCurso => Estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Aceptada;

    /// <summary>Acepta la propuesta vigente: solo quien debe responderla (el dueño acepta la solicitud; quien solicita, la contraoferta).</summary>
    public void Aceptar(bool esDuenio, DateTime ahoraUtc)
    {
        ExigirPendiente();
        ExigirTurno(esDuenio);
        Estado = EstadoSolicitud.Aceptada;
        FechaAceptacionUtc = ahoraUtc;
    }

    /// <summary>Reemplaza la propuesta vigente y pasa el turno a la otra parte.</summary>
    public void Contraofertar(bool esDuenio, IReadOnlyList<Guid> ofrecidas, decimal? precioPropuestoCop)
    {
        ExigirPendiente();
        ExigirTurno(esDuenio);
        if (Contraofertas >= MaxContraofertas)
            throw new ReglaDeNegocioException($"Ya hicieron {MaxContraofertas} contraofertas: acepta la propuesta o retírate.");
        PonerOfrecidas(ofrecidas);
        PrecioPropuestoCop = precioPropuestoCop;
        TurnoDuenio = !esDuenio;
        Contraofertas++;
    }

    /// <summary>Deja la lista igual a <paramref name="ids"/>: conserva las que siguen (no se borran y se vuelven a crear).</summary>
    private void PonerOfrecidas(IReadOnlyList<Guid> ids)
    {
        if (ids.Count > MaxObjetosOfrecidos) throw new ReglaDeNegocioException($"Puedes ofrecer hasta {MaxObjetosOfrecidos} objetos.");
        if (ids.Distinct().Count() != ids.Count) throw new ReglaDeNegocioException("Elegiste el mismo objeto dos veces.");
        _ofrecidas.RemoveAll(o => !ids.Contains(o.PublicacionId));
        for (var i = 0; i < ids.Count; i++)
        {
            var existente = _ofrecidas.FirstOrDefault(o => o.PublicacionId == ids[i]);
            if (existente is null) _ofrecidas.Add(new SolicitudObjeto(Id, ids[i], i));
            else existente.Orden = i;
        }
    }

    private void ExigirTurno(bool esDuenio)
    {
        if (esDuenio != TurnoDuenio)
            throw new ReglaDeNegocioException(esDuenio ? "Le enviaste una contraoferta: espera su respuesta." : "Espera la respuesta del dueño.");
    }

    public void Rechazar(string motivo)
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Rechazada;
        MotivoRechazo = Recortar(motivo);
        FechaCierreUtc = DateTime.UtcNow;
    }

    /// <param name="motivo">Si la cancela el sistema (p. ej. el objeto ofrecido dejó de estar disponible).</param>
    public void Cancelar(string? motivo = null)
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Cancelada;
        MotivoRechazo = Recortar(motivo);
        FechaCierreUtc = DateTime.UtcNow;
    }

    /// <returns>true si con esta confirmación ya confirmaron ambas partes.</returns>
    public bool ConfirmarEntrega(bool esDuenio, DateTime ahoraUtc)
    {
        if (Estado != EstadoSolicitud.Aceptada) throw new ReglaDeNegocioException("Solo se confirma la entrega de una solicitud aceptada.");
        if (esDuenio)
        {
            if (ConfirmadaPorDuenioUtc is not null) throw new ReglaDeNegocioException("Ya confirmaste la entrega.");
            ConfirmadaPorDuenioUtc = ahoraUtc;
        }
        else
        {
            if (ConfirmadaPorSolicitanteUtc is not null) throw new ReglaDeNegocioException("Ya confirmaste la entrega.");
            ConfirmadaPorSolicitanteUtc = ahoraUtc;
        }
        return ConfirmadaPorDuenioUtc is not null && ConfirmadaPorSolicitanteUtc is not null;
    }

    public void Completar(DateTime ahoraUtc)
    {
        if (Estado != EstadoSolicitud.Aceptada) throw new ReglaDeNegocioException("La solicitud no está aceptada.");
        Estado = EstadoSolicitud.Completada;
        FechaCierreUtc = ahoraUtc;
    }

    /// <summary>El intercambio acordado no ocurrió (cualquiera de las partes lo informa, o vence sin confirmaciones).</summary>
    public void MarcarNoConcretada(string motivo, DateTime ahoraUtc)
    {
        if (Estado != EstadoSolicitud.Aceptada) throw new ReglaDeNegocioException("Solo una solicitud aceptada puede marcarse como no concretada.");
        Estado = EstadoSolicitud.NoConcretada;
        MotivoRechazo = Recortar(motivo);
        FechaCierreUtc = ahoraUtc;
    }

    public void MarcarRecordatorioEntrega(DateTime ahoraUtc) => RecordatorioEntregaUtc ??= ahoraUtc;

    /// <summary>El de cierre reemplaza al de entrega si aún no había salido: nadie recibe los dos el mismo día.</summary>
    public void MarcarRecordatorioCierre(DateTime ahoraUtc)
    {
        RecordatorioCierreUtc ??= ahoraUtc;
        RecordatorioEntregaUtc ??= ahoraUtc;
    }

    private static string? Recortar(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) return null;
        var m = motivo.Trim();
        return m.Length > 300 ? m[..300] : m;
    }

    private void ExigirPendiente()
    {
        if (Estado != EstadoSolicitud.Pendiente) throw new ReglaDeNegocioException("La solicitud ya fue resuelta.");
    }
}
