using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.PostgreSql;

/// <summary>
/// Lo que la base InMemory NO puede comprobar: que las migraciones se aplican en PostgreSQL, que todas las consultas LINQ
/// se traducen a SQL, la concurrencia con xmin (409), las fechas UTC (timestamptz) y los índices únicos.
/// </summary>
[Trait("Categoria", "PostgreSQL")]
public class PostgreSqlTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _sql;
    public PostgreSqlTests(PostgreSqlFixture sql) => _sql = sql;

    private sealed class FabricaApiPostgreSql : FabricaApi
    {
        private readonly string _cadena;
        public FabricaApiPostgreSql(string cadena) => _cadena = cadena;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("ConnectionStrings:TruekeDb", _cadena);
            builder.UseSetting("Database:Inicializacion", "Migrate"); // igual que producción
        }
    }

    private TruekeDbContext Contexto(string cadena) => new(new DbContextOptionsBuilder<TruekeDbContext>().UseNpgsql(cadena).Options);

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
    public async Task Flujo_completo_de_la_API_sobre_PostgreSQL()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        await using var f = new FabricaApiPostgreSql(_sql.NuevaBase());
        var duenio = await Api.RegistrarAsync(f, "sqlana");
        var otro = await Api.RegistrarAsync(f, "sqlbeto");
        var anonimo = f.CreateClient();

        // catálogo: filtros, orden por precio, texto, cercanía (consultas traducidas a SQL)
        var pub = await Api.CrearPublicacionAsync(duenio, 4.6097, -74.0817, modo: "Compra");
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones?orden=PrecioAsc&precioMin=1&texto=Bici&soloVerificados=false")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones?texto=bicicletas%20rojas")).StatusCode); // orden por relevancia
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones/sugerencias?texto=bici")).StatusCode);
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
    public async Task Monetizacion_y_escala_nacional_sobre_PostgreSQL()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        await using var f = new FabricaApiPostgreSql(_sql.NuevaBase());
        var c = await Api.RegistrarAsync(f, "sqlnacional");
        var anonimo = f.CreateClient();

        // publicación en Medellín con condición, filtros por departamento/condición y vitrina (gen_random_uuid())
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
        await PagarAsync(new { concepto = "Recarga", montoRecargaCop = 5000, facturaANombre = false, aceptoConsumidorFinal = true });
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/v1/publicaciones/{pub}/impulsar", null)).StatusCode);
        await PagarAsync(new { concepto = "Premium", facturaANombre = false, aceptoConsumidorFinal = true });
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
        Assert.Equal(20_000, ingresos.GetProperty("aprobadoCop").GetInt64()); // recarga de 5.000 + Premium de 15.000
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

    /// <summary>
    /// En Supabase los roles anon y authenticated (los de su Data API, con llave pública) reciben permisos por defecto sobre
    /// "public". Se simula ese ambiente y se comprueba que, tras migrar, no pueden leer ni escribir y todas las tablas tienen RLS.
    /// </summary>
    [SkippableFact]
    public async Task En_Supabase_la_Data_API_no_puede_tocar_las_tablas()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using var db = Contexto(cadena);
        await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'anon') THEN CREATE ROLE anon NOLOGIN; END IF;
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF;
            END $$;
            GRANT USAGE ON SCHEMA public TO anon, authenticated;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO anon, authenticated;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO anon, authenticated;
            """);

        await db.Database.MigrateAsync();

        var conPermiso = await db.Database.SqlQueryRaw<string>("""
            SELECT c.relname AS "Value" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r'
              AND (has_table_privilege('anon', c.oid, 'SELECT,INSERT,UPDATE,DELETE')
                OR has_table_privilege('authenticated', c.oid, 'SELECT,INSERT,UPDATE,DELETE'))
            """).ToListAsync();
        Assert.Empty(conPermiso);
        var sinRls = await db.Database.SqlQueryRaw<string>("""
            SELECT c.relname AS "Value" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND NOT c.relrowsecurity
            """).ToListAsync();
        Assert.Empty(sinRls);
        Assert.Equal(7, await db.Categorias.CountAsync()); // la API (dueña de las tablas) sigue leyendo normal
    }

    [SkippableFact]
    public async Task Las_fechas_se_guardan_en_timestamptz_y_vuelven_en_UTC()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using (var db = Contexto(cadena)) await db.Database.MigrateAsync();

        var u = new Datos.Entidades.Usuario("Fecha Prueba", "Suba", $"f{Guid.NewGuid():N}@t.co", "Clave12345");
        await using (var db = Contexto(cadena))
        {
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
        }

        await using var lectura = Contexto(cadena);
        var leido = await lectura.Usuarios.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        Assert.Equal(DateTimeKind.Utc, leido.FechaRegistro.Kind);
        Assert.True((leido.FechaRegistro - u.FechaRegistro).Duration() < TimeSpan.FromMilliseconds(1)); // PostgreSQL guarda microsegundos
        Assert.True(await lectura.Usuarios.AnyAsync(x => x.FechaRegistro <= DateTime.UtcNow)); // parámetro UTC en la consulta
    }

    [SkippableFact]
    public async Task Reemplazar_una_factura_emitida_respeta_el_indice_de_factura_vigente()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        await using var f = new FabricaApiPostgreSql(_sql.NuevaBase());
        var c = await Api.RegistrarAsync(f, "sqlfactura");
        var pago = await (await c.PostAsJsonAsync("/api/v1/pagos/iniciar",
            new { concepto = "Premium", facturaANombre = false, aceptoConsumidorFinal = true })).Content.ReadFromJsonAsync<JsonElement>();
        (await c.PostAsync($"/api/v1/pagos/{pago.GetProperty("referencia").GetString()}/simular?aprobado=true", null)).EnsureSuccessStatusCode();
        var factura = (await c.GetFromJsonAsync<JsonElement>("/api/v1/cuenta/facturas"))[0].GetProperty("id").GetGuid();

        var super = Api.ConToken(f, await Api.LoginAsync(f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        (await super.PostAsJsonAsync($"/api/v1/admin/facturas/{factura}/emitida", new { numeroDian = "FE-9", cufe = new string('c', 96) })).EnsureSuccessStatusCode();
        var cuerpo = new
        {
            comprador = new { tipoDocumento = "CC", documento = "1020304050", nombre = "Ana Prueba", correo = "ana@t.co", direccion = "Calle 10 # 20-30", municipioCodigo = "11001" },
            motivo = "La persona pidió la factura a su nombre por PQR"
        };
        var r = await super.PutAsJsonAsync($"/api/v1/admin/facturas/{factura}/comprador", cuerpo);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); // la transacción ordena las escrituras
        var nueva = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pendiente", nueva.GetProperty("estado").GetString());
        Assert.Equal(factura, nueva.GetProperty("reemplazaAId").GetGuid());
        // la original ya no es vigente: no se puede reemplazar dos veces
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PutAsJsonAsync($"/api/v1/admin/facturas/{factura}/comprador", cuerpo)).StatusCode);
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
