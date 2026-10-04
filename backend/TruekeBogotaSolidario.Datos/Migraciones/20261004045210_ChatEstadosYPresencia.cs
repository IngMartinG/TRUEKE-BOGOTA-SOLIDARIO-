using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ChatEstadosYPresencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EntregadoUtc",
                table: "Mensajes",
                type: "datetime2",
                nullable: true);

            // Lo ya leído también fue entregado (datos existentes coherentes con Mensaje.MarcarLeido)
            migrationBuilder.Sql("UPDATE Mensajes SET EntregadoUtc = LeidoUtc WHERE LeidoUtc IS NOT NULL AND EntregadoUtc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_AutorId_EntregadoUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "AutorId", "EntregadoUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mensajes_ConversacionId_AutorId_EntregadoUtc",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "EntregadoUtc",
                table: "Mensajes");
        }
    }
}
