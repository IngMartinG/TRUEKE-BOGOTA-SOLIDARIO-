using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.SqlServer;

/// <summary>
/// Lo que la base InMemory NO puede comprobar: que las migraciones se aplican en SQL Server, que todas las consultas LINQ
/// se traducen a SQL, la concurrencia con rowversion (409) y los índices únicos.
/// </summary>
[Trait("Categoria", "SqlServer")]
public class SqlServerTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _sql;
    public SqlServerTests(SqlServerFixture sql) => _sql = sql;

    private sealed class FabricaApiSqlServer : FabricaApi
    {
        private readonly string _cadena;
        public FabricaApiSqlServer(string cadena) => _cadena = cadena;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Database:Provider", "SqlServer");
            builder.UseSetting("ConnectionStrings:TruekeDb", _cadena);
            builder.UseSetting("Database:Inicializacion", "Migrate"); // igual que producción
        }
    }

    private TruekeDbContext Contexto(string cadena) => new(new DbContextOptionsBuilder<TruekeDbContext>().UseSqlServer(cadena).Options);

    [SkippableFact]
    public async Task Las_migraciones_se_aplican_y_siembran_las_categorias()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using var db = Contexto(cadena);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(7, await db.Categorias.CountAsync());
    }

    [SkippableFact]
    public async Task Flujo_completo_de_la_API_sobre_SQL_Server()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        await using var f = new FabricaApiSqlServer(_sql.NuevaBase());
        var duenio = await Api.RegistrarAsync(f, "sqlana");
        var otro = await Api.RegistrarAsync(f, "sqlbeto");
        var anonimo = f.CreateClient();

        // catálogo: filtros, orden por precio, texto, cercanía (consultas traducidas a SQL)
        var pub = await Api.CrearPublicacionAsync(duenio, 4.6097, -74.0817, modo: "Compra");
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones?orden=PrecioAsc&precioMin=1&texto=Bici&soloVerificados=false")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones?orden=Recientes&categoriaId=1&modo=Compra")).StatusCode);
        var cercanas = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/publicaciones/cercanas?lat=4.6097&lon=-74.0817&radioKm=5");
        Assert.Single(cercanas.EnumerateArray());

        // solicitud + chat (GroupBy/First para la bandeja) + entrega + calificación
        var s = await (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Hola" })).Content.ReadFromJsonAsync<JsonElement>();
        var sol = s.GetProperty("id").GetGuid();
        var conv = s.GetProperty("conversacionId").GetGuid();
        (await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "¿Cuándo?" })).EnsureSuccessStatusCode();
        var bandeja = await otro.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones");
        Assert.Equal(1, bandeja[0].GetProperty("noLeidos").GetInt32());
        Assert.Equal("¿Cuándo?", bandeja[0].GetProperty("ultimoMensaje").GetString());

        (await duenio.PostAsync($"/api/v1/solicitudes/{sol}/aceptar", null)).EnsureSuccessStatusCode();
        (await duenio.PostAsync($"/api/v1/solicitudes/{sol}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        var completada = await (await otro.PostAsync($"/api/v1/solicitudes/{sol}/confirmar-entrega", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completada", completada.GetProperty("estado").GetString());
        (await otro.PostAsJsonAsync($"/api/v1/solicitudes/{sol}/calificar", new { estrellas = 5, comentario = "Excelente" })).EnsureSuccessStatusCode();

        // perfil, favoritos, notificaciones, comentarios, exportación (más consultas reales)
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode); // intercambiada: ya no es pública
        Assert.Equal(HttpStatusCode.OK, (await otro.GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);         // pero quien la recibió la sigue viendo
        var nueva = await Api.CrearPublicacionAsync(duenio);
        (await otro.PostAsync($"/api/v1/publicaciones/{nueva}/favorito", null)).EnsureSuccessStatusCode();
        Assert.Equal(1, (await otro.GetFromJsonAsync<JsonElement>("/api/v1/favoritos")).GetProperty("total").GetInt32());
        (await otro.PostAsJsonAsync($"/api/v1/publicaciones/{nueva}/comentarios", new { texto = "¿Aún disponible?" })).EnsureSuccessStatusCode();
        Assert.Single((await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{nueva}/comentarios")).GetProperty("items").EnumerateArray());
        Assert.True(await duenio.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total") > 0);
        Assert.Equal(HttpStatusCode.OK, (await otro.GetAsync("/api/v1/usuarios/yo/datos")).StatusCode);
        var duenioId = (await duenio.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid();
        var calif = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{duenioId}/calificaciones");
        Assert.Equal(1, calif.GetProperty("total").GetInt32());

        // administración: búsqueda de usuarios (LOWER/Contains) y cola de denuncias
        var super = Api.ConToken(f, await Api.LoginAsync(f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        Assert.True((await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/usuarios?texto=SQLANA")).GetProperty("total").GetInt32() >= 1);
        Assert.Equal(HttpStatusCode.OK, (await super.GetAsync("/api/v1/admin/denuncias")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await super.GetAsync("/api/v1/admin/pagos?estado=RequiereRevision")).StatusCode);
    }

    [SkippableFact]
    public async Task Dos_cambios_simultaneos_al_mismo_registro_dan_conflicto_409()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using (var db = Contexto(cadena)) await db.Database.MigrateAsync();

        Guid id;
        await using (var db = Contexto(cadena))
        {
            var u = new Datos.Entidades.Usuario("Concurrencia Prueba", "Suba", $"c{Guid.NewGuid():N}@t.co", "Clave12345");
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            id = u.Id;
        }

        await using var a = Contexto(cadena);
        await using var b = Contexto(cadena);
        var ua = await a.Usuarios.SingleAsync(x => x.Id == id);
        var ub = await b.Usuarios.SingleAsync(x => x.Id == id);
        ua.ActualizarPerfil("Primer Cambio", "Bosa");
        ub.ActualizarPerfil("Segundo Cambio", "Usme");
        await new UnidadDeTrabajo(a).GuardarCambiosAsync();
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(() => new UnidadDeTrabajo(b).GuardarCambiosAsync());
    }

    [SkippableFact]
    public async Task El_indice_unico_de_correo_impide_duplicados()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using var db = Contexto(cadena);
        await db.Database.MigrateAsync();
        var correo = $"dup{Guid.NewGuid():N}@t.co";
        db.Usuarios.Add(new Datos.Entidades.Usuario("Uno Prueba", "Suba", correo, "Clave12345"));
        db.Usuarios.Add(new Datos.Entidades.Usuario("Dos Prueba", "Suba", correo, "Clave12345"));
        await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(() => new UnidadDeTrabajo(db).GuardarCambiosAsync());
    }
}
