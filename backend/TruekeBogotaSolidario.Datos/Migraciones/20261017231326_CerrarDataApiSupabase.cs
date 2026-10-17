using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <summary>
    /// Supabase publica el esquema "public" por su Data API (PostgREST) a los roles anon y authenticated, y la llave anon
    /// es pública. Toda la lectura y escritura debe pasar por nuestra API (JWT, roles, reglas de negocio), así que se les
    /// quitan los permisos (también para tablas futuras) y se activa RLS sin políticas: aunque alguien encendiera la
    /// Data API, no vería nada. El dueño de las tablas (la API) no se ve afectado. Solo actúa donde existen esos roles
    /// (Supabase); en Docker local y en las pruebas no hace nada.
    /// </summary>
    public partial class CerrarDataApiSupabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE t record;
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'anon')
                       OR NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'authenticated') THEN
                        RETURN;
                    END IF;
                    REVOKE ALL ON ALL TABLES IN SCHEMA public FROM anon, authenticated;
                    REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM anon, authenticated;
                    REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM anon, authenticated;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON TABLES FROM anon, authenticated;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON SEQUENCES FROM anon, authenticated;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON FUNCTIONS FROM anon, authenticated;
                    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t.tablename);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A propósito no se reabre: devolver permisos a anon expondría todas las tablas por la Data API.
        }
    }
}
