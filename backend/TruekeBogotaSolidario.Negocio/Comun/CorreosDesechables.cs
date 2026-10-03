namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Dominios de correo temporal ("desechable") más usados. Con ellos se crean cuentas en segundos para farmear
/// Eco-Puntos de bienvenida o reputación. La lista se amplía por configuración (Seguridad:DominiosCorreoBloqueados).
/// </summary>
public static class CorreosDesechables
{
    private static readonly HashSet<string> Dominios = new(StringComparer.OrdinalIgnoreCase)
    {
        "10minutemail.com", "10minutemail.net", "20minutemail.com", "33mail.com", "anonaddy.me", "burnermail.io",
        "byom.de", "dispostable.com", "dropmail.me", "emailondeck.com", "fakeinbox.com", "fakemail.net", "getairmail.com",
        "getnada.com", "guerrillamail.biz", "guerrillamail.com", "guerrillamail.de", "guerrillamail.info", "guerrillamail.net",
        "guerrillamail.org", "guerrillamailblock.com", "harakirimail.com", "inboxbear.com", "inboxkitten.com", "incognitomail.org",
        "jetable.org", "mail-temp.com", "mail.tm", "mailcatch.com", "maildrop.cc", "mailinator.com", "mailinator.net",
        "mailinator2.com", "mailnesia.com", "mailpoof.com", "mailsac.com", "mintemail.com", "moakt.com", "mohmal.com",
        "mytemp.email", "nada.email", "nowmymail.com", "sharklasers.com", "spam4.me", "spambog.com", "spamgourmet.com",
        "spamex.com", "tempail.com", "temp-mail.io", "temp-mail.org", "tempinbox.com", "tempmail.com", "tempmail.dev",
        "tempmail.net", "tempmail.plus", "tempmailo.com", "tempr.email", "throwawaymail.com", "trashmail.com", "trashmail.de",
        "trashmail.net", "trbvm.com", "yopmail.com", "yopmail.fr", "yopmail.net", "zetmail.com", "emailfake.com",
        "fakemailgenerator.com", "luxusmail.org", "minutemail.com", "tmail.ws", "tmpmail.org", "tmpmail.net", "1secmail.com",
        "1secmail.net", "1secmail.org", "linshiyouxiang.net", "grr.la", "pokemail.net", "vomoto.com", "wegwerfmail.de",
    };

    public static bool EsDesechable(string correoNormalizado, IEnumerable<string>? adicionales = null)
    {
        var arroba = correoNormalizado.LastIndexOf('@');
        if (arroba < 0) return false;
        var dominio = correoNormalizado[(arroba + 1)..].Trim().ToLowerInvariant();
        return Dominios.Contains(dominio)
               || (adicionales?.Any(d => string.Equals(d.Trim(), dominio, StringComparison.OrdinalIgnoreCase)) ?? false);
    }
}
