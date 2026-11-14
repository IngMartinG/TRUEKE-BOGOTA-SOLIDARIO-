using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ImpactoSinMercanciaNueva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VentaMercanciaNueva",
                table: "Transacciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Metodología 2.1.0: la mercancía nueva ya no suma kilos. Solo se sube la versión de los factores que siguen en la
            // 2.0.0: uno que el SuperUsuario ya cambió (con otra versión) no se toca.
            migrationBuilder.Sql("""
                UPDATE "FactoresImpacto" SET "Version" = '2.1.0' WHERE "Version" = '2.0.0';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VentaMercanciaNueva",
                table: "Transacciones");

            migrationBuilder.Sql("""
                UPDATE "FactoresImpacto" SET "Version" = '2.0.0' WHERE "Version" = '2.1.0';
                """);
        }
    }
}
