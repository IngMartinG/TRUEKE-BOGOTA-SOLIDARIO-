using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class BusquedaNormalizada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextoBusqueda",
                table: "Publicaciones",
                type: "nvarchar(2600)",
                maxLength: 2600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TituloBusqueda",
                table: "Publicaciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextoBusqueda",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "TituloBusqueda",
                table: "Publicaciones");
        }
    }
}
