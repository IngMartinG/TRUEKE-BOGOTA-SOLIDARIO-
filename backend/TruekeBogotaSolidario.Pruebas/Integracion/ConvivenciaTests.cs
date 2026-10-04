using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Bloquear usuarios y preferencias de avisos por correo.</summary>
public class ConvivenciaTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ConvivenciaTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task<Guid> SolicitarAsync(HttpClient c, Guid pub)
    {
        var r = await c.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Sigue disponible?" });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conversacionId").GetGuid();
    }

    private static Task<HttpResponseMessage> EscribirAsync(HttpClient c, Guid conv, string texto = "Hola")
        => c.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto });

    [Fact]
    public async Task Bloquear_impide_escribir_y_solicitar_en_ambas_direcciones_y_desbloquear_lo_restablece()
    {
        var sesDuenia = await Api.RegistrarSesionAsync(_fabrica, "duenaBloquea");
        var duenia = Api.ConToken(_fabrica, sesDuenia.Token);
        var sesOtro = await Api.RegistrarSesionAsync(_fabrica, "molesto");
        var otro = Api.ConToken(_fabrica, sesOtro.Token);
        var conv = await SolicitarAsync(otro, await Api.CrearPublicacionAsync(duenia));

        Assert.Equal(HttpStatusCode.NoContent, (await duenia.PostAsync($"/api/v1/usuarios/{sesOtro.Usuario.Id}/bloqueo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await duenia.PostAsync($"/api/v1/usuarios/{sesOtro.Usuario.Id}/bloqueo", null)).StatusCode); // idempotente

        Assert.Equal(HttpStatusCode.BadRequest, (await EscribirAsync(otro, conv)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EscribirAsync(duenia, conv)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await otro.PostAsJsonAsync("/api/v1/solicitudes",
            new { publicacionId = await Api.CrearPublicacionAsync(duenia), mensaje = "Otra" })).StatusCode);

        var vistaDuenia = (await duenia.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones")).EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == conv);
        Assert.False(vistaDuenia.GetProperty("escribible").GetBoolean());
        Assert.True(vistaDuenia.GetProperty("yoBloquee").GetBoolean());
        var vistaOtro = (await otro.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones")).EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == conv);
        Assert.False(vistaOtro.GetProperty("escribible").GetBoolean());
        Assert.False(vistaOtro.GetProperty("yoBloquee").GetBoolean());   // a quien bloquean no se le dice quién bloqueó

        var bloqueados = await duenia.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/bloqueados");
        Assert.Equal(sesOtro.Usuario.Id, bloqueados.EnumerateArray().Single().GetProperty("perfil").GetProperty("id").GetGuid());
        Assert.Empty((await otro.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/bloqueados")).EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await duenia.DeleteAsync($"/api/v1/usuarios/{sesOtro.Usuario.Id}/bloqueo")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await EscribirAsync(otro, conv)).StatusCode);
    }

    [Fact]
    public async Task No_se_puede_bloquear_a_si_mismo_ni_a_alguien_que_no_existe()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "solito");
        var c = Api.ConToken(_fabrica, ses.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync($"/api/v1/usuarios/{ses.Usuario.Id}/bloqueo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/v1/usuarios/{Guid.NewGuid()}/bloqueo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().PostAsync($"/api/v1/usuarios/{ses.Usuario.Id}/bloqueo", null)).StatusCode);
    }

    [Fact]
    public async Task Los_avisos_por_correo_respetan_las_preferencias()
    {
        var sesDuenia = await Api.RegistrarSesionAsync(_fabrica, "avisada");
        var duenia = Api.ConToken(_fabrica, sesDuenia.Token);
        var pref = await duenia.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/preferencias-avisos");
        Assert.True(pref.GetProperty("intercambios").GetBoolean());
        Assert.True(pref.GetProperty("mensajes").GetBoolean());
        Assert.False(pref.GetProperty("novedades").GetBoolean());   // solo con consentimiento expreso

        // Sin la app abierta: la solicitud nueva y el primer mensaje le llegan por correo (sin el texto del mensaje)
        var otro = await Api.RegistrarAsync(_fabrica, "interesadoAvisos");
        var conv = await SolicitarAsync(otro, await Api.CrearPublicacionAsync(duenia));
        (await EscribirAsync(otro, conv, "Texto privado que no debe ir al correo")).EnsureSuccessStatusCode();
        (await EscribirAsync(otro, conv, "Otro mensaje")).EnsureSuccessStatusCode();
        var correos = Api.Correos(_fabrica).Para(sesDuenia.Usuario.Correo);
        Assert.Contains(correos, m => m.Asunto == "Novedades en tu intercambio");
        var deChat = correos.Where(m => m.Asunto.StartsWith("Tienes un mensaje nuevo", StringComparison.Ordinal)).ToList();
        Assert.Single(deChat);                                       // como mucho uno por conversación cada 2 horas
        Assert.DoesNotContain("Texto privado", deChat[0].Texto);

        // Apagados: ya no llegan
        var r = await duenia.PutAsJsonAsync("/api/v1/usuarios/yo/preferencias-avisos", new { intercambios = false, mensajes = false, planes = true, novedades = false });
        Assert.False((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("intercambios").GetBoolean());
        var antes = Api.Correos(_fabrica).Para(sesDuenia.Usuario.Correo).Count;
        var otroMas = await Api.RegistrarAsync(_fabrica, "otroInteresado");
        await SolicitarAsync(otroMas, await Api.CrearPublicacionAsync(duenia));
        Assert.Equal(antes, Api.Correos(_fabrica).Para(sesDuenia.Usuario.Correo).Count);
    }
}
