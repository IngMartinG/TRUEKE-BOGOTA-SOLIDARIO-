using System.Security.Cryptography;
using System.Text.Json;
using TruekeBogotaSolidario.Negocio.Push;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class PushReglasTests
{
    /// <summary>Par de llaves VAPID válido (P-256), como lo genera el comando de docs/DESPLIEGUE.md.</summary>
    public static (string Publica, string Privada) LlavesVapid()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(includePrivateParameters: true);
        static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (B64Url([0x04, .. p.Q.X!, .. p.Q.Y!]), B64Url(p.D!));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc123:APA91bH", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAAAAB", true)]
    [InlineData("https://wns2-bl2p.notify.windows.com/w/?token=BQYAAAB", true)]
    [InlineData("https://web.push.apple.com/QGv2xYz", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]            // sin TLS
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]       // otro puerto
    [InlineData("https://fcm.googleapis.com.atacante.co/x", false)]          // se hace pasar por Google
    [InlineData("https://atacante.co/fcm.googleapis.com", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]          // red interna (SSRF)
    [InlineData("https://usuario:clave@fcm.googleapis.com/x", false)]
    [InlineData("no es una url", false)]
    public void Solo_acepta_los_servicios_de_push_de_los_navegadores(string endpoint, bool permitido)
        => Assert.Equal(permitido, ReglasPush.EndpointPermitido(endpoint));

    [Fact]
    public void El_aviso_tiene_el_formato_del_service_worker_de_Angular_con_ruta_relativa()
    {
        var json = JsonDocument.Parse(ReglasPush.Carga("Mensaje de Ana P.", "Toca para abrir el chat.", "mensajes/123", "chat-123")).RootElement;
        var n = json.GetProperty("notification");
        Assert.Equal("Mensaje de Ana P.", n.GetProperty("title").GetString());
        Assert.Equal("chat-123", n.GetProperty("tag").GetString());
        var clic = n.GetProperty("data").GetProperty("onActionClick").GetProperty("default");
        Assert.Equal("navigateLastFocusedOrOpen", clic.GetProperty("operation").GetString());
        Assert.Equal("mensajes/123", clic.GetProperty("url").GetString()); // relativa al alcance: sirve con subcarpeta
    }

    [Fact]
    public void Las_llaves_VAPID_se_validan_al_arrancar()
    {
        var (publica, privada) = LlavesVapid();
        Assert.True(new PushOpciones().Validas); // sin llaves: apagado, no es error
        Assert.True(new PushOpciones { ClavePublica = publica, ClavePrivada = privada }.Validas);
        Assert.False(new PushOpciones { ClavePublica = privada, ClavePrivada = publica }.Validas); // invertidas
        Assert.False(new PushOpciones { ClavePublica = "no-es-base64-!!", ClavePrivada = privada }.Validas);
    }
}
