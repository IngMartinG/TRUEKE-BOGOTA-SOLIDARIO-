using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Ley 1581 de 2012: autorización, acceso (exportar) y supresión (eliminar cuenta).</summary>
public class HabeasDataTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public HabeasDataTests(FabricaApi fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Registrarse_sin_aceptar_la_politica_es_400()
    {
        var r = await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        { nombreCompleto = "Sin Consentimiento", localidad = "Bosa", correo = $"nc{Guid.NewGuid():N}@trueke.test", clave = Api.ClaveValida, aceptoPoliticaDatos = false });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("política de tratamiento de datos", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Exportar_trae_todos_mis_datos_y_ninguno_ajeno()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "exporta");
        var yo = Api.ConToken(_fabrica, ses.Token);
        var otro = await Api.RegistrarAsync(_fabrica, "vecino");
        var miPub = await Api.CrearPublicacionAsync(yo);
        var suPub = await Api.CrearPublicacionAsync(otro);
        (await yo.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = suPub, mensaje = "Mensaje mío" })).EnsureSuccessStatusCode();
        (await yo.PostAsJsonAsync($"/api/v1/publicaciones/{suPub}/comentarios", new { texto = "Comentario mío" })).EnsureSuccessStatusCode();
        (await otro.PostAsJsonAsync($"/api/v1/publicaciones/{miPub}/comentarios", new { texto = "Comentario AJENO" })).EnsureSuccessStatusCode();

        var r = await yo.GetAsync("/api/v1/usuarios/yo/datos");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var d = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ses.Usuario.Correo, d.GetProperty("perfil").GetProperty("correo").GetString());
        Assert.Equal("2026-10", d.GetProperty("politicaDatosVersion").GetString());
        Assert.Single(d.GetProperty("publicaciones").EnumerateArray());
        Assert.Single(d.GetProperty("solicitudesEnviadas").EnumerateArray());
        Assert.Equal("Comentario mío", Assert.Single(d.GetProperty("comentarios").EnumerateArray()).GetProperty("texto").GetString());
        Assert.Equal("Mensaje mío", Assert.Single(d.GetProperty("mensajesEnviados").EnumerateArray()).GetProperty("texto").GetString());
        var json = d.GetRawText();
        Assert.DoesNotContain("Comentario AJENO", json);
        Assert.DoesNotContain("claveHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eliminar_exige_confirmacion_y_clave()
    {
        var c = await Api.RegistrarAsync(_fabrica, "dudoso");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/usuarios/yo/eliminar", new { confirmacion = "eliminar", clave = Api.ClaveValida })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/usuarios/yo/eliminar", new { confirmacion = "ELIMINAR", clave = "Equivocada1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/usuarios/yo/eliminar", new { confirmacion = "ELIMINAR" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode); // sigue viva
    }

    [Fact]
    public async Task Eliminar_anonimiza_revoca_libera_el_correo_y_limpia_lo_publico()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "seva");
        var yo = Api.ConToken(_fabrica, ses.Token);
        var otro = await Api.RegistrarAsync(_fabrica, "seQueda");
        var miPub = await Api.CrearPublicacionAsync(yo);
        var suPub = await Api.CrearPublicacionAsync(otro);
        (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = miPub, mensaje = "Me interesa" })).EnsureSuccessStatusCode();   // recibida
        (await yo.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = suPub, mensaje = "Y a mí la tuya" })).EnsureSuccessStatusCode(); // enviada
        (await yo.PostAsJsonAsync($"/api/v1/publicaciones/{suPub}/comentarios", new { texto = "Mi comentario público" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await yo.PostAsJsonAsync("/api/v1/usuarios/yo/eliminar", new { confirmacion = "ELIMINAR", clave = Api.ClaveValida })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await yo.GetAsync("/api/v1/usuarios/yo")).StatusCode);                       // token revocado
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { correo = ses.Usuario.Correo, clave = Api.ClaveValida })).StatusCode);                                         // no puede entrar
        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{miPub}")).StatusCode); // retirada
        Assert.Equal(HttpStatusCode.OK, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{suPub}")).StatusCode);       // la del otro, libre
        Assert.DoesNotContain("Mi comentario público", await _fabrica.CreateClient().GetStringAsync($"/api/v1/publicaciones/{suPub}/comentarios"));

        var recibidas = await otro.GetStringAsync("/api/v1/solicitudes/recibidas");
        Assert.Contains("Cancelada", recibidas);
        Assert.Contains("Usuario E.", recibidas);   // nombre anonimizado
        Assert.DoesNotContain("seva", recibidas);

        // el correo queda libre para una cuenta nueva
        var nueva = await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        { nombreCompleto = "Vuelvo Despues", localidad = "Suba", correo = ses.Usuario.Correo, clave = Api.ClaveValida, aceptoPoliticaDatos = true });
        Assert.Equal(HttpStatusCode.Created, nueva.StatusCode);
    }

    [Fact]
    public async Task El_unico_SuperUsuario_no_puede_eliminarse()
    {
        var super = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var r = await super.PostAsJsonAsync("/api/v1/usuarios/yo/eliminar", new { confirmacion = "ELIMINAR", clave = FabricaApi.ClaveSuper });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
