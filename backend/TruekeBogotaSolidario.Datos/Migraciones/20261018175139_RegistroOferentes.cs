using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class RegistroOferentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OferenteDireccionCifrada",
                table: "Usuarios",
                type: "character varying(800)",
                unicode: false,
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OferenteDocumentoCifrado",
                table: "Usuarios",
                type: "character varying(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OferenteMunicipioCodigo",
                table: "Usuarios",
                type: "character varying(5)",
                unicode: false,
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OferenteNombre",
                table: "Usuarios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OferenteRegistradoUtc",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OferenteTelefonoCifrado",
                table: "Usuarios",
                type: "character varying(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OferenteTipoDocumento",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OferenteDireccionCifrada",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "OferenteDocumentoCifrado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "OferenteMunicipioCodigo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "OferenteNombre",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "OferenteRegistradoUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "OferenteTelefonoCifrado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "OferenteTipoDocumento",
                table: "Usuarios");
        }
    }
}
