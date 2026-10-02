namespace TruekeBogotaSolidario.Datos.Common;

public static class UrlsSeguras
{
    /// <summary>
    /// https, o http SOLO hacia la propia máquina (emulador Azurite en desarrollo). Sin credenciales embebidas.
    /// </summary>
    public static bool EsValida(string? url, int longitudMaxima = 500)
    {
        if (url is null || url.Length > longitudMaxima || !Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (!string.IsNullOrEmpty(u.UserInfo)) return false;
        return u.Scheme == Uri.UriSchemeHttps || (u.Scheme == Uri.UriSchemeHttp && u.IsLoopback);
    }
}
