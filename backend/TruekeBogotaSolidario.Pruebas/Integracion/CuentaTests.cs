using System.Net;
using System.Net.Http.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Verificación de correo y recuperación de contraseña.</summary>
public class CuentaTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public CuentaTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<(string Correo, HttpClient Cliente)> RegistrarSinVerificarAsync()
    {
        var correo = $"cuenta{Guid.NewGuid():N}@trueke.test";
        var r = await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        { nombreCompleto = "Cuenta Prueba", localidad = "Fontibón", correo, clave = Api.ClaveValida, aceptoPoliticaDatos = true });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var sesion = (await r.Content.ReadFromJsonAsync<SesionMinDto>())!;
        return (correo, Api.ConToken(_fabrica, sesion.Token));
    }

    private Task<HttpResponseMessage> PostAnonimoAsync(string ruta, object cuerpo) => _fabrica.CreateClient().PostAsJsonAsync(ruta, cuerpo);

    [Fact]
    public async Task Sin_verificar_el_correo_no_puede_publicar_y_al_verificar_si()
    {
        var (correo, c) = await RegistrarSinVerificarAsync();
        Assert.False((await c.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/usuarios/yo")).GetProperty("correoVerificado").GetBoolean());

        var bloqueada = await c.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "Silla", descripcion = "x", categoriaId = 1, modo = "Donacion", localidad = "Suba" });
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);

        await Api.VerificarCorreoAsync(_fabrica, correo);
        Assert.True((await c.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/usuarios/yo")).GetProperty("correoVerificado").GetBoolean());
        await Api.CrearPublicacionAsync(c);
    }

    [Fact]
    public async Task El_enlace_de_verificacion_es_de_un_solo_uso_y_rechaza_tokens_inventados()
    {
        var (correo, _) = await RegistrarSinVerificarAsync();
        var token = Api.Correos(_fabrica).UltimoToken(correo, "verificar-correo")!;
        Assert.Equal(HttpStatusCode.NoContent, (await PostAnonimoAsync("/api/v1/auth/verificar-correo", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAnonimoAsync("/api/v1/auth/verificar-correo", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAnonimoAsync("/api/v1/auth/verificar-correo", new { token = new string('a', 43) })).StatusCode);
    }

    [Fact]
    public async Task Reenviar_invalida_el_enlace_anterior()
    {
        var (correo, c) = await RegistrarSinVerificarAsync();
        var viejo = Api.Correos(_fabrica).UltimoToken(correo, "verificar-correo")!;
        Assert.Equal(HttpStatusCode.Accepted, (await c.PostAsync("/api/v1/auth/reenviar-verificacion", null)).StatusCode);
        var nuevo = Api.Correos(_fabrica).UltimoToken(correo, "verificar-correo")!;
        Assert.NotEqual(viejo, nuevo);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAnonimoAsync("/api/v1/auth/verificar-correo", new { token = viejo })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAnonimoAsync("/api/v1/auth/verificar-correo", new { token = nuevo })).StatusCode);
    }

    [Fact]
    public async Task Olvide_clave_responde_igual_exista_o_no_la_cuenta()
    {
        var (correo, _) = await RegistrarSinVerificarAsync();
        var existe = await PostAnonimoAsync("/api/v1/auth/olvide-clave", new { correo });
        var noExiste = await PostAnonimoAsync("/api/v1/auth/olvide-clave", new { correo = "nadie-" + Guid.NewGuid().ToString("N") + "@trueke.test" });
        Assert.Equal(HttpStatusCode.Accepted, existe.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, noExiste.StatusCode);
        Assert.Equal(await existe.Content.ReadAsStringAsync(), await noExiste.Content.ReadAsStringAsync());
        Assert.NotNull(Api.Correos(_fabrica).UltimoToken(correo, "restablecer-clave"));
    }

    [Fact]
    public async Task Restablecer_cambia_la_clave_cierra_sesiones_desbloquea_y_es_de_un_solo_uso()
    {
        var (correo, c) = await RegistrarSinVerificarAsync();
        for (var i = 0; i < 5; i++) await PostAnonimoAsync("/api/v1/auth/login", new { correo, clave = "Incorrecta1" }); // queda bloqueada

        await PostAnonimoAsync("/api/v1/auth/olvide-clave", new { correo });
        var token = Api.Correos(_fabrica).UltimoToken(correo, "restablecer-clave")!;
        Assert.Equal(HttpStatusCode.NoContent, (await PostAnonimoAsync("/api/v1/auth/restablecer-clave", new { token, claveNueva = "NuevaClave77" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode);                 // sesión anterior revocada
        Assert.Equal(HttpStatusCode.OK, (await PostAnonimoAsync("/api/v1/auth/login", new { correo, clave = "NuevaClave77" })).StatusCode); // y desbloqueada
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAnonimoAsync("/api/v1/auth/login", new { correo, clave = Api.ClaveValida })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAnonimoAsync("/api/v1/auth/restablecer-clave", new { token, claveNueva = "OtraMas123" })).StatusCode);
    }

    [Fact]
    public async Task Restablecer_exige_una_clave_robusta()
    {
        var (correo, _) = await RegistrarSinVerificarAsync();
        await PostAnonimoAsync("/api/v1/auth/olvide-clave", new { correo });
        var token = Api.Correos(_fabrica).UltimoToken(correo, "restablecer-clave")!;
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAnonimoAsync("/api/v1/auth/restablecer-clave", new { token, claveNueva = "solominusculas" })).StatusCode);
    }
}
