namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Token de refresco (rotativo). En la base solo se guarda el HASH SHA-256: una copia de la BD no permite iniciar sesión.
/// Cada uso crea un token nuevo de la misma "familia"; si se reutiliza uno ya reemplazado se asume robo y se revoca la familia.
/// </summary>
public class SesionRefresh
{
    private SesionRefresh() { TokenHash = ""; } // EF Core

    public SesionRefresh(Guid usuarioId, Guid familiaId, string tokenHash, DateTime ahoraUtc, DateTime expiraUtc, DateTime expiraFamiliaUtc)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        FamiliaId = familiaId;
        TokenHash = tokenHash;
        CreadoUtc = ahoraUtc;
        ExpiraFamiliaUtc = expiraFamiliaUtc;
        ExpiraUtc = expiraUtc < expiraFamiliaUtc ? expiraUtc : expiraFamiliaUtc;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    /// <summary>Todos los tokens nacidos de un mismo inicio de sesión comparten familia.</summary>
    public Guid FamiliaId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreadoUtc { get; private set; }
    public DateTime ExpiraUtc { get; private set; }
    /// <summary>Vida máxima absoluta de la sesión, aunque se siga refrescando.</summary>
    public DateTime ExpiraFamiliaUtc { get; private set; }
    public Guid? ReemplazadoPorId { get; private set; }
    public DateTime? RevocadoUtc { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public bool EstaActiva(DateTime ahoraUtc) => RevocadoUtc is null && ReemplazadoPorId is null && ExpiraUtc > ahoraUtc;

    public void MarcarReemplazada(Guid nuevaId, DateTime ahoraUtc)
    {
        ReemplazadoPorId = nuevaId;
        RevocadoUtc = ahoraUtc;
    }

    public void Revocar(DateTime ahoraUtc) => RevocadoUtc ??= ahoraUtc;
}
