using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Valores PÚBLICOS que el front necesita al arrancar (nunca secretos): así no se escriben a mano en el código de Angular
/// y cambian por configuración. Null = esa función está deshabilitada en el entorno.
/// </summary>
public sealed record ConfiguracionPublicaDto(string? CaptchaClaveSitio, string? GoogleClientId, string VersionPoliticaDatos,
    bool SubidaArchivosHabilitada, int MaxImagenesPorPublicacion, long TamanoMaximoArchivoBytes,
    int DiasCierreConUnaConfirmacion, int DiasCierreSinConfirmacion, bool PagosDePrueba,
    string? ImagenesOrigenUrl = null, string? ImagenesCdnUrl = null);

public interface IConfiguracionService
{
    ConfiguracionPublicaDto ObtenerPublica();
}

public sealed class ConfiguracionService : IConfiguracionService
{
    private readonly CaptchaOpciones _captcha;
    private readonly GoogleOpciones _google;
    private readonly LegalOpciones _legal;
    private readonly PagosOpciones _pagos;
    private readonly IAlmacenArchivos _almacen;
    private readonly AlmacenamientoOpciones _almacenamiento;

    public ConfiguracionService(IOptions<CaptchaOpciones> captcha, IOptions<GoogleOpciones> google, IOptions<LegalOpciones> legal,
        IOptions<PagosOpciones> pagos, IAlmacenArchivos almacen, IOptions<AlmacenamientoOpciones> almacenamiento)
    {
        _captcha = captcha.Value; _google = google.Value; _legal = legal.Value; _pagos = pagos.Value; _almacen = almacen;
        _almacenamiento = almacenamiento.Value;
    }

    /// <summary>Con CDN, el front reemplaza el prefijo de origen por el de la CDN al mostrar las fotos.</summary>
    private bool CdnActiva => _almacen.Habilitado && _almacenamiento.CdnHabilitada && _almacenamiento.CdnValida;

    public ConfiguracionPublicaDto ObtenerPublica() => new(
        _captcha.Habilitado && !string.IsNullOrWhiteSpace(_captcha.ClaveSitio) ? _captcha.ClaveSitio : null,
        string.IsNullOrWhiteSpace(_google.ClientId) ? null : _google.ClientId,
        _legal.VersionPoliticaDatos,
        _almacen.Habilitado,
        Publicacion.MaxImagenes,
        ReglasArchivos.TamanoMaximoBytes,
        Limites.DiasCierreConUnaConfirmacion,
        Limites.DiasCierreSinConfirmacion,
        _pagos.EsDePrueba,
        CdnActiva ? _almacenamiento.ServicioUrl.TrimEnd('/') + "/" : null,
        CdnActiva ? _almacenamiento.CdnUrl.TrimEnd('/') + "/" : null);
}
