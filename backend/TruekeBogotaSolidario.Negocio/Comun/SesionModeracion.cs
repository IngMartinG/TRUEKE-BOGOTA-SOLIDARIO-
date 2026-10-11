using Microsoft.Extensions.Options;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// ¿La sesión actual habilita las vistas de moderación (contenido oculto, motivos, coordenadas exactas)? No basta el rol:
/// igual que las rutas de administración, exige que la sesión se haya iniciado con verificación en dos pasos (amr=mfa).
/// Presentacion la implementa con el JWT de la petición.
/// </summary>
public interface ISesionModeracion
{
    bool ConDosFactores { get; }
}

/// <summary>Fuera de una petición HTTP no hay sesión con 2FA: solo vale si la configuración no la exige (desarrollo).</summary>
public sealed class SesionModeracionSinPeticion : ISesionModeracion
{
    private readonly SeguridadOpciones _o;
    public SesionModeracionSinPeticion(IOptions<SeguridadOpciones> o) => _o = o.Value;
    public bool ConDosFactores => !_o.ExigirDosFactoresModeradores;
}
