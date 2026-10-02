using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class SeguridadTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public SeguridadTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static JsonElement Payload(string jwt)
    {
        var p = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        p = p.PadRight(p.Length + (4 - p.Length % 4) % 4, '=');
        return JsonDocument.Parse(Convert.FromBase64String(p)).RootElement;
    }

    // ------------------------------------------------------------------ Login y bloqueo
    [Fact]
    public async Task Tras_5_intentos_fallidos_la_cuenta_se_bloquea_incluso_con_la_clave_correcta()
    {
        var correo = $"bloqueo-{Guid.NewGuid():N}@trueke.test";
        var anonimo = _fabrica.CreateClient();
        (await anonimo.PostAsJsonAsync("/api/v1/auth/registrar",
            new { nombreCompleto = "Bloqueo Prueba", localidad = "Suba", correo, clave = Api.ClaveValida, aceptoPoliticaDatos = true })).EnsureSuccessStatusCode();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = "Incorrecta1" })).StatusCode);

        var conClaveBuena = await anonimo.PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = Api.ClaveValida });
        Assert.Equal(HttpStatusCode.Unauthorized, conClaveBuena.StatusCode);

        // mismo mensaje que credenciales incorrectas: no revela que la cuenta existe ni que está bloqueada
        var inexistente = await anonimo.PostAsJsonAsync("/api/v1/auth/login", new { correo = "nadie@trueke.test", clave = "Algo12345" });
        Assert.Equal(Titulo(await conClaveBuena.Content.ReadAsStringAsync()), Titulo(await inexistente.Content.ReadAsStringAsync()));
    }

    private static string? Titulo(string json) => JsonDocument.Parse(json).RootElement.GetProperty("title").GetString();

    // ------------------------------------------------------------------ JWT
    [Fact]
    public async Task El_token_lleva_sub_role_sv_y_ningun_dato_personal()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "claims");
        var p = Payload(ses.Token);
        Assert.Equal(ses.Usuario.Id.ToString(), p.GetProperty("sub").GetString());
        Assert.Equal("Cliente", p.GetProperty("role").GetString());
        Assert.True(p.TryGetProperty("sv", out _));
        Assert.True(p.TryGetProperty("exp", out _));
        var texto = p.GetRawText();
        Assert.DoesNotContain("@", texto);
        Assert.DoesNotContain("claims Prueba", texto);
    }

    [Fact]
    public async Task Tokens_manipulados_alg_none_u_otra_clave_son_rechazados()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "manipulado");
        var partes = ses.Token.Split('.');

        // 1) payload alterado (rol SuperUsuario) con la firma original
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(Payload(ses.Token).GetRawText())!;
        payload["role"] = "SuperUsuario";
        var payloadAlterado = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var alterado = $"{partes[0]}.{payloadAlterado}.{partes[2]}";

        // 2) alg:none sin firma
        var none = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}")) + "." + payloadAlterado + ".";

        // 3) firmado con otra clave
        var contenido = $"{partes[0]}.{payloadAlterado}";
        var otraFirma = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes("otra-clave-que-no-es-la-del-servidor-1234567890"), Encoding.ASCII.GetBytes(contenido)));
        var otraClave = $"{contenido}.{otraFirma}";

        foreach (var token in new[] { alterado, none, otraClave, "basura", "" })
        {
            var c = _fabrica.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode);
        }
    }

    [Fact]
    public async Task Cambiar_la_clave_revoca_los_tokens_anteriores()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "revocar");
        var c = Api.ConToken(_fabrica, ses.Token);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode);

        var cambio = await c.PostAsJsonAsync("/api/v1/auth/cambiar-clave", new { claveActual = Api.ClaveValida, claveNueva = "OtraClave999" });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode);
        // la respuesta trae una sesión nueva para este dispositivo
        var nueva = (await cambio.Content.ReadFromJsonAsync<SesionMinDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await Api.ConToken(_fabrica, nueva.Token).GetAsync("/api/v1/usuarios/yo")).StatusCode);
    }

    [Fact]
    public async Task Cerrar_sesiones_revoca_los_tokens()
    {
        var c = await Api.RegistrarAsync(_fabrica, "cerrar");
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/v1/auth/cerrar-sesiones", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode);
    }

    // ------------------------------------------------------------------ Roles
    [Fact]
    public async Task Autorizacion_por_rol_Cliente_Administrador_SuperUsuario()
    {
        var super = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var sesAdmin = await Api.RegistrarSesionAsync(_fabrica, "admin");
        var cliente = await Api.RegistrarAsync(_fabrica, "cliente");
        var otro = await Api.RegistrarSesionAsync(_fabrica, "otro");

        // Cliente: nada de administración
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/admin/verificaciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PatchAsJsonAsync($"/api/v1/admin/usuarios/{otro.Usuario.Id}/rol", new { rol = "SuperUsuario" })).StatusCode);

        // SuperUsuario asciende a Administrador → el token viejo del ascendido queda revocado
        var tokenViejo = Api.ConToken(_fabrica, sesAdmin.Token);
        Assert.Equal(HttpStatusCode.OK, (await super.PatchAsJsonAsync($"/api/v1/admin/usuarios/{sesAdmin.Usuario.Id}/rol", new { rol = "Administrador" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tokenViejo.GetAsync("/api/v1/usuarios/yo")).StatusCode);

        // Con un token nuevo, el Administrador modera pero NO cambia roles
        var admin = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, sesAdmin.Usuario.Correo, Api.ClaveValida));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/verificaciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PatchAsJsonAsync($"/api/v1/admin/usuarios/{otro.Usuario.Id}/rol", new { rol = "Administrador" })).StatusCode);

        // "Invitado" nunca se asigna como rol
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PatchAsJsonAsync($"/api/v1/admin/usuarios/{otro.Usuario.Id}/rol", new { rol = "Invitado" })).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/usuarios/yo")]
    [InlineData("GET", "/api/v1/publicaciones/mias")]
    [InlineData("POST", "/api/v1/publicaciones")]
    [InlineData("POST", "/api/v1/solicitudes")]
    [InlineData("GET", "/api/v1/solicitudes/recibidas")]
    [InlineData("GET", "/api/v1/eco-puntos/resumen")]
    [InlineData("POST", "/api/v1/pagos/iniciar")]
    [InlineData("GET", "/api/v1/admin/verificaciones")]
    [InlineData("POST", "/api/v1/auth/cambiar-clave")]
    public async Task Invitado_recibe_401_en_endpoints_protegidos(string metodo, string ruta)
    {
        var req = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo == "POST") req.Content = JsonContent.Create(new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().SendAsync(req)).StatusCode);
    }

    // ------------------------------------------------------------------ El actor sale del JWT, no del body
    [Fact]
    public async Task No_se_puede_suplantar_al_usuario_enviando_ids_en_el_body()
    {
        var victima = await Api.RegistrarSesionAsync(_fabrica, "victima");
        var atacante = await Api.RegistrarAsync(_fabrica, "atacante");
        var resp = await atacante.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Suplantada", descripcion = "x", categoriaId = 1, modo = "Donacion", localidad = "Usme",
            propietarioId = victima.Usuario.Id, usuarioId = victima.Usuario.Id
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var mia = await (await atacante.GetAsync("/api/v1/publicaciones/mias")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(mia.EnumerateArray(), p => p.GetProperty("titulo").GetString() == "Suplantada");
        var deVictima = await (await Api.ConToken(_fabrica, victima.Token).GetAsync("/api/v1/publicaciones/mias")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(deVictima.EnumerateArray());
    }

    // ------------------------------------------------------------------ Respuestas: sin datos sensibles, sin Server, con cabeceras
    [Fact]
    public async Task Las_respuestas_no_exponen_hash_rowversion_ni_correos_de_terceros()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "privado");
        var pub = await Api.CrearPublicacionAsync(duenio, 4.6, -74.08);
        var cuerpo = await (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("claveHash", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rowVersion", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", cuerpo);
        Assert.DoesNotContain("propietarioId", cuerpo, StringComparison.OrdinalIgnoreCase);

        var yo = await (await duenio.GetAsync("/api/v1/usuarios/yo")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("clave", yo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("intentosFallidos", yo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cabeceras_de_seguridad_presentes_y_sin_cabecera_Server()
    {
        var r = await _fabrica.CreateClient().GetAsync("/api/v1/categorias");
        Assert.False(r.Headers.Contains("Server"));
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Los_enums_viajan_como_texto()
    {
        var c = await Api.RegistrarAsync(_fabrica, "enum");
        var id = await Api.CrearPublicacionAsync(c, modo: "Donacion");
        var p = await (await c.GetAsync($"/api/v1/publicaciones/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Donacion", p.GetProperty("modo").GetString());
        Assert.Equal("Disponible", p.GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/publicaciones",
            new { titulo = "Mal modo", descripcion = "x", categoriaId = 1, modo = "Robo", localidad = "Usme" })).StatusCode);
    }

    [Fact]
    public async Task Cuerpo_mayor_a_1MB_es_rechazado()
    {
        var c = await Api.RegistrarAsync(_fabrica, "grande");
        var resp = await c.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "x", descripcion = new string('a', 1_100_000) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
    }

    // ------------------------------------------------------------------ 500 sin detalles internos
    private sealed class EcoPuntosQueFalla : IEcoPuntosService
    {
        public Task<EcoPuntosResumenDto> ResumenAsync(Guid actorId) => throw new InvalidOperationException("SECRETO: cadena de conexión Server=prod;Password=123");
        public PoliticaEcoPuntosDto ObtenerPolitica() => throw new InvalidOperationException("SECRETO: cadena de conexión Server=prod;Password=123");
    }

    [Fact]
    public async Task Un_error_inesperado_devuelve_500_generico_sin_mensaje_ni_stack()
    {
        await using var f = _fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Scoped<IEcoPuntosService, EcoPuntosQueFalla>())));
        var r = await f.CreateClient().GetAsync("/api/v1/eco-puntos/politica");
        Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRETO", cuerpo);
        Assert.DoesNotContain("Password", cuerpo);
        Assert.DoesNotContain(" at ", cuerpo);
        Assert.DoesNotContain("InvalidOperationException", cuerpo);
    }
}
