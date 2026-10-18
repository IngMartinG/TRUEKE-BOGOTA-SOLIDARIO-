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

        // Una fecha imposible no pasa (y no marca la cuenta); una válida sí, y no se puede cambiar después.
        // La fecha de menor de edad deja la cuenta en revisión: ver Cuenta_existente_que_declara_ser_menor_queda_en_revision.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = Hoy(sumarDias: 1) })).StatusCode);
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

    /// <summary>Cuenta creada antes de la regla (sin fecha de nacimiento), con una publicación activa, y su sesión.</summary>
    private async Task<(HttpClient Cliente, Guid UsuarioId, string Correo, Guid PublicacionId)> CuentaAntiguaAsync(string nombre)
    {
        var ses = await Api.RegistrarSesionAsync(_f, nombre);
        var pub = await Api.CrearPublicacionAsync(Api.ConToken(_f, ses.Token));
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            var u = await db.Usuarios.SingleAsync(x => x.Id == ses.Usuario.Id);
            db.Entry(u).Property(x => x.FechaNacimiento).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        return (await IngresarAsync(ses.Usuario.Correo), ses.Usuario.Id, ses.Usuario.Correo, pub);
    }

    private async Task<HttpClient> IngresarAsync(string correo)
    {
        var login = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = Api.ClaveValida });
        return Api.ConToken(_f, (await login.Content.ReadFromJsonAsync<SesionMinDto>())!.Token);
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage r)
        => (await r.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("codigo", out var c) ? c.GetString() : null;

    [Fact]
    public async Task Cuenta_existente_que_declara_ser_menor_queda_en_revision()
    {
        var (cliente, id, correo, pub) = await CuentaAntiguaAsync("menor");

        // Primer intento con fecha de menor: 403 con código estable, y la fecha no se guarda
        var menor = await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = Hoy(-15) });
        Assert.Equal(HttpStatusCode.Forbidden, menor.StatusCode);
        Assert.Equal("menor_de_edad_detectado", await CodigoAsync(menor));

        // La marca invalida el token vigente (dice "edad pendiente"): el front refresca y recibe uno con "edad menor"
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/v1/usuarios/yo")).StatusCode);
        var nueva = await IngresarAsync(correo);

        // El reintento con una fecha adulta también responde 403: ya no se puede "corregir" para pasar el filtro
        var adulta = await nueva.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = "1990-01-01" });
        Assert.Equal(HttpStatusCode.Forbidden, adulta.StatusCode);
        Assert.Equal("menor_de_edad_detectado", await CodigoAsync(adulta));

        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            var u = await db.Usuarios.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.NotNull(u.MenorDetectadoUtc);
            Assert.Null(u.FechaNacimiento);
            Assert.True((await db.Publicaciones.AsNoTracking().SingleAsync(p => p.Id == pub)).EstaOculta); // fuera del catálogo
        }

        // La moderación recibe el aviso y la encuentra con el filtro
        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var avisos = await super.GetStringAsync("/api/v1/notificaciones");
        Assert.Contains("MenorDetectado", avisos);
        Assert.Contains(id.ToString(), avisos);
        var lista = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/usuarios?soloMenorDetectado=true&tamano=50");
        var fila = lista.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        Assert.NotEqual(JsonValueKind.Null, fila.GetProperty("menorDetectadoUtc").ValueKind);

        // Lo demás responde el código nuevo; habeas data y PQRS siguen funcionando
        var bloqueada = await nueva.GetAsync("/api/v1/usuarios/yo/bloqueados");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);
        Assert.Equal("menor_de_edad_detectado", await CodigoAsync(bloqueada));
        Assert.True((await nueva.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("menorDetectado").GetBoolean());
        Assert.Contains("\"menorDetectado\":true", await nueva.GetStringAsync("/api/v1/usuarios/yo/datos"));
        Assert.Equal(HttpStatusCode.Created, (await nueva.PostAsJsonAsync("/api/v1/pqr",
            new { tipo = "Consulta", asunto = "Quiero borrar mis datos", descripcion = "Por favor eliminen mi cuenta y mis datos." })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await nueva.PostAsJsonAsync("/api/v1/usuarios/yo/eliminar", new { confirmacion = "ELIMINAR", clave = Api.ClaveValida })).StatusCode);
    }

    [Fact]
    public async Task La_moderacion_descarta_la_marca_si_fue_un_error_y_la_cuenta_registra_su_fecha()
    {
        var (cliente, id, correo, pub) = await CuentaAntiguaAsync("digito");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = Hoy(-12) })).StatusCode);

        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await super.PostAsJsonAsync($"/api/v1/admin/usuarios/{id}/descartar-menor", new { motivo = "" })).StatusCode);
        var descartar = await super.PostAsJsonAsync($"/api/v1/admin/usuarios/{id}/descartar-menor",
            new { motivo = "Envió su cédula por PQRS: digitó mal el año." });
        Assert.Equal(HttpStatusCode.OK, descartar.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await descartar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("menorDetectadoUtc").ValueKind);
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            Assert.False((await db.Publicaciones.AsNoTracking().SingleAsync(p => p.Id == pub)).EstaOculta);
        }

        var nueva = await IngresarAsync(correo);
        Assert.Equal(HttpStatusCode.OK,
            (await nueva.PutAsJsonAsync("/api/v1/usuarios/yo/fecha-nacimiento", new { fechaNacimiento = "1991-07-30" })).StatusCode);
    }
}
