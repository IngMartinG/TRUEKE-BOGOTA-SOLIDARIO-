using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class DenunciasTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public DenunciasTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> SuperAsync()
        => Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private static Task<HttpResponseMessage> DenunciarAsync(HttpClient c, string tipo, Guid objetivo, string motivo = "Fraude", string? detalle = null)
        => c.PostAsJsonAsync("/api/v1/denuncias", new { tipo, objetivoId = objetivo, motivo, detalle });

    private static async Task<JsonElement> GrupoAsync(HttpClient moderador, Guid objetivo, string estado = "Pendiente")
        => (await moderador.GetFromJsonAsync<JsonElement>($"/api/v1/admin/denuncias?estado={estado}"))
            .EnumerateArray().Single(g => g.GetProperty("objetivoId").GetGuid() == objetivo);

    [Fact]
    public async Task Varias_denuncias_se_agrupan_y_ocultar_el_contenido_resuelve_todas()
    {
        var vendedor = await Api.RegistrarAsync(_fabrica, "estafador");
        var pub = await Api.CrearPublicacionAsync(vendedor);
        var a = await Api.RegistrarAsync(_fabrica, "vecinaA");
        var b = await Api.RegistrarAsync(_fabrica, "vecinoB");

        Assert.Equal(HttpStatusCode.Created, (await DenunciarAsync(a, "Publicacion", pub, "Fraude", "Pide pago por adelantado")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await DenunciarAsync(b, "Publicacion", pub, "ArticuloProhibido")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await DenunciarAsync(a, "Publicacion", pub)).StatusCode);   // una por persona

        var super = await SuperAsync();
        var g = await GrupoAsync(super, pub);
        Assert.Equal(2, g.GetProperty("total").GetInt32());
        Assert.Contains("Pide pago por adelantado", g.GetProperty("detalles").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Bicicleta usada", g.GetProperty("vistaPrevia").GetString());

        var resolver = await super.PostAsJsonAsync($"/api/v1/admin/denuncias/{g.GetProperty("denunciaId").GetGuid()}/resolver",
            new { accion = "OcultarContenido", nota = "Publicación fraudulenta" });
        Assert.Equal(HttpStatusCode.NoContent, resolver.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode); // ya no es pública
        Assert.Equal(2, (await GrupoAsync(super, pub, "Resuelta")).GetProperty("total").GetInt32());
        // denunciantes y autor fueron avisados
        Assert.Equal(1, await a.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));
        var avisos = await vendedor.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones");
        Assert.Contains(avisos.GetProperty("items").EnumerateArray(), n => n.GetProperty("tipo").GetString() == "PublicacionOcultada");
    }

    [Fact]
    public async Task Descartar_no_toca_el_contenido()
    {
        var autor = await Api.RegistrarAsync(_fabrica, "autor");
        var pub = await Api.CrearPublicacionAsync(autor);
        var a = await Api.RegistrarAsync(_fabrica, "quejoso");
        await DenunciarAsync(a, "Publicacion", pub, "Spam");
        var super = await SuperAsync();
        var g = await GrupoAsync(super, pub);
        Assert.Equal(HttpStatusCode.NoContent, (await super.PostAsJsonAsync($"/api/v1/admin/denuncias/{g.GetProperty("denunciaId").GetGuid()}/resolver",
            new { accion = "Descartar" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);
    }

    [Fact]
    public async Task Denunciar_un_mensaje_solo_puede_un_participante_y_ocultarlo_lo_reemplaza_por_un_aviso()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "duenio");
        var otro = await Api.RegistrarAsync(_fabrica, "acosador");
        var pub = await Api.CrearPublicacionAsync(duenio);
        var s = await (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Hola" })).Content.ReadFromJsonAsync<JsonElement>();
        var conv = s.GetProperty("conversacionId").GetGuid();
        var enviado = await (await otro.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "Mensaje ofensivo" })).Content.ReadFromJsonAsync<JsonElement>();
        var mensajeId = enviado.GetProperty("id").GetGuid();

        var tercero = await Api.RegistrarAsync(_fabrica, "tercero");
        Assert.Equal(HttpStatusCode.NotFound, (await DenunciarAsync(tercero, "Mensaje", mensajeId, "Acoso")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DenunciarAsync(otro, "Mensaje", mensajeId, "Acoso")).StatusCode);   // el propio
        Assert.Equal(HttpStatusCode.Created, (await DenunciarAsync(duenio, "Mensaje", mensajeId, "Acoso")).StatusCode);

        var super = await SuperAsync();
        var g = await GrupoAsync(super, mensajeId);
        (await super.PostAsJsonAsync($"/api/v1/admin/denuncias/{g.GetProperty("denunciaId").GetGuid()}/resolver",
            new { accion = "OcultarContenido", nota = "Lenguaje ofensivo" })).EnsureSuccessStatusCode();

        var mensajes = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/conversaciones/{conv}/mensajes");
        var oculto = mensajes.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == mensajeId);
        Assert.True(oculto.GetProperty("oculto").GetBoolean());
        Assert.DoesNotContain("ofensivo", oculto.GetProperty("texto").GetString());
    }

    [Fact]
    public async Task Reglas_de_creacion_y_permisos()
    {
        var autor = await Api.RegistrarAsync(_fabrica, "propia");
        var pub = await Api.CrearPublicacionAsync(autor);
        Assert.Equal(HttpStatusCode.BadRequest, (await DenunciarAsync(autor, "Publicacion", pub)).StatusCode);              // la propia
        Assert.Equal(HttpStatusCode.NotFound, (await DenunciarAsync(autor, "Publicacion", Guid.NewGuid())).StatusCode);    // inexistente
        Assert.Equal(HttpStatusCode.NotFound, (await DenunciarAsync(autor, "Usuario", Guid.NewGuid(), "Spam")).StatusCode); // usuario inexistente
        Assert.Equal(HttpStatusCode.Unauthorized, (await DenunciarAsync(_fabrica.CreateClient(), "Publicacion", pub)).StatusCode);

        var cliente = await Api.RegistrarAsync(_fabrica, "cliente");
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/admin/denuncias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync($"/api/v1/admin/denuncias/{Guid.NewGuid()}/resolver", new { accion = "Descartar" })).StatusCode);

        // motivo "Otro" exige detalle
        Assert.Equal(HttpStatusCode.BadRequest, (await DenunciarAsync(cliente, "Publicacion", pub, "Otro")).StatusCode);
    }
}
