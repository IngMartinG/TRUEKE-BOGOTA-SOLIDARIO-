using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Suspensión de cuentas, búsqueda de usuarios y pagos en revisión.</summary>
public class AdminCuentasTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public AdminCuentasTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> SuperAsync() => Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    [Fact]
    public async Task Suspender_cierra_sesiones_impide_entrar_y_oculta_su_contenido()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "infractor");
        var infractor = Api.ConToken(_fabrica, ses.Token);
        var pub = await Api.CrearPublicacionAsync(infractor);
        var super = await SuperAsync();

        var r = await super.PostAsJsonAsync($"/api/v1/admin/usuarios/{ses.Usuario.Id}/suspender", new { motivo = "Publicaciones fraudulentas", dias = 7 });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("suspendido").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await infractor.GetAsync("/api/v1/usuarios/yo")).StatusCode);           // sesión cortada
        var login = await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = ses.Usuario.Correo, clave = Api.ClaveValida });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("suspendida", await login.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/usuarios/{ses.Usuario.Id}/perfil")).StatusCode);
        Assert.Contains(Api.Correos(_fabrica).Para(ses.Usuario.Correo), m => m.Asunto.Contains("suspendida"));

        Assert.Equal(HttpStatusCode.OK, (await super.PostAsync($"/api/v1/admin/usuarios/{ses.Usuario.Id}/reactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { correo = ses.Usuario.Correo, clave = Api.ClaveValida })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);
    }

    [Fact]
    public async Task Jerarquia_un_admin_no_suspende_admins_ni_superusuarios_y_un_cliente_nada()
    {
        var super = await SuperAsync();
        var sesAdmin = await Api.RegistrarSesionAsync(_fabrica, "moderadora");
        (await super.PatchAsJsonAsync($"/api/v1/admin/usuarios/{sesAdmin.Usuario.Id}/rol", new { rol = "Administrador" })).EnsureSuccessStatusCode();
        var admin = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, sesAdmin.Usuario.Correo, Api.ClaveValida));
        var otroAdmin = await Api.RegistrarSesionAsync(_fabrica, "otroadmin");
        (await super.PatchAsJsonAsync($"/api/v1/admin/usuarios/{otroAdmin.Usuario.Id}/rol", new { rol = "Administrador" })).EnsureSuccessStatusCode();
        var cliente = await Api.RegistrarSesionAsync(_fabrica, "clientito");

        var superId = (await super.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/admin/usuarios/{otroAdmin.Usuario.Id}/suspender", new { motivo = "Prueba" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/admin/usuarios/{superId}/suspender", new { motivo = "Prueba" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/usuarios/{sesAdmin.Usuario.Id}/suspender", new { motivo = "Yo" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/usuarios/{cliente.Usuario.Id}/suspender", new { motivo = "Spam" })).StatusCode);

        var c = Api.ConToken(_fabrica, (await Api.RegistrarSesionAsync(_fabrica, "curioso")).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/admin/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/admin/pagos/TRK-x/reembolsado", new { nota = "nada" })).StatusCode); // solo SuperUsuario
    }

    [Fact]
    public async Task Buscar_usuarios_por_correo_o_nombre()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "buscable");
        var super = await SuperAsync();
        var porCorreo = await super.GetFromJsonAsync<JsonElement>($"/api/v1/admin/usuarios?texto={Uri.EscapeDataString(ses.Usuario.Correo)}");
        var u = Assert.Single(porCorreo.GetProperty("items").EnumerateArray());
        Assert.Equal(ses.Usuario.Correo, u.GetProperty("correo").GetString());
        var porNombre = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/usuarios?texto=BUSCABLE");
        Assert.True(porNombre.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task Pagos_en_revision_se_listan_y_el_reembolso_queda_registrado()
    {
        // Un destacado pagado cuya publicación se canceló antes de aprobarse → RequiereRevision
        var ses = await Api.RegistrarSesionAsync(_fabrica, "pagadora");
        var c = Api.ConToken(_fabrica, ses.Token);
        var pub = await Api.CrearPublicacionAsync(c);
        var ini = await c.PostAsJsonAsync("/api/v1/pagos/iniciar", new { concepto = "Destacar", publicacionId = pub });
        var referencia = (await ini.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("referencia").GetString()!;
        (await c.PostAsJsonAsync($"/api/v1/publicaciones/{pub}/cancelar", new { motivo = "Ya la vendí" })).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/pagos/{referencia}/simular?aprobado=true", null)).EnsureSuccessStatusCode();

        var super = await SuperAsync();
        var revision = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/pagos?estado=RequiereRevision&tamano=50");
        Assert.Contains(revision.GetProperty("items").EnumerateArray(), p => p.GetProperty("referencia").GetString() == referencia);

        var r = await super.PostAsJsonAsync($"/api/v1/admin/pagos/{referencia}/reembolsado", new { nota = "Reembolso Wompi #123" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("Reembolsado", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostAsJsonAsync($"/api/v1/admin/pagos/{referencia}/reembolsado", new { nota = "otra vez" })).StatusCode);
    }
}
