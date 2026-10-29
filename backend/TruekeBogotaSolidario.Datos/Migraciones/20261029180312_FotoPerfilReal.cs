using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FotoPerfilReal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoFoto",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SinRevisar");

            migrationBuilder.AddColumn<DateTime>(
                name: "FotoRevisadaUtc",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazoFoto",
                table: "Usuarios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlazoCambioFotoUtc",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EstadoFoto",
                table: "Usuarios",
                column: "EstadoFoto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_EstadoFoto",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EstadoFoto",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FotoRevisadaUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MotivoRechazoFoto",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PlazoCambioFotoUtc",
                table: "Usuarios");
        }
    }
}
