using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class MensajesMarcados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MarcaRevisadaUtc",
                table: "Mensajes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MarcadoUtc",
                table: "Mensajes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRevision",
                table: "Mensajes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_MarcadoUtc",
                table: "Mensajes",
                column: "MarcadoUtc",
                filter: "\"MarcadoUtc\" IS NOT NULL AND \"MarcaRevisadaUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mensajes_MarcadoUtc",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "MarcaRevisadaUtc",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "MarcadoUtc",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "MotivoRevision",
                table: "Mensajes");
        }
    }
}
