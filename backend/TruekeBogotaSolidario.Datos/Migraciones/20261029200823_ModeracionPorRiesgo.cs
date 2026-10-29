using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ModeracionPorRiesgo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EnRevisionDesdeUtc",
                table: "Publicaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoModeracion",
                table: "Publicaciones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Aprobada"); // lo ya publicado sigue en el catálogo

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "Publicaciones",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivosRiesgo",
                table: "Publicaciones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PuntajeRiesgo",
                table: "Publicaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_EstadoModeracion",
                table: "Publicaciones",
                column: "EstadoModeracion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_EstadoModeracion",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "EnRevisionDesdeUtc",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "EstadoModeracion",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "MotivosRiesgo",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "PuntajeRiesgo",
                table: "Publicaciones");
        }
    }
}
