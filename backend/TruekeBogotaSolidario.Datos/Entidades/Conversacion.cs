using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Chat privado entre el dueño de una publicación y quien la solicitó. Nace con la solicitud; se puede escribir mientras
/// la solicitud esté Pendiente o Aceptada y queda de solo lectura si se rechaza o cancela. Reemplaza el intercambio de correos.
/// </summary>
public class Conversacion
{
    private Conversacion() { } // EF Core

    public Conversacion(Guid solicitudId, Guid publicacionId, Guid duenioId, Guid solicitanteId, DateTime ahoraUtc)
    {
        if (duenioId == solicitanteId) throw new ReglaDeNegocioException("Una conversación necesita dos personas distintas.");
        Id = Guid.NewGuid();
        SolicitudId = solicitudId;
        PublicacionId = publicacionId;
        DuenioId = duenioId;
        SolicitanteId = solicitanteId;
        CreadaUtc = ahoraUtc;
        UltimoMensajeUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid SolicitudId { get; private set; }
    public Solicitud? Solicitud { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Guid DuenioId { get; private set; }
    public Usuario? Duenio { get; private set; }
    public Guid SolicitanteId { get; private set; }
    public Usuario? Solicitante { get; private set; }
    public DateTime CreadaUtc { get; private set; }
    public DateTime UltimoMensajeUtc { get; private set; }

    public bool EsParticipante(Guid usuarioId) => usuarioId == DuenioId || usuarioId == SolicitanteId;

    public Guid Contraparte(Guid usuarioId) => usuarioId == DuenioId ? SolicitanteId : DuenioId;

    public void RegistrarMensaje(DateTime ahoraUtc) => UltimoMensajeUtc = ahoraUtc;
}

public class Mensaje
{
    public const int LongitudMaxima = 1000;

    private Mensaje() { Texto = ""; } // EF Core

    public Mensaje(Guid conversacionId, Guid autorId, string texto, DateTime ahoraUtc)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length is < 1 or > LongitudMaxima)
            throw new ReglaDeNegocioException($"El mensaje debe tener entre 1 y {LongitudMaxima} caracteres.");
        if (texto.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            throw new ReglaDeNegocioException("El mensaje contiene caracteres no permitidos.");
        Id = Guid.NewGuid();
        ConversacionId = conversacionId;
        AutorId = autorId;
        Texto = texto;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversacionId { get; private set; }
    public Conversacion? Conversacion { get; private set; }
    public Guid AutorId { get; private set; }
    public string Texto { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public DateTime? LeidoUtc { get; private set; }
    public bool EstaOculto { get; private set; }
    public string? MotivoOcultamiento { get; private set; }

    public void MarcarLeido(DateTime ahoraUtc) => LeidoUtc ??= ahoraUtc;

    public void Ocultar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Debes indicar el motivo del ocultamiento.");
        if (EstaOculto) throw new ReglaDeNegocioException("El mensaje ya está oculto.");
        EstaOculto = true;
        MotivoOcultamiento = motivo.Trim();
    }
}
