using System.Net;
using System.Text;

namespace TruekeBogotaSolidario.Negocio.Correo;

/// <summary>
/// Plantilla HTML de la marca (estilos en línea, compatible con Gmail/Outlook). TODO texto variable se codifica con
/// HtmlEncode: un nombre como "&lt;script&gt;" nunca se interpreta. Siempre se envía también la versión de texto plano.
/// </summary>
public static class PlantillaCorreo
{
    private const string Verde = "#1f7a4d";
    private const string VerdeOscuro = "#123d27";

    public static MensajeCorreo Crear(string para, string asunto, string saludo, IEnumerable<string> parrafos,
        (string Texto, string Url)? boton = null, string? pie = null)
    {
        var lista = parrafos.ToList();
        var texto = new StringBuilder().Append(saludo).Append("\n\n");
        foreach (var p in lista) texto.Append(p).Append("\n\n");
        if (boton is { } b) texto.Append(b.Texto).Append(": ").Append(b.Url).Append("\n\n");
        if (pie is not null) texto.Append(pie).Append('\n');

        static string H(string s) => WebUtility.HtmlEncode(s);
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\">")
            .Append("<title>").Append(H(asunto)).Append("</title></head>")
            .Append("<body style=\"margin:0;padding:0;background:#f3f6f4;font-family:Segoe UI,Arial,sans-serif;color:#1d2a22\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f3f6f4;padding:24px 0\"><tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;width:100%;background:#ffffff;border-radius:14px;overflow:hidden\">")
            .Append($"<tr><td style=\"background:{VerdeOscuro};padding:20px 28px;color:#ffffff;font-size:20px;font-weight:700\">♻ Trueke Bogotá Solidario</td></tr>")
            .Append("<tr><td style=\"padding:28px\">")
            .Append("<p style=\"margin:0 0 16px;font-size:16px\">").Append(H(saludo)).Append("</p>");
        foreach (var p in lista)
            html.Append("<p style=\"margin:0 0 16px;font-size:15px;line-height:1.5\">").Append(H(p).Replace("\n", "<br>")).Append("</p>");
        if (boton is { } bt)
        {
            html.Append("<p style=\"margin:24px 0\"><a href=\"").Append(H(bt.Url))
                .Append($"\" style=\"background:{Verde};color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:999px;font-weight:600;display:inline-block\">")
                .Append(H(bt.Texto)).Append("</a></p>")
                .Append("<p style=\"margin:0 0 16px;font-size:12px;color:#5b6b61\">Si el botón no funciona, copia este enlace en tu navegador:<br>")
                .Append("<span style=\"word-break:break-all\">").Append(H(bt.Url)).Append("</span></p>");
        }
        if (pie is not null) html.Append("<p style=\"margin:16px 0 0;font-size:13px;color:#5b6b61\">").Append(H(pie)).Append("</p>");
        html.Append("</td></tr>")
            .Append("<tr><td style=\"padding:16px 28px;background:#f8faf9;font-size:12px;color:#7a887f\">")
            .Append("Economía circular para Bogotá: Trueke · Compra · Donación. Este es un mensaje automático; no lo respondas con datos sensibles.")
            .Append("</td></tr></table></td></tr></table></body></html>");

        return new MensajeCorreo(para, asunto, texto.ToString().TrimEnd(), html.ToString());
    }
}
