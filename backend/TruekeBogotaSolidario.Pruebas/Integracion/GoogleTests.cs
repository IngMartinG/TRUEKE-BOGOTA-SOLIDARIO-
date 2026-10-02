using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class GoogleTests : IClassFixture<FabricaApi>
{
    /// <summary>Simula a Google: solo los tokens "emitidos" en la prueba son válidos.</summary>
    private sealed class GoogleFalso : IValidadorGoogle
    {
        public ConcurrentDictionary<string, IdentidadGoogle> Tokens { get; } = new();
        public bool Habilitado => true;
        public Task<IdentidadGoogle?> ValidarAsync(string idToken)
            => Task.FromResult(Tokens.TryGetValue(idToken, out var id) ? id : null);
    }

    private readonly WebApplicationFactory<Program> _f;
    private readonly GoogleFalso _google = new();

    public GoogleTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IValidadorGoogle>(_google))));

    private string Emitir(string correo, bool verificado = true, string? sub = null, string nombre = "Laura Gómez")
    {
        var token = "google-" + Guid.NewGuid().ToString("N");
        _google.Tokens[token] = new IdentidadGoogle(sub ?? Guid.NewGuid().ToString("N"), correo, verificado, nombre);
        return token;
    }

    private Task<HttpResponseMessage> GoogleAsync(string idToken, bool acepto = true)
        => _f.CreateClient().PostAsJsonAsync("/api/v1/auth/google", new { idToken, aceptoPoliticaDatos = acepto });

    private static string Correo() => $"g{Guid.NewGuid():N}@gmail.test";

    [Fact]
    public async Task Cuenta_nueva_exige_consentimiento_y_queda_verificada_sin_clave()
    {
        var correo = Correo();
        Assert.Equal(HttpStatusCode.BadRequest, (await GoogleAsync(Emitir(correo), acepto: false)).StatusCode);

        var r = await GoogleAsync(Emitir(correo));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var cuerpo = await r.Content.ReadFromJsonAsync<JsonElement>();
        var u = cuerpo.GetProperty("usuario");
        Assert.True(u.GetProperty("correoVerificado").GetBoolean());
        Assert.False(u.GetProperty("tieneClave").GetBoolean());
        Assert.True(u.GetProperty("vinculadoGoogle").GetBoolean());

        // puede operar de inmediato
        var token = cuerpo.GetProperty("token").GetString()!;
        await Api.CrearPublicacionAsync(Api.ConToken(_f, token));
    }

    [Fact]
    public async Task Segundo_inicio_con_la_misma_cuenta_de_Google_es_200_y_mismo_usuario()
    {
        var correo = Correo();
        var sub = Guid.NewGuid().ToString("N");
        var primero = (await (await GoogleAsync(Emitir(correo, sub: sub))).Content.ReadFromJsonAsync<SesionMinDto>())!;
        var r = await GoogleAsync(Emitir(correo, sub: sub), acepto: false);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(primero.Usuario.Id, (await r.Content.ReadFromJsonAsync<SesionMinDto>())!.Usuario.Id);
    }

    [Fact]
    public async Task Vincula_una_cuenta_verificada_existente_y_conserva_su_clave()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "existente"); // verificada
        var r = await GoogleAsync(Emitir(ses.Usuario.Correo), acepto: false);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(ses.Usuario.Id, (await r.Content.ReadFromJsonAsync<SesionMinDto>())!.Usuario.Id);
        Assert.Equal(HttpStatusCode.OK, (await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { correo = ses.Usuario.Correo, clave = Api.ClaveValida })).StatusCode);
    }

    [Fact]
    public async Task Vincular_una_cuenta_NO_verificada_elimina_la_clave_del_posible_impostor()
    {
        var correo = Correo();
        // alguien registró ese correo (que no es suyo) con una clave y nunca lo verificó
        (await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        { nombreCompleto = "Impostor Prueba", localidad = "Usme", correo, clave = Api.ClaveValida, aceptoPoliticaDatos = true })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await GoogleAsync(Emitir(correo))).StatusCode);   // el verdadero dueño entra con Google
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { correo, clave = Api.ClaveValida })).StatusCode);                          // la clave del impostor ya no sirve
    }

    [Fact]
    public async Task Rechaza_tokens_invalidos_o_con_correo_no_verificado()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync("token-que-google-no-emitio-123456")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync(Emitir(Correo(), verificado: false))).StatusCode);
    }

    [Fact]
    public async Task Cuenta_solo_Google_puede_crear_una_clave_sin_clave_actual()
    {
        var correo = Correo();
        var ses = (await (await GoogleAsync(Emitir(correo))).Content.ReadFromJsonAsync<SesionMinDto>())!;
        var r = await Api.ConToken(_f, ses.Token).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new { claveNueva = "MiClave2026" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = "MiClave2026" })).StatusCode);
    }

    [Fact]
    public async Task Sin_ClientId_configurado_Google_esta_deshabilitado()
    {
        await using var f = new FabricaApi();
        var r = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/google", new { idToken = "cualquier-token-de-prueba-123", aceptoPoliticaDatos = true });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
