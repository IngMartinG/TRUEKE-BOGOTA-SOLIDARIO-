namespace TruekeBogotaSolidario.Datos.Entidades;

public enum PropositoToken { VerificarCorreo = 1, RestablecerClave = 2 }

/// <summary>Enlace de un solo uso enviado por correo. Solo se guarda el hash; el token en claro existe únicamente en el correo.</summary>
public class TokenUsoUnico
{
    private TokenUsoUnico() { TokenHash = ""; } // EF Core

    public TokenUsoUnico(Guid usuarioId, PropositoToken proposito, string tokenHash, DateTime ahoraUtc, TimeSpan vigencia)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Proposito = proposito;
        TokenHash = tokenHash;
        CreadoUtc = ahoraUtc;
        ExpiraUtc = ahoraUtc + vigencia;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public PropositoToken Proposito { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreadoUtc { get; private set; }
    public DateTime ExpiraUtc { get; private set; }
    public DateTime? UsadoUtc { get; private set; }

    public bool EsValido(DateTime ahoraUtc) => UsadoUtc is null && ExpiraUtc > ahoraUtc;

    /// <summary>También se usa para invalidar tokens anteriores cuando se emite uno nuevo.</summary>
    public void Consumir(DateTime ahoraUtc) => UsadoUtc ??= ahoraUtc;
}
