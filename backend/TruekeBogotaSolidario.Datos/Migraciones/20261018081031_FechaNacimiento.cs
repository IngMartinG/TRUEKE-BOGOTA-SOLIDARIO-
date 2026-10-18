using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FechaNacimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaNacimiento",
                table: "Usuarios",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaNacimiento",
                table: "Usuarios");
        }
    }
}
