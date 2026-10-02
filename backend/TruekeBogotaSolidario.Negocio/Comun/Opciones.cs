using System.ComponentModel.DataAnnotations;

namespace TruekeBogotaSolidario.Negocio.Comun;

public sealed class JwtOpciones
{
    public const string Seccion = "Jwt";
    [Required, MinLength(1)] public string Issuer { get; set; } = "";
    [Required, MinLength(1)] public string Audience { get; set; } = "";
    /// <summary>Mínimo 32 caracteres (256 bits). Nunca se versiona: variable de entorno / Key Vault.</summary>
    [Required, MinLength(32)] public string Key { get; set; } = "";
    /// <summary>Vida del token de acceso. Corta a propósito: la sesión se mantiene con el refresco (cookie HttpOnly).</summary>
    [Range(5, 1440)] public int Minutos { get; set; } = 15;
}

public sealed class SeguridadOpciones
{
    public const string Seccion = "Seguridad";
    [Range(3, 20)] public int MaxIntentosLogin { get; set; } = 5;
    [Range(1, 1440)] public int MinutosBloqueo { get; set; } = 15;
    /// <summary>Cuánto se cachea la validación de sesión (versión de seguridad). Es también el retraso máximo de revocación entre instancias.</summary>
    [Range(0, 3600)] public int SegundosCacheSesion { get; set; } = 30;
    /// <summary>Vida de cada token de refresco (se renueva con cada uso).</summary>
    [Range(1, 90)] public int DiasRefresco { get; set; } = 14;
    /// <summary>Vida máxima absoluta de una sesión aunque se siga usando: luego hay que volver a iniciar sesión.</summary>
    [Range(1, 365)] public int DiasMaximosSesion { get; set; } = 30;
    /// <summary>Si un token recién reemplazado se reusa dentro de esta ventana (dos pestañas refrescando a la vez) se responde 409 sin revocar la familia.</summary>
    [Range(0, 120)] public int SegundosGraciaRefresco { get; set; } = 30;
}

public sealed class WompiOpciones
{
    public string BaseUrl { get; set; } = "https://production.wompi.co/v1";
    public string LlavePublica { get; set; } = "";
    public string LlavePrivada { get; set; } = "";
    public string SecretoIntegridad { get; set; } = "";
    public string SecretoEventos { get; set; } = "";
}

public sealed class PagosOpciones
{
    public const string Seccion = "Pagos";
    /// <summary>"Wompi" (real) o "Simulado" (solo desarrollo/pruebas; el arranque lo rechaza en Producción).</summary>
    public string Proveedor { get; set; } = "Wompi";
    [Range(5, 1440)] public int MinutosParaExpirarPendientes { get; set; } = 60;
    [Range(10, 3600)] public int SegundosEntreReconciliaciones { get; set; } = 120;
    public WompiOpciones Wompi { get; set; } = new();
    public bool EsSimulado => string.Equals(Proveedor, "Simulado", StringComparison.OrdinalIgnoreCase);
}

public sealed class UrlsOpciones
{
    public const string Seccion = "Urls";
    /// <summary>URL base del front Angular: los enlaces de los correos apuntan aquí (https obligatorio en Producción).</summary>
    public string Frontend { get; set; } = "http://localhost:4200";
    /// <summary>Hosts https permitidos para imágenes de publicaciones (p. ej. tucuenta.blob.core.windows.net). Vacío = cualquier https.</summary>
    public List<string> HostsPermitidosImagenes { get; set; } = new();
    /// <summary>Hosts https permitidos para documentos de verificación de identidad.</summary>
    public List<string> HostsPermitidosDocumentos { get; set; } = new();
}

public sealed class LegalOpciones
{
    public const string Seccion = "Legal";
    /// <summary>Versión vigente de la política de tratamiento de datos que el front muestra al registrarse.</summary>
    [Required, StringLength(20, MinimumLength = 1)] public string VersionPoliticaDatos { get; set; } = "2026-10";
}

public static class Limites
{
    public const int MaxPublicacionesActivasPorUsuario = 50;
    public const int MaxSolicitudesPendientesPorUsuario = 10;
    public const int MaxPagosPendientesPorHora = 5;
    public const int MaxComentariosPorHora = 20;
}
