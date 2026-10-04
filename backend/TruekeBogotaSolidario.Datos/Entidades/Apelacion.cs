using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public enum EstadoApelacion { Pendiente = 1, Aceptada = 2, Rechazada = 3 }

/// <summary>
/// Versión de la persona denunciada sobre una denuncia que un moderador consideró procedente. Una por resolución,
/// dentro de <see cref="DiasParaApelar"/> días. Si se acepta, se revierte la medida (el contenido vuelve a mostrarse).
/// </summary>
public class Apelacion
{
    public const int DiasParaApelar = 15;
    public const int LongitudMinima = 20;
    public const int LongitudMaxima = 1000;

    private Apelacion() { Texto = ""; } // EF Core

    public Apelacion(Guid resolucionId, Guid usuarioId, TipoObjetoDenuncia tipo, Guid objetivoId, AccionModeracion accionOriginal,
        string texto, DateTime ahoraUtc)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length is < LongitudMinima or > LongitudMaxima)
            throw new ReglaDeNegocioException($"Cuéntanos tu versión en {LongitudMinima} a {LongitudMaxima} caracteres.");
        if (texto.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            throw new ReglaDeNegocioException("El texto contiene caracteres no permitidos.");
        Id = Guid.NewGuid();
        ResolucionId = resolucionId;
        UsuarioId = usuarioId;
        Tipo = tipo;
        ObjetivoId = objetivoId;
        AccionOriginal = accionOriginal;
        Texto = texto;
        FechaUtc = ahoraUtc;
        Estado = EstadoApelacion.Pendiente;
    }

    public Guid Id { get; private set; }
    public Guid ResolucionId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public TipoObjetoDenuncia Tipo { get; private set; }
    public Guid ObjetivoId { get; private set; }
    public AccionModeracion AccionOriginal { get; private set; }
    public string Texto { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public EstadoApelacion Estado { get; private set; }
    public Guid? ModeradorId { get; private set; }
    public DateTime? FechaResolucionUtc { get; private set; }
    public string? NotaResolucion { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void Resolver(Guid moderadorId, bool aceptada, string nota, DateTime ahoraUtc)
    {
        if (Estado != EstadoApelacion.Pendiente) throw new ReglaDeNegocioException("La apelación ya fue resuelta.");
        nota = (nota ?? "").Trim();
        if (nota.Length is < 3 or > 300) throw new ReglaDeNegocioException("Explica la decisión (3 a 300 caracteres): la verá quien apeló.");
        Estado = aceptada ? EstadoApelacion.Aceptada : EstadoApelacion.Rechazada;
        ModeradorId = moderadorId;
        FechaResolucionUtc = ahoraUtc;
        NotaResolucion = nota;
    }
}
