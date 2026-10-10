using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Enlaces para compartir. WhatsApp, Facebook y X no ejecutan JavaScript: leen las etiquetas Open Graph del HTML.
/// El front es una SPA estática, así que la API sirve una página mínima con las etiquetas de la publicación y
/// redirige a las personas al instante a la publicación en el front. Solo datos ya públicos en el catálogo.
/// </summary>
[ApiController]
[Authorize]
[Route("compartir/publicaciones/{id:guid}")]
[ApiExplorerSettings(IgnoreApi = true)] // HTML para robots de vista previa, no es parte del contrato JSON
public sealed class CompartirController : ControllerBase
{
    private const string Marca = "Trueke Bogotá Solidario";
    private const string DescripcionGeneral = "Intercambia, compra y dona en tu ciudad. Economía circular hecha comunidad.";

    private readonly IVistaPreviaService _vistaPrevia;
    private readonly UrlsOpciones _urls;

    public CompartirController(IVistaPreviaService vistaPrevia, IOptions<UrlsOpciones> urls)
    {
        _vistaPrevia = vistaPrevia; _urls = urls.Value;
    }

    private string Front => _urls.Frontend.TrimEnd('/');
    private string Api => string.IsNullOrWhiteSpace(_urls.Api) ? $"{Request.Scheme}://{Request.Host}" : _urls.Api.TrimEnd('/');

    [HttpGet, AllowAnonymous]
    public async Task<ContentResult> Pagina(Guid id)
    {
        var destino = $"{Front}/publicacion/{id}";
        var vp = await _vistaPrevia.ObtenerAsync(id);
        // Lo que no es visible "no existe": la misma página general, sin revelar si la publicación existe.
        var titulo = vp?.Titulo ?? Marca;
        var descripcion = vp is null ? DescripcionGeneral : string.IsNullOrEmpty(vp.Descripcion) ? vp.Etiqueta : $"{vp.Etiqueta} — {vp.Descripcion}";
        var imagen = vp is { TieneFoto: true } ? $"{Api}/compartir/publicaciones/{id}/imagen.jpg" : $"{Front}/og-imagen.png";

        Response.Headers.CacheControl = "public, max-age=600";
        return Content(Html(titulo, descripcion, imagen, $"{Api}/compartir/publicaciones/{id}", destino), "text/html; charset=utf-8");
    }

    [HttpGet("imagen.jpg"), AllowAnonymous]
    public async Task<IActionResult> Imagen(Guid id, CancellationToken ct)
    {
        var jpeg = await _vistaPrevia.ImagenAsync(id, ct);
        if (jpeg is null) return Redirect($"{Front}/og-imagen.png");
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(jpeg, "image/jpeg");
    }

    internal static string Html(string titulo, string descripcion, string imagen, string urlPropia, string destino)
    {
        // Escape de los 5 caracteres especiales de HTML (WebUtility también convertiría tildes y "·" en &#NNN;).
        static string E(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"es-CO\"><head><meta charset=\"utf-8\">");
        sb.Append($"<title>{E(titulo)} · {E(Marca)}</title>");
        sb.Append("<meta name=\"robots\" content=\"noindex\">");
        sb.Append($"<meta name=\"description\" content=\"{E(descripcion)}\">");
        sb.Append($"<meta property=\"og:site_name\" content=\"{E(Marca)}\">");
        sb.Append("<meta property=\"og:type\" content=\"website\"><meta property=\"og:locale\" content=\"es_CO\">");
        sb.Append($"<meta property=\"og:title\" content=\"{E(titulo)}\">");
        sb.Append($"<meta property=\"og:description\" content=\"{E(descripcion)}\">");
        // og:url apunta a ESTA página: si apuntara al front, Facebook volvería a leer la SPA y mostraría lo general.
        sb.Append($"<meta property=\"og:url\" content=\"{E(urlPropia)}\">");
        sb.Append($"<meta property=\"og:image\" content=\"{E(imagen)}\">");
        sb.Append("<meta property=\"og:image:width\" content=\"1200\"><meta property=\"og:image:height\" content=\"630\">");
        sb.Append($"<meta property=\"og:image:alt\" content=\"{E(titulo)}\">");
        sb.Append("<meta name=\"twitter:card\" content=\"summary_large_image\">");
        sb.Append($"<meta http-equiv=\"refresh\" content=\"0;url={E(destino)}\">");
        sb.Append($"</head><body><p><a href=\"{E(destino)}\">Ver «{E(titulo)}» en {E(Marca)}</a></p></body></html>");
        return sb.ToString();
    }
}
