using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>La misma regla que <see cref="DosFactoresHandler"/>, aplicada a las vistas públicas que cambian para un moderador.</summary>
public sealed class SesionModeracionHttp : ISesionModeracion
{
    private readonly IHttpContextAccessor _http;
    private readonly SeguridadOpciones _o;

    public SesionModeracionHttp(IHttpContextAccessor http, IOptions<SeguridadOpciones> o)
    {
        _http = http;
        _o = o.Value;
    }

    public bool ConDosFactores => !_o.ExigirDosFactoresModeradores || _http.HttpContext?.User.HasClaim("amr", "mfa") == true;
}
