using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class BusquedaGuardadaOrigen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsMercanciaNueva",
                table: "BusquedasGuardadas",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsMercanciaNueva",
                table: "BusquedasGuardadas");
        }
    }
}
