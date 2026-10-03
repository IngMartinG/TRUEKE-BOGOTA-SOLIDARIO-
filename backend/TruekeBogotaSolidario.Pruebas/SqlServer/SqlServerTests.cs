using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Producción ya puede tener datos creados con la migración Inicial: la migración de escala nacional debe aplicarse
    /// sobre ellos (correo canónico único, Bogotá como municipio, bono ya entregado, orden por fecha de publicación).
    /// </summary>
    [SkippableFact]
    public async Task La_migracion_de_escala_nacional_respeta_los_datos_existentes()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using var db = Contexto(cadena);
        var migrador = db.Database.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrador.MigrateAsync("20261002174413_Inicial");

        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        const string columnas = "(Id, NombreCompleto, Localidad, Correo, ClaveHash, FechaRegistro, VersionSeguridad, IntentosFallidosLogin, SaldoEcoPuntos, Reputacion, " +
                                "TotalTruekesCompletados, TotalComprasRealizadas, TotalDonacionesRealizadas, Rol, TipoCuenta, DestacadosGratisRestantes, EstadoVerificacion, " +
                                "CorreoVerificado, EstaEliminado, EstaSuspendido, CalificacionesTotal, CalificacionesSuma, DosFactoresActivo, UltimoPasoDosFactores)";
        foreach (var (id, correo) in new[] { (u1, "ana.perez@gmail.com"), (u2, "anaperez@gmail.com") }) // alias que antes se permitían
            await db.Database.ExecuteSqlRawAsync($"INSERT INTO Usuarios {columnas} VALUES ({{0}}, 'Ana Prueba', 'Kennedy', {{1}}, '', SYSUTCDATETIME(), 0, 0, 10, 0, 0, 0, 0, 'Cliente', 'Individual', 0, 'NoVerificado', 1, 0, 0, 0, 0, 0, 0)", id, correo);
        var pub = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Publicaciones (Id, PropietarioId, Titulo, Descripcion, CategoriaId, Modo, Localidad, Estado, FechaPublicacion, EstaOculta) " +
            "VALUES ({0}, {1}, 'Silla', 'De madera', 4, 'Donacion', 'Kennedy', 'Disponible', '2026-09-01T10:00:00', 0)", pub, u1);

        await db.Database.MigrateAsync(); // aplica EscalaNacionalYMonetizacion sobre los datos
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var usuario = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == u1);
        Assert.Equal("ana.perez@gmail.com", usuario.CorreoCanonico); // sin colisión con el alias existente
        Assert.Equal(Divipola.CodigoBogota, usuario.MunicipioCodigo);
        Assert.True(usuario.BonoBienvenidaOtorgado);
        var p = await db.Publicaciones.AsNoTracking().SingleAsync(x => x.Id == pub);
        Assert.Equal(Datos.Entidades.CondicionProducto.Usado, p.Condicion);
        Assert.Equal("11", p.DepartamentoCodigo);
        Assert.Equal(p.FechaPublicacion, p.FechaRelevancia);
    }

    [SkippableFact]
    public async Task Monetizacion_y_escala_nacional_sobre_SQL_Server()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        await using var f = new FabricaApiSqlServer(_sql.NuevaBase());
        var c = await Api.RegistrarAsync(f, "sqlnacional");
        var anonimo = f.CreateClient();

        // publicación en Medellín con condición, filtros por departamento/condición y vitrina (NEWID())
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Bicicleta", descripcion = "Rin 26", categoriaId = 1, modo = "Compra", precioReferenciaCop = 300000,
            condicion = "Reparado", detalleCondicion = "Se cambiaron los frenos", localidad = "Laureles", municipioCodigo = "05001"
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pub = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var filtrado = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/publicaciones?departamentoCodigo=05&municipioCodigo=05001&condicion=Reparado&orden=Recientes");
        Assert.Equal(1, filtrado.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones/destacadas?departamentoCodigo=05")).StatusCode);

        // pagos simulados: recarga → impulso; Premium → factura; estadísticas
        async Task PagarAsync(object cuerpo)
        {
            var pago = await (await c.PostAsJsonAsync("/api/v1/pagos/iniciar", cuerpo)).Content.ReadFromJsonAsync<JsonElement>();
            (await c.PostAsync($"/api/v1/pagos/{pago.GetProperty("referencia").GetString()}/simular?aprobado=true", null)).EnsureSuccessStatusCode();
        }
        await PagarAsync(new { concepto = "Recarga", montoRecargaCop = 2000 });
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/v1/publicaciones/{pub}/impulsar", null)).StatusCode);
        await PagarAsync(new { concepto = "Premium" });
        var facturas = await c.GetFromJsonAsync<JsonElement>("/api/v1/cuenta/facturas");
        Assert.Equal(2, facturas.GetArrayLength());

        // vistas: UPDATE atómico (ExecuteUpdate) e inserción del día
        using (var scope = f.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IEstadisticaRepository>();
            await repo.SumarVistasAsync(new[] { (pub, DateTime.UtcNow, 2) });
            await repo.SumarVistasAsync(new[] { (pub, DateTime.UtcNow, 3) });
        }
        var stats = await c.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}/estadisticas");
        Assert.Equal(5, stats.GetProperty("vistasTotales").GetInt32());
        Assert.True(stats.GetProperty("serieDisponible").GetBoolean());

        // finanzas (GroupBy por año/mes en SQL) y colas de facturas y PQR
        var super = Api.ConToken(f, await Api.LoginAsync(f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var ingresos = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/ingresos");
        Assert.Equal(17_000, ingresos.GetProperty("aprobadoCop").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await super.GetAsync("/api/v1/admin/ingresos.csv")).StatusCode);
        Assert.Equal(2, (await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/facturas")).GetProperty("total").GetInt32());
        (await c.PostAsJsonAsync("/api/v1/pqr", new { tipo = "Queja", asunto = "Demora en la entrega", descripcion = "El vendedor no respondió en el chat." })).EnsureSuccessStatusCode();
        Assert.Equal(1, (await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/pqr")).GetProperty("total").GetInt32());
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
