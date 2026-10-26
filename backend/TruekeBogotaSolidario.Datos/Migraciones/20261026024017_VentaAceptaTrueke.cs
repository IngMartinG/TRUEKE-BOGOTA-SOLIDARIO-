using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class VentaAceptaTrueke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Modo",
                table: "Solicitudes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "AceptaTrueke",
                table: "Publicaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Hasta ahora cada solicitud se negociaba en el modo de su publicación.
            migrationBuilder.Sql("""
                UPDATE "Solicitudes" s SET "Modo" = p."Modo" FROM "Publicaciones" p WHERE p."Id" = s."PublicacionId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Modo",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "AceptaTrueke",
                table: "Publicaciones");
        }
    }
}
