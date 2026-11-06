using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// El chat se revisa sin que nadie lo lea: lo ilícito no se envía, el contacto por fuera se envía marcado con un aviso de
/// seguridad, y un moderador solo puede leer el contexto de lo marcado o denunciado.
/// </summary>
public class ChatModeracionTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public ChatModeracionTests(FabricaApi f) => _f = f;

    private async Task<HttpClient> SuperAsync() => Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private async Task<(HttpClient Duenio, HttpClient Otro, Guid Conversacion)> ConversacionAsync()
    {
        var duenio = await Api.RegistrarAsync(_f, "duenia");
        var otro = await Api.RegistrarAsync(_f, "interesado");
        var pub = await Api.CrearPublicacionAsync(duenio);
        var r = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Me la cambias por un libro?" });
        return (duenio, otro, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conversacionId").GetGuid());
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient c, Guid conv, string texto)
        => c.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto });

    [Fact]
    public async Task Un_articulo_ilicito_no_se_envia()
    {
        var (duenio, otro, conv) = await ConversacionAsync();
        var r = await EnviarAsync(duenio, conv, "Si quieres también tengo marihuana");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("no se envi", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        var mensajes = await otro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes");
        Assert.DoesNotContain(mensajes!, m => m.Texto.Contains("marihuana"));
    }

    [Fact]
    public async Task Un_telefono_se_envia_con_aviso_y_el_moderador_ve_el_contexto_y_lo_oculta()
    {
        var (duenio, otro, conv) = await ConversacionAsync();
        Assert.Equal(HttpStatusCode.Created, (await EnviarAsync(duenio, conv, "¿El sábado te sirve?")).StatusCode);
        var r = await EnviarAsync(otro, conv, "Mejor escríbeme al 310 555 1234 y cuadramos");
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var enviado = (await r.Content.ReadFromJsonAsync<MensajeChatDto>())!;
        Assert.True(enviado.AvisoSeguridad);
        var recibido = (await duenio.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Single(m => m.Id == enviado.Id);
        Assert.True(recibido.AvisoSeguridad); // quien lo recibe también ve el aviso de seguridad

        // Un cliente no puede usar las rutas de moderación
        Assert.Equal(HttpStatusCode.Forbidden, (await duenio.GetAsync("/api/v1/admin/chat/marcados")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await duenio.GetAsync($"/api/v1/admin/chat/mensajes/{enviado.Id}/contexto")).StatusCode);

        var mod = await SuperAsync();
        var marcado = (await mod.GetFromJsonAsync<List<MensajeMarcadoDto>>("/api/v1/admin/chat/marcados"))!.Single(m => m.MensajeId == enviado.Id);
        Assert.Contains("teléfono", marcado.Motivo);
        var avisosMod = await mod.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones");
        Assert.Contains(avisosMod.GetProperty("items").EnumerateArray(),
            n => n.GetProperty("tipo").GetString() == "MensajeMarcado" && n.GetProperty("recursoId").GetGuid() == enviado.Id);
        Assert.True((await mod.GetFromJsonAsync<JsonElement>("/api/v1/admin/pendientes")).GetProperty("mensajesMarcados").GetInt32() >= 1);

        var ctx = (await mod.GetFromJsonAsync<ContextoChatDto>($"/api/v1/admin/chat/mensajes/{enviado.Id}/contexto"))!;
        Assert.Equal(3, ctx.Mensajes.Count); // el de la solicitud, la pregunta y el marcado
        Assert.True(ctx.Mensajes.Single(m => m.EsElRevisado).Id == enviado.Id);
        Assert.True(ctx.MarcaPendiente);

        // Un mensaje sin marca ni denuncia no abre la conversación
        var normal = ctx.Mensajes.First(m => !m.EsElRevisado).Id;
        Assert.Equal(HttpStatusCode.NotFound, (await mod.GetAsync($"/api/v1/admin/chat/mensajes/{normal}/contexto")).StatusCode);

        var resolver = await mod.PostAsJsonAsync($"/api/v1/admin/chat/mensajes/{enviado.Id}/resolver",
            new { accion = "Ocultar", nota = "Datos de contacto para negociar por fuera" });
        Assert.Equal(HttpStatusCode.NoContent, resolver.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await mod.PostAsJsonAsync($"/api/v1/admin/chat/mensajes/{enviado.Id}/resolver", new { accion = "Descartar" })).StatusCode);
        Assert.DoesNotContain((await mod.GetFromJsonAsync<List<MensajeMarcadoDto>>("/api/v1/admin/chat/marcados"))!, m => m.MensajeId == enviado.Id);

        var visto = (await duenio.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Single(m => m.Id == enviado.Id);
        Assert.True(visto.Oculto);
        Assert.DoesNotContain("310", visto.Texto);
        var avisos = await otro.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones");
        Assert.Contains(avisos.GetProperty("items").EnumerateArray(), n => n.GetProperty("tipo").GetString() == "MensajeOcultado");
    }

    [Fact]
    public async Task Un_mensaje_denunciado_tambien_abre_su_contexto()
    {
        var (duenio, otro, conv) = await ConversacionAsync();
        var r = await EnviarAsync(duenio, conv, "No te voy a responder más");
        var id = (await r.Content.ReadFromJsonAsync<MensajeChatDto>())!.Id;
        var mod = await SuperAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await mod.GetAsync($"/api/v1/admin/chat/mensajes/{id}/contexto")).StatusCode);

        (await otro.PostAsJsonAsync("/api/v1/denuncias", new { tipo = "Mensaje", objetivoId = id, motivo = "Acoso" })).EnsureSuccessStatusCode();
        var ctx = (await mod.GetFromJsonAsync<ContextoChatDto>($"/api/v1/admin/chat/mensajes/{id}/contexto"))!;
        Assert.Equal(1, ctx.Denuncias);
        Assert.False(ctx.MarcaPendiente);
    }
}
