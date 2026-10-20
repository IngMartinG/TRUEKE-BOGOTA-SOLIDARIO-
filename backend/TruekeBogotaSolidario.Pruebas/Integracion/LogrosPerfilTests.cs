using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class LogrosPerfilTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public LogrosPerfilTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task CompletarTruekeAsync(HttpClient duenio, HttpClient otro)
    {
        var pub = await Api.CrearPublicacionAsync(duenio);
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa" });
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await otro.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task El_perfil_publico_muestra_solo_los_logros_obtenidos_y_el_propio_ve_su_progreso()
    {
        var sesion = await Api.RegistrarSesionAsync(_fabrica, "logros-ana");
        var ana = Api.ConToken(_fabrica, sesion.Token);
        var beto = await Api.RegistrarAsync(_fabrica, "logros-beto");
        // Dos truekes con la misma persona: el segundo no da puntos (anti-farmeo), así que tampoco cuenta para el logro
        await CompletarTruekeAsync(ana, beto);
        await CompletarTruekeAsync(ana, beto);

        var perfil = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{sesion.Usuario.Id}/perfil");
        var publicos = perfil.GetProperty("logros").EnumerateArray().ToList();
        Assert.All(publicos, l => Assert.True(l.GetProperty("obtenido").GetBoolean()));
        Assert.Contains(publicos, l => l.GetProperty("clave").GetString() == "primer-intercambio");
        Assert.Contains(publicos, l => l.GetProperty("clave").GetString() == "pionero");
        Assert.DoesNotContain(publicos, l => l.GetProperty("clave").GetString() == "truekero");

        var mios = await ana.GetFromJsonAsync<JsonElement>("/api/v1/eco-puntos/logros");
        var truekero = mios.EnumerateArray().Single(l => l.GetProperty("clave").GetString() == "truekero");
        Assert.False(truekero.GetProperty("obtenido").GetBoolean());
        Assert.Equal(1, truekero.GetProperty("progreso").GetInt32());
        Assert.Equal(3, truekero.GetProperty("meta").GetInt32());
    }

    [Fact]
    public async Task Los_logros_propios_exigen_sesion()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/v1/eco-puntos/logros")).StatusCode);
}
