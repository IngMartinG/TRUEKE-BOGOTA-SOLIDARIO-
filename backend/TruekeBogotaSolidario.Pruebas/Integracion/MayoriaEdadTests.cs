using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>La plataforma es solo para mayores de 18 años: se exige la fecha de nacimiento al crear la cuenta y a las cuentas viejas.</summary>
public class MayoriaEdadTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public MayoriaEdadTests(FabricaApi f) => _f = f;

    private static string Hoy(int sumarAnios = 0, int sumarDias = 0)
        => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).AddYears(sumarAnios).AddDays(sumarDias).ToString("yyyy-MM-dd");

    private Task<HttpResponseMessage> RegistrarAsync(string correo, string? fechaNacimiento)
        => _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar",
            new { nombreCompleto = "Edad Prueba", localidad = "Teusaquillo", correo, clave = Api.ClaveValida, fechaNacimiento, aceptoPoliticaDatos = true, aceptoTerminos = true });

    [Fact]
    public async Task Registro_exige_fecha_y_rechaza_a_menores_sin_guardar_nada()
    {
        var correo = $"edad{Guid.NewGuid():N}@trueke.test";
        var sinFecha = await RegistrarAsync(correo, null);
        Assert.Equal(HttpStatusCode.BadRequest, sinFecha.StatusCode);
        Assert.Contains("fechaNacimiento", await sinFecha.Content.ReadAsStringAsync());

        // Cumple 18 mañana: todavía es menor de edad
        var menor = await RegistrarAsync(correo, Hoy(-18, 1));
        Assert.Equal(HttpStatusCode.BadRequest, menor.StatusCode);
        Assert.Contains("mayores de 18", await menor.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await RegistrarAsync(correo, Hoy(sumarDias: 1))).StatusCode);

        using (var scope = _f.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Usuarios.AnyAsync(u => u.Correo == correo));

        // Cumple 18 hoy: ya puede
        var ok = await RegistrarAsync(correo, Hoy(-18));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(Hoy(-18), (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("usuario").GetProperty("fechaNacimiento").GetString());
    }

    [Fact]
    public async Task Cuenta_vieja_sin_fecha_solo_gestiona_su_cuenta_hasta_registrarla()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "antigua");
        using (var scope = _f.Services.CreateScope())
        {
            // Simula una cuenta creada antes de la regla
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            var u = await db.Usuarios.SingleAsync(x => x.Id == ses.Usuario.Id);
            db.Entry(u).Property(x => x.FechaNacimiento).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        var login = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = ses.Usuario.Correo, clave = Api.ClaveValida });
        var cliente = Api.ConToken(_f, (await login.Content.ReadFromJsonAsync<SesionMinDto>())!.Token);

        // Bloqueada: el 403 trae un código estable para que el front lleve a registrarla
        var bloqueada = await cliente.GetAsync("/api/v1/usuarios/yo/bloqueados");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);
        Assert.Equal("fecha_nacimiento_requerida", (await bloqueada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        await Assert.ThrowsAnyAsync<Exception>(() => Api.CrearPublicacionAsync(cliente));

        // Lo que nunca se bloquea: ver el perfil propio, exportar los datos (habeas data) y los avisos
        var yo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo");
        Assert.Equal(JsonValueKind.Null, yo.GetProperty("fechaNacimiento").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/usuarios/yo/datos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/notificaciones")).StatusCode);

        // Un menor no pasa; una fecha válida sí, y no se puede cambiar después
        Assert.Equal(HttpStatusCode.BadRequest,
            (await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = Hoy(-10) })).StatusCode);
        var registrar = await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = "1985-03-02" });
        Assert.Equal(HttpStatusCode.OK, registrar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = "1980-01-01" })).StatusCode);

        // Con una sesión nueva (el front refresca) ya puede publicar
        var nueva = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo = ses.Usuario.Correo, clave = Api.ClaveValida });
        var conEdad = Api.ConToken(_f, (await nueva.Content.ReadFromJsonAsync<SesionMinDto>())!.Token);
        Assert.NotEqual(Guid.Empty, await Api.CrearPublicacionAsync(conEdad));
    }

    [Fact]
    public async Task La_fecha_de_nacimiento_no_aparece_en_el_perfil_publico()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "privada");
        var perfil = await _f.CreateClient().GetStringAsync($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.DoesNotContain("1990-05-20", perfil);
        Assert.DoesNotContain("nacimiento", perfil, StringComparison.OrdinalIgnoreCase);
    }
}
