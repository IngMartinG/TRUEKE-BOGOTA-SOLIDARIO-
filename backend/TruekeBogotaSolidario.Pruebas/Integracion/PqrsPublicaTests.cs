using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>PQRS pública: cualquier persona radica sin cuenta, recibe el radicado en su correo y consulta con radicado + correo.</summary>
public class PqrsPublicaTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public PqrsPublicaTests(FabricaApi f) => _f = f;

    private Task<HttpResponseMessage> RadicarAsync(string correo, string tipo = "Peticion", string nombre = "María José O'Neill")
        => _f.CreateClient().PostAsJsonAsync("/api/v1/pqr/publica",
            new { nombre, correo, tipo, asunto = "No puedo crear mi cuenta", descripcion = "Al registrarme aparece un error y no avanza." });

    [Fact]
    public async Task Sin_cuenta_se_radica_se_consulta_con_el_correo_y_la_respuesta_llega_al_correo()
    {
        var correo = $"pqr{Guid.NewGuid():N}@trueke.test";
        var r = await RadicarAsync(correo, "Consulta");
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pqr = await r.Content.ReadFromJsonAsync<JsonElement>();
        var radicado = pqr.GetProperty("radicado").GetString()!;
        Assert.Equal("Consulta", pqr.GetProperty("tipo").GetString());
        var acuse = Assert.Single(Api.Correos(_f).Para(correo), m => m.Asunto.Contains(radicado));
        Assert.Contains("10 días hábiles", acuse.Texto);
        // Cualquiera puede escribir el correo de otra persona: el acuse no repite el texto de quien radicó.
        Assert.DoesNotContain("No puedo crear mi cuenta", acuse.Texto);
        Assert.DoesNotContain("María", acuse.Texto);
        Assert.Contains("Si no enviaste esta solicitud", acuse.Texto);

        var anonimo = _f.CreateClient();
        var ok = await anonimo.PostAsJsonAsync("/api/v1/pqr/consultar", new { radicado = radicado.ToLowerInvariant(), correo = correo.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Abierta", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
        // Otro correo: el mismo 404 que un radicado inexistente (no se revela cuáles existen)
        var ajeno = await anonimo.PostAsJsonAsync("/api/v1/pqr/consultar", new { radicado, correo = "otra@trueke.test" });
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
        var inexistente = await anonimo.PostAsJsonAsync("/api/v1/pqr/consultar", new { radicado = "PQR-20260101-ABCDEF", correo });
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal((await ajeno.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString(),
            (await inexistente.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());

        // El equipo la pone en trámite y la responde: los avisos llegan al correo de quien la radicó
        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var cola = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/pqr?tamano=50");
        var item = cola.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("radicado").GetString() == radicado);
        Assert.True(item.GetProperty("publica").GetBoolean());
        Assert.Equal(correo, item.GetProperty("correoUsuario").GetString());
        var id = item.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await super.PostAsync($"/api/v1/admin/pqr/{id}/en-tramite", null)).StatusCode);
        Assert.Contains(Api.Correos(_f).Para(correo), m => m.Asunto.Contains("en trámite"));
        var cola2 = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/pqr?tamano=50"); // sigue entre las pendientes
        Assert.Contains(cola2.GetProperty("items").EnumerateArray(), i => i.GetProperty("radicado").GetString() == radicado);

        var resp = await super.PostAsJsonAsync($"/api/v1/admin/pqr/{id}/responder", new { respuesta = "Revisamos tu cuenta y ya puedes registrarte." });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains(Api.Correos(_f).Para(correo), m => m.Asunto.StartsWith("Respuesta") && m.Texto.Contains("ya puedes registrarte"));
        var final = await anonimo.PostAsJsonAsync("/api/v1/pqr/consultar", new { radicado, correo });
        Assert.Equal("Respondida", (await final.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Rechaza_retracto_nombres_invalidos_y_limita_por_correo()
    {
        var correo = $"lim{Guid.NewGuid():N}@trueke.test";
        Assert.Equal(HttpStatusCode.BadRequest, (await RadicarAsync(correo, "Retracto")).StatusCode);
        var nombre = await RadicarAsync(correo, nombre: "Ana2");
        Assert.Equal(HttpStatusCode.BadRequest, nombre.StatusCode);
        Assert.Contains("nombre", await nombre.Content.ReadAsStringAsync());

        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Created, (await RadicarAsync(correo)).StatusCode);
        var sexta = await RadicarAsync(correo);
        Assert.Equal(HttpStatusCode.BadRequest, sexta.StatusCode);
        Assert.Contains("varias solicitudes", await sexta.Content.ReadAsStringAsync());
    }
}
