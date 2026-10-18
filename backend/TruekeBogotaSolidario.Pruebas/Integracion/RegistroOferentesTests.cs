using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Registro de oferentes (Ley 1480, art. 53): vender exige nombre, documento, dirección de notificaciones y teléfono. Se
/// guardan cifrados, nunca los ve un tercero y la moderación solo los consulta con la PQRS de alguien que compró.
/// </summary>
public class RegistroOferentesTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public RegistroOferentesTests(FabricaApi f) => _f = f;

    private const string Documento = "1012345678";
    private const string Direccion = "Calle 45 # 12-30";
    private const string DocVendedor = "79876543";
    private const string TelVendedor = "3109876543";

    /// <summary>Lista para operar (correo verificado y foto) pero sin registro de vendedor.</summary>
    private async Task<(HttpClient Cliente, Guid Id)> SinRegistroAsync(string nombre)
    {
        var ses = await Api.RegistrarSesionSinFotoAsync(_f, nombre);
        await Api.PonerFotoAsync(_f, ses.Usuario.Id);
        return (Api.ConToken(_f, ses.Token), ses.Usuario.Id);
    }

    private static object Publicacion(string modo) => new
    {
        titulo = "Bicicleta usada", descripcion = "En buen estado", categoriaId = 1, modo, condicion = "Usado", localidad = "Chapinero",
        precioReferenciaCop = modo == "Compra" ? 50000 : (decimal?)null
    };

    private static async Task<string?> CodigoAsync(HttpResponseMessage r)
        => (await r.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("codigo", out var c) ? c.GetString() : null;

    [Fact]
    public async Task Vender_exige_el_registro_y_trueke_y_donacion_no()
    {
        var (c, id) = await SinRegistroAsync("vendedora");
        var sinRegistro = await c.PostAsJsonAsync("/api/v1/publicaciones", Publicacion("Compra"));
        Assert.Equal(HttpStatusCode.Forbidden, sinRegistro.StatusCode);
        Assert.Equal("datos_oferente_requeridos", await CodigoAsync(sinRegistro));
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/v1/publicaciones", Publicacion("Trueke"))).StatusCode);
        var donacion = await c.PostAsJsonAsync("/api/v1/publicaciones", Publicacion("Donacion"));
        Assert.Equal(HttpStatusCode.Created, donacion.StatusCode);

        // Pasar a Compra una publicación existente también lo exige
        var pubId = (await donacion.Content.ReadFromJsonAsync<IdDto>())!.Id;
        Assert.Equal("datos_oferente_requeridos", await CodigoAsync(await c.PutAsJsonAsync($"/api/v1/publicaciones/{pubId}", Publicacion("Compra"))));
        Assert.False((await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("registroOferente").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync("/api/v1/usuarios/yo/oferente")).StatusCode);

        // Datos inválidos: 400 por campo
        var malo = await c.PutAsJsonAsync("/api/v1/usuarios/yo/oferente", new
        {
            tipoDocumento = "CC", documento = "12", nombre = "Ana Pérez", direccion = "!!!", municipioCodigo = "99999", telefono = "12345"
        });
        Assert.Equal(HttpStatusCode.BadRequest, malo.StatusCode);
        var errores = (await malo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var campo in new[] { "Documento", "Direccion", "Telefono" })
            Assert.True(errores.EnumerateObject().Any(e => e.Name.Equals(campo, StringComparison.OrdinalIgnoreCase)), campo);

        var registro = await c.PutAsJsonAsync("/api/v1/usuarios/yo/oferente", new
        {
            tipoDocumento = "CC", documento = "1.012.345.678", nombre = "Ana  Pérez", direccion = Direccion, municipioCodigo = "11001",
            telefono = "+57 300-123-4567"
        });
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        Assert.True((await registro.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("registroOferente").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/v1/publicaciones", Publicacion("Compra"))).StatusCode);

        // El titular ve sus datos normalizados; en la base van cifrados
        var mio = await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/oferente");
        Assert.Equal(Documento, mio.GetProperty("documento").GetString());
        Assert.Equal("3001234567", mio.GetProperty("telefono").GetString());
        Assert.Equal("Ana Pérez", mio.GetProperty("nombre").GetString());
        Assert.Contains(Documento, await c.GetStringAsync("/api/v1/usuarios/yo/datos")); // y en la exportación de sus datos
        using var scope = _f.Services.CreateScope();
        var u = await scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Usuarios.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.DoesNotContain(Documento, u.OferenteDocumentoCifrado);
        Assert.DoesNotContain("3001234567", u.OferenteTelefonoCifrado);
        Assert.DoesNotContain("Calle", u.OferenteDireccionCifrada);
    }

    [Fact]
    public async Task Un_tercero_nunca_ve_los_datos_y_la_moderacion_solo_con_una_PQRS_de_un_comprador()
    {
        var vendedor = await Api.RegistrarSesionAsync(_f, "vende");
        var cv = Api.ConToken(_f, vendedor.Token);
        // Datos propios del vendedor (los demás usuarios de prueba tienen los de Api.DatosOferente)
        (await cv.PutAsJsonAsync("/api/v1/usuarios/yo/oferente", new
        {
            tipoDocumento = "CC", documento = DocVendedor, nombre = "Vende Prueba", direccion = "Carrera 7 # 80-15", municipioCodigo = "11001",
            telefono = TelVendedor
        })).EnsureSuccessStatusCode();
        var comprador = await Api.RegistrarAsync(_f, "compra");
        var curioso = await Api.RegistrarAsync(_f, "curioso");

        // Compra completada entre los dos. Antes de venderse, la publicación tampoco muestra el registro.
        var pub = await Api.CrearPublicacionAsync(cv, modo: "Compra");
        var publicacion = await curioso.GetStringAsync($"/api/v1/publicaciones/{pub}");
        var s = (await (await comprador.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await cv.PostAsync($"/api/v1/solicitudes/{s}/aceptar", null)).EnsureSuccessStatusCode();
        (await cv.PostAsync($"/api/v1/solicitudes/{s}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await comprador.PostAsync($"/api/v1/solicitudes/{s}/confirmar-entrega", null)).EnsureSuccessStatusCode();

        // Ningún tercero ve el registro: ni el perfil público, ni la publicación, ni las solicitudes del comprador
        foreach (var texto in new[]
                 {
                     await curioso.GetStringAsync($"/api/v1/usuarios/{vendedor.Usuario.Id}/perfil"),
                     publicacion,
                     await comprador.GetStringAsync("/api/v1/solicitudes/enviadas"),
                     await comprador.GetStringAsync("/api/v1/usuarios/yo/datos"),
                 })
        {
            Assert.DoesNotContain(DocVendedor, texto);
            Assert.DoesNotContain(TelVendedor, texto);
            Assert.DoesNotContain("Carrera 7", texto);
        }

        Guid Radicar(JsonElement p) => p.GetProperty("id").GetGuid();
        var reclamo = Radicar(await (await comprador.PostAsJsonAsync("/api/v1/pqr",
            new { tipo = "Reclamo", asunto = "El producto llegó dañado", descripcion = "La bicicleta que compré llegó con el marco roto." }))
            .Content.ReadFromJsonAsync<JsonElement>());
        var ajeno = Radicar(await (await curioso.PostAsJsonAsync("/api/v1/pqr",
            new { tipo = "Reclamo", asunto = "Quiero los datos de alguien", descripcion = "Necesito el teléfono de un vendedor." }))
            .Content.ReadFromJsonAsync<JsonElement>());

        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        // Un cliente no puede consultar el registro
        Assert.Equal(HttpStatusCode.Forbidden, (await curioso.GetAsync($"/api/v1/admin/pqr/{reclamo}/vendedores/{vendedor.Usuario.Id}/oferente")).StatusCode);

        // La PQRS del comprador lista al vendedor (sin sus datos) y permite consultar el registro, que queda en la auditoría
        var vendedores = await super.GetFromJsonAsync<JsonElement>($"/api/v1/admin/pqr/{reclamo}/vendedores");
        var fila = Assert.Single(vendedores.EnumerateArray());
        Assert.Equal(vendedor.Usuario.Id, fila.GetProperty("usuarioId").GetGuid());
        Assert.True(fila.GetProperty("tieneRegistro").GetBoolean());
        Assert.DoesNotContain(DocVendedor, vendedores.GetRawText());
        var datos = await super.GetFromJsonAsync<JsonElement>($"/api/v1/admin/pqr/{reclamo}/vendedores/{vendedor.Usuario.Id}/oferente");
        Assert.Equal(DocVendedor, datos.GetProperty("documento").GetString());
        Assert.Equal(TelVendedor, datos.GetProperty("telefono").GetString());
        using (var scope = _f.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Auditoria
                .AnyAsync(a => a.Accion == "OFERENTE_CONSULTADO" && a.ObjetivoId == vendedor.Usuario.Id));

        // Con la PQRS de alguien que no le compró: nada (el mismo 404 que si no existiera)
        Assert.Empty((await super.GetFromJsonAsync<JsonElement>($"/api/v1/admin/pqr/{ajeno}/vendedores")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await super.GetAsync($"/api/v1/admin/pqr/{ajeno}/vendedores/{vendedor.Usuario.Id}/oferente")).StatusCode);
    }
}
