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
        // denunciantes y autor fueron avisados (el autor, sin saber quién lo denunció)
        Assert.Equal(1, await a.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));
        var avisos = await vendedor.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones");
        var aviso = Assert.Single(avisos.GetProperty("items").EnumerateArray(), n => n.GetProperty("tipo").GetString() == "DenunciaRecibida");
        Assert.Contains("posible fraude", aviso.GetProperty("mensaje").GetString());
        Assert.DoesNotContain("vecina", aviso.GetProperty("mensaje").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> ResolverAsync(HttpClient moderador, Guid objetivo, string accion, string? nota = "Infracción comprobada")
    {
        var g = await GrupoAsync(moderador, objetivo);
        (await moderador.PostAsJsonAsync($"/api/v1/admin/denuncias/{g.GetProperty("denunciaId").GetGuid()}/resolver", new { accion, nota }))
            .EnsureSuccessStatusCode();
        return g.GetProperty("denunciaId").GetGuid();
    }

    private async Task<(HttpClient Cliente, Guid Id)> AdminAsync(string nombre)
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, nombre);
        (await (await SuperAsync()).PatchAsJsonAsync($"/api/v1/admin/usuarios/{ses.Usuario.Id}/rol", new { rol = "Administrador" })).EnsureSuccessStatusCode();
        return (Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, ses.Usuario.Correo, Api.ClaveValida)), ses.Usuario.Id);
    }

    [Fact]
    public async Task El_denunciado_ve_la_decision_sin_saber_quien_lo_denuncio_y_si_se_descarta_no_se_le_avisa()
    {
        var autor = await Api.RegistrarAsync(_fabrica, "denunciada");
        var pub = await Api.CrearPublicacionAsync(autor);
        var pubDescartada = await Api.CrearPublicacionAsync(autor);
        var denunciante = await Api.RegistrarAsync(_fabrica, "denunciante");
        await DenunciarAsync(denunciante, "Publicacion", pub, "Fraude", "Texto secreto del denunciante");
        await DenunciarAsync(denunciante, "Publicacion", pubDescartada, "Spam");

        var super = await SuperAsync();
        await ResolverAsync(super, pub, "MarcarRevisada", "Precio engañoso");
        await ResolverAsync(super, pubDescartada, "Descartar", null);

        var cuerpo = await autor.GetStringAsync("/api/v1/denuncias/recibidas");
        Assert.DoesNotContain("denunciante", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Texto secreto", cuerpo);
        var recibidas = JsonDocument.Parse(cuerpo).RootElement;
        var r = Assert.Single(recibidas.EnumerateArray());                     // la descartada no aparece
        Assert.Equal(pub, r.GetProperty("objetivoId").GetGuid());
        Assert.Equal("MarcarRevisada", r.GetProperty("accion").GetString());
        Assert.Equal("Precio engañoso", r.GetProperty("notaModerador").GetString());
        Assert.NotEqual(JsonValueKind.Null, r.GetProperty("apelableHastaUtc").ValueKind);

        var avisos = (await autor.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray().ToList();
        var aviso = Assert.Single(avisos, n => n.GetProperty("tipo").GetString() == "DenunciaRecibida");
        Assert.Equal(r.GetProperty("resolucionId").GetGuid(), aviso.GetProperty("recursoId").GetGuid());

        // A otra persona no le aparece nada
        var otro = await Api.RegistrarAsync(_fabrica, "ajeno");
        Assert.Empty((await otro.GetFromJsonAsync<JsonElement>("/api/v1/denuncias/recibidas")).EnumerateArray());
    }

    [Fact]
    public async Task Apelacion_aceptada_por_otro_moderador_restaura_el_contenido_y_avisa_a_ambas_partes()
    {
        var autor = await Api.RegistrarAsync(_fabrica, "apelante");
        var pub = await Api.CrearPublicacionAsync(autor);
        var denunciante = await Api.RegistrarAsync(_fabrica, "reportero");
        await DenunciarAsync(denunciante, "Publicacion", pub, "ArticuloProhibido");

        var (admin1, _) = await AdminAsync("moderadorUno");
        var (admin2, _) = await AdminAsync("moderadorDos");
        await ResolverAsync(admin1, pub, "OcultarContenido", "Parece un artículo prohibido");
        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);

        var resolucion = (await autor.GetFromJsonAsync<JsonElement>("/api/v1/denuncias/recibidas")).EnumerateArray().Single().GetProperty("resolucionId").GetGuid();
        var url = $"/api/v1/denuncias/recibidas/{resolucion}/apelacion";
        Assert.Equal(HttpStatusCode.BadRequest, (await autor.PostAsJsonAsync(url, new { texto = "corto" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await denunciante.PostAsJsonAsync(url, new { texto = "Yo no soy la persona denunciada aquí." })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await autor.PostAsJsonAsync(url, new { texto = "Es una herramienta de jardín legal, adjunto la factura en el chat." })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await autor.PostAsJsonAsync(url, new { texto = "Insisto: es una herramienta de jardín legal." })).StatusCode);

        var tras = (await autor.GetFromJsonAsync<JsonElement>("/api/v1/denuncias/recibidas")).EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, tras.GetProperty("apelableHastaUtc").ValueKind);
        Assert.Equal("Pendiente", tras.GetProperty("apelacion").GetProperty("estado").GetString());

        // Quien resolvió la denuncia no revisa su propia decisión
        var enCola1 = (await admin1.GetFromJsonAsync<JsonElement>("/api/v1/admin/apelaciones")).EnumerateArray().Single(x => x.GetProperty("resolucionId").GetGuid() == resolucion);
        Assert.False(enCola1.GetProperty("puedoResolver").GetBoolean());
        var apelacionId = enCola1.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin1.PostAsJsonAsync($"/api/v1/admin/apelaciones/{apelacionId}/resolver",
            new { aceptar = true, nota = "Me equivoqué" })).StatusCode);

        var enCola2 = (await admin2.GetFromJsonAsync<JsonElement>("/api/v1/admin/apelaciones")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == apelacionId);
        Assert.True(enCola2.GetProperty("puedoResolver").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin2.PostAsJsonAsync($"/api/v1/admin/apelaciones/{apelacionId}/resolver",
            new { aceptar = true, nota = "La factura demuestra que es legal." })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode); // vuelve a verse
        var final = (await autor.GetFromJsonAsync<JsonElement>("/api/v1/denuncias/recibidas")).EnumerateArray().Single();
        Assert.Equal("Aceptada", final.GetProperty("apelacion").GetProperty("estado").GetString());
        Assert.Equal("La factura demuestra que es legal.", final.GetProperty("apelacion").GetProperty("respuesta").GetString());

        var avisosAutor = (await autor.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray();
        Assert.Contains(avisosAutor, n => n.GetProperty("tipo").GetString() == "ApelacionResuelta");
        var avisosDenunciante = (await denunciante.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray();
        Assert.Equal(2, avisosDenunciante.Count(n => n.GetProperty("tipo").GetString() == "DenunciaRevisada"));  // la decisión y la reversión
    }

    [Fact]
    public async Task Apelacion_rechazada_mantiene_el_mensaje_oculto_y_un_cliente_no_ve_la_cola()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "duenioMsg");
        var autor = await Api.RegistrarAsync(_fabrica, "groserito");
        var pub = await Api.CrearPublicacionAsync(duenio);
        var s = await (await autor.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Hola" })).Content.ReadFromJsonAsync<JsonElement>();
        var conv = s.GetProperty("conversacionId").GetGuid();
        var msg = (await (await autor.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "Insulto" })).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        await DenunciarAsync(duenio, "Mensaje", msg, "Acoso");

        var super = await SuperAsync();
        await ResolverAsync(super, msg, "OcultarContenido", "Lenguaje ofensivo");
        var resolucion = (await autor.GetFromJsonAsync<JsonElement>("/api/v1/denuncias/recibidas")).EnumerateArray().Single().GetProperty("resolucionId").GetGuid();
        (await autor.PostAsJsonAsync($"/api/v1/denuncias/recibidas/{resolucion}/apelacion", new { texto = "Era una broma entre nosotros, no un insulto." })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await autor.GetAsync("/api/v1/admin/apelaciones")).StatusCode);

        // El SuperUsuario sí puede revisar su propia decisión (equipos pequeños)
        var apelacion = (await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/apelaciones")).EnumerateArray().Single(x => x.GetProperty("resolucionId").GetGuid() == resolucion);
        Assert.True(apelacion.GetProperty("puedoResolver").GetBoolean());
        Assert.Equal("Era una broma entre nosotros, no un insulto.", apelacion.GetProperty("texto").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostAsJsonAsync($"/api/v1/admin/apelaciones/{apelacion.GetProperty("id").GetGuid()}/resolver",
            new { aceptar = false })).StatusCode);                                                         // la nota es obligatoria
        (await super.PostAsJsonAsync($"/api/v1/admin/apelaciones/{apelacion.GetProperty("id").GetGuid()}/resolver",
            new { aceptar = false, nota = "El mensaje incumple las normas." })).EnsureSuccessStatusCode();

        var mensajes = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/conversaciones/{conv}/mensajes");
        Assert.True(mensajes.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == msg).GetProperty("oculto").GetBoolean());
        Assert.Equal("Rechazada", (await autor.GetFromJsonAsync<JsonElement>("/api/v1/denuncias/recibidas")).EnumerateArray().Single()
            .GetProperty("apelacion").GetProperty("estado").GetString());
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
