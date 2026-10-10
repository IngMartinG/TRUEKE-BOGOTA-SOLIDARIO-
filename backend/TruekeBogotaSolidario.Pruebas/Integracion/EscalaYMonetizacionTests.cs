using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Contrato HTTP de ubicaciones, condición del producto, estadísticas, facturación, PQR y finanzas.</summary>
public class EscalaYMonetizacionTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public EscalaYMonetizacionTests(FabricaApi fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Un_invitado_puede_consultar_departamentos_y_municipios_de_Colombia()
    {
        var anonimo = _fabrica.CreateClient();
        var deps = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/ubicaciones/departamentos");
        Assert.Equal(33, deps.GetArrayLength());

        var r = await anonimo.GetAsync("/api/v1/ubicaciones/departamentos/05/municipios");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("max-age", r.Headers.CacheControl?.ToString());
        var antioquia = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(125, antioquia.GetArrayLength());

        var busqueda = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/ubicaciones/municipios?texto=medellin");
        Assert.Equal("05001", busqueda[0].GetProperty("codigo").GetString());
        Assert.Equal("Antioquia", busqueda[0].GetProperty("departamento").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/api/v1/ubicaciones/departamentos/00/municipios")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/api/v1/ubicaciones/municipios/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones/destacadas?departamentoCodigo=05")).StatusCode);
    }

    [Fact]
    public async Task Publicar_exige_la_condicion_y_acepta_municipios_de_todo_el_pais()
    {
        var c = await Api.RegistrarAsync(_fabrica, "nacional");
        var sinCondicion = await c.PostAsJsonAsync("/api/v1/publicaciones",
            new { titulo = "Nevera", descripcion = "Funciona", categoriaId = 3, modo = "Donacion", localidad = "El Poblado", municipioCodigo = "05001" });
        Assert.Equal(HttpStatusCode.BadRequest, sinCondicion.StatusCode);
        var problema = await sinCondicion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problema.GetProperty("errors").TryGetProperty("condicion", out _));

        var malMunicipio = await c.PostAsJsonAsync("/api/v1/publicaciones",
            new { titulo = "Nevera", descripcion = "Funciona", categoriaId = 3, modo = "Donacion", condicion = "Usado", localidad = "Centro", municipioCodigo = "ABCDE" });
        Assert.Equal(HttpStatusCode.BadRequest, malMunicipio.StatusCode);

        var ok = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Nevera", descripcion = "Funciona", categoriaId = 3, modo = "Donacion", condicion = "Reparado",
            detalleCondicion = "Se le cambió el compresor en 2025", localidad = "El Poblado", municipioCodigo = "05001"
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var pub = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reparado", pub.GetProperty("condicion").GetString());
        Assert.Equal("Medellín, Antioquia", pub.GetProperty("municipio").GetString());
        Assert.Equal(0, pub.GetProperty("vistas").GetInt32()); // el dueño ve sus vistas

        var publico = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub.GetProperty("id").GetString()}");
        Assert.Equal(JsonValueKind.Null, publico.GetProperty("vistas").ValueKind); // un tercero no

        var filtrado = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/publicaciones?departamentoCodigo=05&condicion=Reparado");
        Assert.Contains(filtrado.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetString() == pub.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Las_estadisticas_solo_las_ve_el_duenio()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "stats");
        var otro = await Api.RegistrarAsync(_fabrica, "curioso");
        var pub = await Api.CrearPublicacionAsync(duenio);
        Assert.Equal(HttpStatusCode.OK, (await duenio.GetAsync($"/api/v1/publicaciones/{pub}/estadisticas")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/v1/publicaciones/{pub}/estadisticas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}/estadisticas")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.PostAsync($"/api/v1/publicaciones/{pub}/impulsar", null)).StatusCode);
    }

    [Fact]
    public async Task Datos_de_facturacion_y_PQR_requieren_sesion_y_validan_documentos()
    {
        var anonimo = _fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/cuenta/facturas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/v1/pqr", new { tipo = "Peticion", asunto = "Hola hola", descripcion = "Una petición cualquiera" })).StatusCode);

        var c = await Api.RegistrarAsync(_fabrica, "facturable");
        var nitMalo = await c.PutAsJsonAsync("/api/v1/cuenta/facturacion",
            new { tipoDocumento = "NIT", documento = "900123456-1", nombre = "Empresa SAS", correo = "f@empresa.co" });
        Assert.Equal(HttpStatusCode.BadRequest, nitMalo.StatusCode);
        // la DIAN exige todos los datos: sin dirección no se guardan
        var sinDireccion = await c.PutAsJsonAsync("/api/v1/cuenta/facturacion",
            new { tipoDocumento = "NIT", documento = "900123456", nombre = "Empresa SAS", correo = "f@empresa.co", municipioCodigo = "76001" });
        Assert.Equal(HttpStatusCode.BadRequest, sinDireccion.StatusCode);
        var ok = await c.PutAsJsonAsync("/api/v1/cuenta/facturacion",
            new { tipoDocumento = "NIT", documento = "900123456", nombre = "Empresa SAS", correo = "f@empresa.co", direccion = "Avenida 6N # 25-10", municipioCodigo = "76001" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var datos = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("900123456-8", datos.GetProperty("documento").GetString());
        Assert.Equal("Santiago de Cali, Valle del Cauca", datos.GetProperty("municipio").GetString());

        var pqr = await c.PostAsJsonAsync("/api/v1/pqr", new { tipo = "Peticion", asunto = "Factura a nombre de empresa", descripcion = "¿Pueden reexpedir la factura con mi NIT?" });
        Assert.Equal(HttpStatusCode.Created, pqr.StatusCode);
        Assert.StartsWith("PQR-", (await pqr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("radicado").GetString());
    }

    [Fact]
    public async Task Las_finanzas_son_solo_para_el_SuperUsuario_y_el_CSV_es_seguro()
    {
        var super = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var cliente = await Api.RegistrarAsync(_fabrica, "cliente-finanzas");
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/admin/ingresos")).StatusCode);

        var ingresos = await super.GetAsync("/api/v1/admin/ingresos");
        Assert.Equal(HttpStatusCode.OK, ingresos.StatusCode);
        var csv = await super.GetAsync("/api/v1/admin/ingresos.csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Referencia;", (await csv.Content.ReadAsStringAsync()).TrimStart('﻿'));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://malo\")", "\"'=HYPERLINK(\"\"http://malo\"\")\"")]
    [InlineData("+57 300", "'+57 300")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("texto; con punto y coma", "\"texto; con punto y coma\"")]
    [InlineData("normal", "normal")]
    public void El_CSV_neutraliza_formulas_y_escapa_separadores(string valor, string esperado)
        => Assert.Equal(esperado, Negocio.Servicios.Csv.Celda(valor));
}
