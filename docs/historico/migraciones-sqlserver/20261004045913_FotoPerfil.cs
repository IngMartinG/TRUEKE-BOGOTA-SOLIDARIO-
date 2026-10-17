using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FotoPerfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoUrl",
                table: "Usuarios",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoUrl",
                table: "Usuarios");
        }
    }
}
