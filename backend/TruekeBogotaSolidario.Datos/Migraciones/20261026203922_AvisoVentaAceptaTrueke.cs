using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class AvisoVentaAceptaTrueke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quien ya tenía ventas publicadas antes de la casilla "También acepto trueke" recibe un aviso en la bandeja
            // (el front lo muestra también como franja al abrir la cuenta). Uno por persona, no uno por publicación.
            migrationBuilder.Sql("""
                INSERT INTO "Notificaciones" ("Id", "UsuarioId", "Tipo", "Mensaje", "RecursoId", "FechaUtc", "LeidaUtc")
                SELECT gen_random_uuid(), u."Id", 'NovedadVentaTrueke',
                       'Novedad: tus ventas ahora también pueden recibir trueke. Edítalas y marca "También acepto trueke" si quieres que te ofrezcan objetos.',
                       NULL, now(), NULL
                FROM "Usuarios" u
                WHERE NOT u."EstaEliminado"
                  AND EXISTS (
                      SELECT 1 FROM "Publicaciones" p
                      WHERE p."PropietarioId" = u."Id" AND p."Modo" = 'Compra' AND NOT p."AceptaTrueke"
                        AND NOT p."EstaOculta" AND p."Estado" IN ('Disponible', 'EnNegociacion'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Notificaciones" WHERE "Tipo" = 'NovedadVentaTrueke';""");
        }
    }
}
