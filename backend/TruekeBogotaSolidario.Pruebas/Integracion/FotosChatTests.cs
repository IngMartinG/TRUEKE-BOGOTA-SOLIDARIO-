using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class FotosChatTests : IClassFixture<FabricaApi>
{
    private readonly AlmacenFalso _almacen = new();
    private readonly WebApplicationFactory<Program> _f;

    public FotosChatTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen))));

    private async Task<(HttpClient Duenio, HttpClient Otro, Guid Conv)> PrepararAsync(string alias)
    {
        var duenio = await Api.RegistrarAsync(_f, alias + "duenia");
        var otro = await Api.RegistrarAsync(_f, alias + "interesado");
        var pub = await Api.CrearPublicacionAsync(duenio);
        var r = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Me la cambias?" });
        return (duenio, otro, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conversacionId").GetGuid());
    }

    /// <summary>Pide la subida tipo FotoChat y simula el PUT del navegador.</summary>
    private async Task<string> SubirFotoAsync(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "FotoChat", contentType = "image/png", tamanoBytes = 100 });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var s = (await r.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
        _almacen.Subir(s.UrlArchivo, "image/png", AlmacenFalso.Png());
        return s.UrlArchivo;
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient c, Guid conv, string? fotoUrl, string texto = "")
        => c.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto, fotoUrl });

    [Fact]
    public async Task La_foto_queda_privada_y_solo_la_ven_las_dos_partes_con_una_url_firmada()
    {
        var (duenio, otro, conv) = await PrepararAsync("fc1");
        var subida = await SubirFotoAsync(otro);

        var r = await EnviarAsync(otro, conv, subida); // sin texto: la foto basta
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var enviado = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enviado.GetProperty("tieneFoto").GetBoolean());
        var firmada = enviado.GetProperty("fotoUrl").GetString()!;
        Assert.Contains("/chat/", firmada);       // bucket privado del chat, nunca el público de imágenes
        Assert.EndsWith("?firma=chat", firmada);  // siempre firmada
        Assert.False(_almacen.Existe(subida));   // la subida se borró: lo guardado es la copia del servidor

        var mensajes = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/conversaciones/{conv}/mensajes");
        var conFoto = mensajes.EnumerateArray().Single(m => m.GetProperty("tieneFoto").GetBoolean());
        Assert.Equal(firmada, conFoto.GetProperty("fotoUrl").GetString());

        var bandeja = await duenio.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones");
        Assert.Equal("📷 Foto", bandeja.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == conv).GetProperty("ultimoMensaje").GetString());

        // Un tercero no ve la conversación (ni sus fotos)
        var tercero = await Api.RegistrarAsync(_f, "fc1tercero");
        Assert.Equal(HttpStatusCode.NotFound, (await tercero.GetAsync($"/api/v1/conversaciones/{conv}/mensajes")).StatusCode);
    }

    [Fact]
    public async Task Solo_se_aceptan_fotos_propias_subidas_con_la_url_firmada()
    {
        var (duenio, otro, conv) = await PrepararAsync("fc2");
        var ajena = await SubirFotoAsync(duenio);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(otro, conv, ajena)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(otro, conv, "https://otro-sitio.test/foto.png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(otro, conv, null)).StatusCode); // sin foto ni texto

        // Un PDF disfrazado no pasa: FotoChat solo admite imágenes
        var pdf = await otro.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "FotoChat", contentType = "application/pdf", tamanoBytes = 100 });
        Assert.Equal(HttpStatusCode.BadRequest, pdf.StatusCode);
    }

    [Fact]
    public async Task Una_foto_denunciada_la_ve_el_moderador_y_al_ocultarla_desaparece_del_chat()
    {
        var (duenio, otro, conv) = await PrepararAsync("fc3");
        var r = await EnviarAsync(otro, conv, await SubirFotoAsync(otro), "Mira el estado");
        var mensajeId = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, (await duenio.PostAsJsonAsync("/api/v1/denuncias",
            new { tipo = "Mensaje", objetivoId = mensajeId, motivo = "ContenidoInapropiado" })).StatusCode);

        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var grupo = (await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/denuncias?estado=Pendiente"))
            .EnumerateArray().Single(g => g.GetProperty("objetivoId").GetGuid() == mensajeId);
        Assert.Equal("📷 Foto: Mira el estado", grupo.GetProperty("vistaPrevia").GetString());
        Assert.EndsWith("?firma=chat", grupo.GetProperty("fotoUrl").GetString());

        (await super.PostAsJsonAsync($"/api/v1/admin/denuncias/{grupo.GetProperty("denunciaId").GetGuid()}/resolver",
            new { accion = "OcultarContenido", nota = "Foto inapropiada" })).EnsureSuccessStatusCode();

        var mensajes = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/conversaciones/{conv}/mensajes");
        var oculto = mensajes.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == mensajeId);
        Assert.True(oculto.GetProperty("oculto").GetBoolean());
        Assert.False(oculto.GetProperty("tieneFoto").GetBoolean());
        Assert.Equal(JsonValueKind.Null, oculto.GetProperty("fotoUrl").ValueKind);
    }

    [Fact]
    public async Task Hay_un_tope_de_fotos_al_dia()
    {
        var (_, otro, conv) = await PrepararAsync("fc4");
        for (var i = 0; i < ChatService.MaxFotosPorDia; i++)
            Assert.Equal(HttpStatusCode.Created, (await EnviarAsync(otro, conv, await SubirFotoAsync(otro))).StatusCode);
        var r = await EnviarAsync(otro, conv, await SubirFotoAsync(otro));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(cuerpo.Contains($"{ChatService.MaxFotosPorDia} fotos"), cuerpo);
    }
}
