using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FotoPublica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoPublicaUrl",
                table: "Usuarios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Los demás solo ven fotos aprobadas o anteriores a la regla (aún sin revisar); las que esperan a un moderador o
            // fueron rechazadas dejan de verse.
            migrationBuilder.Sql("""
                UPDATE "Usuarios" SET "FotoPublicaUrl" = "FotoUrl"
                WHERE "FotoUrl" IS NOT NULL AND "EstadoFoto" IN ('Aprobada', 'SinRevisar');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoPublicaUrl",
                table: "Usuarios");
        }
    }
}
