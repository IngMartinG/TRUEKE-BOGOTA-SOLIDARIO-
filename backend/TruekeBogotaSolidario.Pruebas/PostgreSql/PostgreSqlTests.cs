using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        Assert.Contains(cercanas.EnumerateArray(), e => e.GetProperty("publicacion").GetProperty("id").GetGuid() == pub);

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
    public async Task Las_consultas_de_los_bonos_de_Eco_Puntos_se_traducen_a_SQL()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        await using var f = new FabricaApiPostgreSql(_sql.NuevaBase());
        var c = await Api.RegistrarAsync(f, "sqlbonos");
        var pub = await Api.CrearPublicacionAsync(c);
        var id = (await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid();

        using var scope = f.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var ahora = DateTime.UtcNow;
        // con un corte en el futuro, la publicación recién creada ya "cumple" el tiempo activa
        Assert.Contains(id, await sp.GetRequiredService<IUsuarioRepository>().ListarConBonosPendientesAsync(ahora.AddHours(1), ahora, 50));
        Assert.Equal(pub, (await sp.GetRequiredService<IPublicacionRepository>().PrimeraActivaDesdeAsync(id, ahora.AddHours(1)))!.Id);
        Assert.Null(await sp.GetRequiredService<IPublicacionRepository>().PrimeraActivaDesdeAsync(id, ahora.AddHours(-1)));
        Assert.Equal(1, await sp.GetRequiredService<IMovimientoEcoPuntosRepository>()
            .ContarGanadosDesdeAsync(id, Datos.Entidades.ConceptoEcoPuntos.PerfilCompleto, ahora.AddHours(-1)));
    }

    [SkippableFact]
    public async Task La_migracion_del_aviso_avisa_una_vez_a_quien_tiene_ventas_sin_trueke()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using (var db = Contexto(cadena)) await db.GetService<IMigrator>().MigrateAsync("20261026024017_VentaAceptaTrueke");

        Datos.Entidades.Usuario Persona(string n) => new($"{n} Prueba", "Suba", $"{n}{Guid.NewGuid():N}@t.co", "Clave12345");
        var conVentas = Persona("ventas");
        var conTrueke = Persona("contrueke");
        var soloTrueke = Persona("trueke");
        var cancelada = Persona("cancelada");
        await using (var db = Contexto(cadena))
        {
            // Los datos se insertan con el modelo ACTUAL: las columnas de migraciones posteriores se crean un momento y se
            // quitan antes de migrar (si una migración nueva agrega columnas a estas tablas, se suman aquí).
            await db.Database.ExecuteSqlRawAsync(ColumnasPosteriores);
            var categoria = await db.Categorias.FirstAsync();
            Datos.Entidades.Publicacion Pub(Datos.Entidades.Usuario u, Datos.Entidades.ModoTransaccion modo, bool aceptaTrueke = false) =>
                new(u.Id, new Datos.Entidades.DatosPublicacion("Bicicleta de prueba", "En buen estado", categoria, modo,
                    Datos.Entidades.CondicionProducto.Usado, null, Divipola.CodigoBogota, "Suba",
                    modo == Datos.Entidades.ModoTransaccion.Compra ? 50_000 : null, null, null, null, aceptaTrueke));
            db.Usuarios.AddRange(conVentas, conTrueke, soloTrueke, cancelada);
            var vieja = Pub(cancelada, Datos.Entidades.ModoTransaccion.Compra);
            vieja.Cancelar("Ya no la vendo");
            db.Publicaciones.AddRange(
                Pub(conVentas, Datos.Entidades.ModoTransaccion.Compra), Pub(conVentas, Datos.Entidades.ModoTransaccion.Compra),
                Pub(conTrueke, Datos.Entidades.ModoTransaccion.Compra, aceptaTrueke: true),
                Pub(soloTrueke, Datos.Entidades.ModoTransaccion.Trueke), vieja);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(QuitarColumnasPosteriores);
        }

        await using (var db = Contexto(cadena)) await db.Database.MigrateAsync();

        await using var lectura = Contexto(cadena);
        var avisos = await lectura.Notificaciones.AsNoTracking().Where(n => n.Tipo == "NovedadVentaTrueke").ToListAsync();
        var aviso = Assert.Single(avisos); // una por persona, aunque tenga dos ventas
        Assert.Equal(conVentas.Id, aviso.UsuarioId);
        Assert.Null(aviso.LeidaUtc);
        Assert.Equal(DateTimeKind.Utc, aviso.FechaUtc.Kind);
    }

    /// <summary>Columnas de Usuarios agregadas después de la migración VentaAceptaTrueke (FotoPerfilReal).</summary>
    private const string ColumnasPosteriores = """
        ALTER TABLE "Usuarios" ADD "EstadoFoto" character varying(20) NOT NULL DEFAULT 'SinRevisar';
        ALTER TABLE "Usuarios" ADD "FotoRevisadaUtc" timestamp with time zone;
        ALTER TABLE "Usuarios" ADD "MotivoRechazoFoto" character varying(300);
        ALTER TABLE "Usuarios" ADD "PlazoCambioFotoUtc" timestamp with time zone;
        ALTER TABLE "Publicaciones" ADD "EstadoModeracion" character varying(20) NOT NULL DEFAULT 'Aprobada';
        ALTER TABLE "Publicaciones" ADD "PuntajeRiesgo" integer NOT NULL DEFAULT 0;
        ALTER TABLE "Publicaciones" ADD "MotivosRiesgo" character varying(500);
        ALTER TABLE "Publicaciones" ADD "MotivoRechazo" character varying(300);
        ALTER TABLE "Publicaciones" ADD "EnRevisionDesdeUtc" timestamp with time zone;
        ALTER TABLE "Publicaciones" ADD "PesoEstimadoKg" numeric(8,2);
        ALTER TABLE "Publicaciones" ADD "Material" character varying(20);
        """;
    private const string QuitarColumnasPosteriores = """
        ALTER TABLE "Usuarios" DROP COLUMN "EstadoFoto", DROP COLUMN "FotoRevisadaUtc", DROP COLUMN "MotivoRechazoFoto",
            DROP COLUMN "PlazoCambioFotoUtc";
        ALTER TABLE "Publicaciones" DROP COLUMN "EstadoModeracion", DROP COLUMN "PuntajeRiesgo", DROP COLUMN "MotivosRiesgo",
            DROP COLUMN "MotivoRechazo", DROP COLUMN "EnRevisionDesdeUtc", DROP COLUMN "PesoEstimadoKg", DROP COLUMN "Material";
        """;

    [SkippableFact]
    public async Task La_migracion_de_impacto_registra_solo_los_intercambios_que_ambas_partes_confirmaron()
    {
        Skip.IfNot(_sql.Disponible, _sql.MotivoNoDisponible);
        var cadena = _sql.NuevaBase();
        await using (var db = Contexto(cadena)) await db.GetService<IMigrator>().MigrateAsync("20261029200823_ModeracionPorRiesgo");

        Datos.Entidades.Usuario Persona(string n) => new($"{n} Prueba", "Suba", $"{n}{Guid.NewGuid():N}@t.co", "Clave12345");
        var duenio = Persona("duenio");
        var otro = Persona("otro");
        Guid confirmada, sola;
        await using (var db = Contexto(cadena))
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Publicaciones" ADD "PesoEstimadoKg" numeric(8,2);
                ALTER TABLE "Publicaciones" ADD "Material" character varying(20);
                """);
            var ropa = await db.Categorias.FirstAsync(c => c.Id == 1);
            var ahora = DateTime.UtcNow;
            Datos.Entidades.Publicacion Pub() => new(duenio.Id, new Datos.Entidades.DatosPublicacion("Chaqueta", "Abrigada", ropa,
                Datos.Entidades.ModoTransaccion.Donacion, Datos.Entidades.CondicionProducto.Usado, null, Divipola.CodigoBogota, "Suba",
                null, null, null, null));
            Datos.Entidades.Transaccion Intercambio(Datos.Entidades.Publicacion p, bool ambas)
            {
                var s = new Datos.Entidades.Solicitud(p.Id, otro.Id, Datos.Entidades.ModoTransaccion.Donacion, "La necesito");
                s.Aceptar(true, ahora);
                s.ConfirmarEntrega(true, ahora);
                if (ambas) s.ConfirmarEntrega(false, ahora);
                s.Completar(ahora);
                db.Solicitudes.Add(s);
                return new Datos.Entidades.Transaccion(p.Id, s.Id, duenio.Id, otro.Id, Datos.Entidades.ModoTransaccion.Donacion, ambas, ambas, ahora);
            }
            db.Usuarios.AddRange(duenio, otro);
            var p1 = Pub();
            var p2 = Pub();
            db.Publicaciones.AddRange(p1, p2);
            var t1 = Intercambio(p1, ambas: true);
            var t2 = Intercambio(p2, ambas: false);
            db.Transacciones.AddRange(t1, t2);
            await db.SaveChangesAsync();
            (confirmada, sola) = (t1.Id, t2.Id);
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Publicaciones" DROP COLUMN "PesoEstimadoKg", DROP COLUMN "Material";""");
        }

        await using (var db = Contexto(cadena)) await db.Database.MigrateAsync();

        await using var lectura = Contexto(cadena);
        var registros = await lectura.RegistrosImpacto.AsNoTracking().ToListAsync();
        var r = Assert.Single(registros); // el que se cerró con una sola confirmación no cuenta
        Assert.Equal(confirmada, r.TransaccionId);
        Assert.Equal((0.5m, Datos.Entidades.MaterialObjeto.Textil, 4m, "2.0.0"), (r.Kg, r.Material, r.KgCo2, r.Version));
        Assert.Equal(Datos.Entidades.OrigenPeso.EstimadoPorCategoria, r.OrigenPeso);
        Assert.Equal((duenio.Id, otro.Id, "Suba"), (r.OferenteId, r.ReceptorId, r.Localidad));
        Assert.DoesNotContain(registros, x => x.TransaccionId == sola);
        Assert.Equal(9, await lectura.FactoresImpacto.CountAsync());
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
