using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Autorización de datos y términos en casillas separadas (Ley 1581), con la versión aceptada guardada; si cambia una
/// versión, la cuenta solo gestiona lo básico hasta aceptarla de nuevo.
/// </summary>
public class LegalTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public LegalTests(FabricaApi f) => _f = f;

    private static async Task<string?> CodigoAsync(HttpResponseMessage r)
        => (await r.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("codigo", out var c) ? c.GetString() : null;

    [Fact]
    public async Task Registro_exige_las_dos_casillas_y_guarda_las_versiones_aceptadas()
    {
        var correo = $"legal{Guid.NewGuid():N}@trueke.test";
        object Cuerpo(bool datos, bool terminos) => new
        {
            nombreCompleto = "Legal Prueba", localidad = "Suba", correo, clave = Api.ClaveValida, fechaNacimiento = Api.FechaNacimientoAdulta,
            aceptoPoliticaDatos = datos, aceptoTerminos = terminos
        };
        var sinTerminos = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", Cuerpo(true, false));
        Assert.Equal(HttpStatusCode.BadRequest, sinTerminos.StatusCode);
        Assert.Contains("aceptoTerminos", await sinTerminos.Content.ReadAsStringAsync());

        var ok = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", Cuerpo(true, true));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var usuario = (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("usuario");
        Assert.Equal("2026-10-30", usuario.GetProperty("politicaDatosVersion").GetString());
        Assert.Equal("2026-10-30", usuario.GetProperty("terminosVersion").GetString());
    }

    [Fact]
    public async Task Con_una_version_anterior_solo_gestiona_su_cuenta_hasta_aceptar_la_vigente()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "versionVieja");
        using (var scope = _f.Services.CreateScope())
        {
            // Simula que aceptó los términos anteriores (y ninguna versión de términos, como las cuentas de antes)
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            var u = await db.Usuarios.SingleAsync(x => x.Id == ses.Usuario.Id);
            db.Entry(u).Property(x => x.PoliticaDatosVersion).CurrentValue = "2026-10";
            db.Entry(u).Property(x => x.TerminosVersion).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        var login = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = ses.Usuario.Correo, clave = Api.ClaveValida });
        var cliente = Api.ConToken(_f, (await login.Content.ReadFromJsonAsync<SesionMinDto>())!.Token);

        var bloqueada = await cliente.GetAsync("/api/v1/conversaciones");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);
        Assert.Equal("terminos_desactualizados", await CodigoAsync(bloqueada));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/usuarios/yo")).StatusCode);              // ve su perfil
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/usuarios/yo/datos")).StatusCode);     // habeas data

        var sinMarcar = await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/aceptar-legal", new { aceptoPoliticaDatos = true, aceptoTerminos = false });
        Assert.Equal(HttpStatusCode.BadRequest, sinMarcar.StatusCode);
        var acepta = await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/aceptar-legal", new { aceptoPoliticaDatos = true, aceptoTerminos = true });
        Assert.Equal(HttpStatusCode.OK, acepta.StatusCode);
        Assert.Equal("2026-10-30", (await acepta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("terminosVersion").GetString());

        // Con un token nuevo (el front refresca la sesión) ya puede usar todo
        var nuevo = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = ses.Usuario.Correo, clave = Api.ClaveValida });
        var alDia = Api.ConToken(_f, (await nuevo.Content.ReadFromJsonAsync<SesionMinDto>())!.Token);
        Assert.Equal(HttpStatusCode.OK, (await alDia.GetAsync("/api/v1/conversaciones")).StatusCode);
    }

    [Fact]
    public async Task La_configuracion_publica_trae_las_versiones_y_al_responsable()
    {
        var cfg = await _f.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/configuracion");
        var legal = cfg.GetProperty("legal");
        Assert.Equal("2026-10-30", legal.GetProperty("versionTerminos").GetString());
        Assert.Equal("2026-10-30", legal.GetProperty("fechaVigencia").GetString());
        Assert.True(legal.TryGetProperty("responsableNombre", out _));
    }
}
