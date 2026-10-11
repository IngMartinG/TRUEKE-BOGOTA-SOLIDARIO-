using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
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

    /// <summary>Simula el servidor de fotos de Google: responde una imagen válida y registra qué se pidió.</summary>
    private sealed class FotosGoogleFalso : HttpMessageHandler
    {
        public List<Uri> Pedidas { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Pedidas) Pedidas.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(AlmacenFalso.Png()) });
        }
    }

    private const string FotoGoogle = "https://lh3.googleusercontent.com/a/ACg8ocFoto=s96-c";
    private readonly WebApplicationFactory<Program> _f;
    private readonly GoogleFalso _google = new();
    private readonly FotosGoogleFalso _fotos = new();

    public GoogleTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<IValidadorGoogle>(_google));
            s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(new AlmacenFalso()));
            s.AddHttpClient(ImportadorFotoGoogle.ClienteHttp).ConfigurePrimaryHttpMessageHandler(() => _fotos);
        }));

    private string Emitir(string correo, bool verificado = true, string? sub = null, string nombre = "Laura Gómez", string? foto = FotoGoogle,
        TimeSpan? antiguedad = null)
    {
        var token = "google-" + Guid.NewGuid().ToString("N");
        _google.Tokens[token] = new IdentidadGoogle(sub ?? Guid.NewGuid().ToString("N"), correo, verificado, nombre, foto,
            DateTimeOffset.UtcNow - (antiguedad ?? TimeSpan.Zero));
        return token;
    }

    private Task<HttpResponseMessage> GoogleAsync(string idToken, bool acepto = true, string? codigo = null)
        => _f.CreateClient().PostAsJsonAsync("/api/v1/auth/google", new { idToken, aceptoPoliticaDatos = acepto, codigoDosFactores = codigo });

    private static byte[] DesdeBase32(string s)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in s)
        {
            buffer = (buffer << 5) | alfabeto.IndexOf(c);
            bits += 5;
            if (bits >= 8) { bytes.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        return bytes.ToArray();
    }

    /// <summary>Activa 2FA con la sesión dada (confirmando con la contraseña); devuelve el secreto y el paso TOTP ya usado.</summary>
    private static async Task<(byte[] Secreto, long Paso)> ActivarDosFactoresAsync(HttpClient c)
    {
        var cfg = (await (await c.PostAsJsonAsync("/api/v1/auth/2fa/configurar", new { clave = Api.ClaveValida })).Content.ReadFromJsonAsync<ConfiguracionDosFactoresDto>())!;
        var secreto = DesdeBase32(cfg.SecretoBase32);
        var paso = Totp.Paso(DateTimeOffset.UtcNow);
        var r = await c.PostAsJsonAsync("/api/v1/auth/2fa/activar", new { codigo = Totp.Calcular(secreto, paso) });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (secreto, paso);
    }

    private static async Task<string?> CodigoErrorAsync(HttpResponseMessage r)
        => (await r.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("codigo", out var c) ? c.GetString() : null;

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

        // trae su foto de Google (copiada a nuestro almacenamiento, pedida a 512 px) y puede operar de inmediato
        Assert.StartsWith(AlmacenFalso.Base.ToString(), u.GetProperty("fotoUrl").GetString());
        Assert.Contains(_fotos.Pedidas, p => p.AbsoluteUri.EndsWith("=s512-c", StringComparison.Ordinal));
        var token = cuerpo.GetProperty("token").GetString()!;
        await Api.CrearPublicacionAsync(Api.ConToken(_f, token));
    }

    [Fact]
    public async Task Sin_foto_en_Google_la_cuenta_debe_subir_una_antes_de_publicar()
    {
        var r = await GoogleAsync(Emitir(Correo(), foto: null));
        var token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var publicar = await Api.ConToken(_f, token).PostAsJsonAsync("/api/v1/publicaciones",
            new { titulo = "Silla", descripcion = "Silla de madera", categoriaId = 1, modo = "Donacion", condicion = "Usado", localidad = "Suba" });
        Assert.Equal(HttpStatusCode.Forbidden, publicar.StatusCode);
        Assert.Equal("foto_requerida", (await publicar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData("https://lh3.googleusercontent.com/a/x=s96-c", true)]
    [InlineData("http://lh3.googleusercontent.com/a/x", false)]          // sin https
    [InlineData("https://googleusercontent.com.atacante.co/x", false)]  // dominio parecido
    [InlineData("https://169.254.169.254/latest/meta-data", false)]     // red interna (SSRF)
    [InlineData(null, false)]
    public void Solo_se_importan_fotos_de_los_servidores_de_Google(string? url, bool valida)
        => Assert.Equal(valida, ImportadorFotoGoogle.EsFotoDeGoogle(url, out _));

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
    public async Task Con_Google_los_codigos_2FA_incorrectos_bloquean_la_cuenta_igual_que_con_clave()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "googleBloqueo");
        var sub = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, (await GoogleAsync(Emitir(ses.Usuario.Correo, sub: sub), acepto: false)).StatusCode); // vinculada
        var (secreto, paso) = await ActivarDosFactoresAsync(Api.ConToken(_f, ses.Token));

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync(Emitir(ses.Usuario.Correo, sub: sub), false, "000000")).StatusCode);

        // Bloqueada: ni el código correcto sirve hasta que pase el bloqueo (ni con Google ni con clave)
        var bueno = Totp.Calcular(secreto, paso + 1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync(Emitir(ses.Usuario.Correo, sub: sub), false, bueno)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { correo = ses.Usuario.Correo, clave = Api.ClaveValida, codigoDosFactores = bueno })).StatusCode);
    }

    [Fact]
    public async Task Un_codigo_2FA_incorrecto_no_deja_la_cuenta_vinculada_a_ese_Google()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "googleSinVincular");
        await ActivarDosFactoresAsync(Api.ConToken(_f, ses.Token));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync(Emitir(ses.Usuario.Correo), false, "000000")).StatusCode);

        // Si hubiera quedado vinculada, otra cuenta de Google con el mismo correo recibiría un 401 genérico;
        // sin vincular, se le pide el segundo factor como a cualquiera.
        var r = await GoogleAsync(Emitir(ses.Usuario.Correo), acepto: false);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("2fa_requerido", await CodigoErrorAsync(r));
    }

    [Fact]
    public async Task El_2FA_que_puso_un_impostor_en_una_cuenta_sin_verificar_no_bloquea_al_dueno_del_correo()
    {
        var correo = Correo();
        var registro = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        { nombreCompleto = "Impostor Prueba", localidad = "Usme", correo, clave = Api.ClaveValida, aceptoPoliticaDatos = true });
        var tokenImpostor = (await registro.Content.ReadFromJsonAsync<SesionMinDto>())!.Token;
        await ActivarDosFactoresAsync(Api.ConToken(_f, tokenImpostor));

        var r = await GoogleAsync(Emitir(correo), acepto: false);   // el verdadero dueño entra con Google, sin código
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var yo = await Api.ConToken(_f, (await r.Content.ReadFromJsonAsync<SesionMinDto>())!.Token).GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo");
        Assert.False(yo.GetProperty("dosFactoresActivo").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.ConToken(_f, tokenImpostor).GetAsync("/api/v1/usuarios/yo")).StatusCode);
    }

    [Fact]
    public async Task Rechaza_tokens_invalidos_o_con_correo_no_verificado()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync("token-que-google-no-emitio-123456")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GoogleAsync(Emitir(Correo(), verificado: false))).StatusCode);
    }

    [Fact]
    public async Task Cuenta_solo_Google_crea_su_clave_solo_confirmando_con_un_token_de_Google_reciente()
    {
        var correo = Correo();
        var sub = Guid.NewGuid().ToString("N");
        var ses = (await (await GoogleAsync(Emitir(correo, sub: sub))).Content.ReadFromJsonAsync<SesionMinDto>())!;
        var c = Api.ConToken(_f, ses.Token);
        Task<HttpResponseMessage> CrearAsync(string? googleIdToken)
            => c.PostAsJsonAsync("/api/v1/auth/cambiar-clave", new { claveNueva = "MiClave2026", googleIdToken });

        // Con solo el token de acceso (p. ej. robado) no se puede poner una contraseña
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearAsync(Emitir(correo, sub: sub, antiguedad: TimeSpan.FromMinutes(30)))).StatusCode); // viejo
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearAsync(Emitir(Correo()))).StatusCode);                                          // otra cuenta

        var r = await CrearAsync(Emitir(correo, sub: sub));
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
