using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class BloqueosYPreferenciasAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AceptaNovedades",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AvisosCorreoIntercambios",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AvisosCorreoMensajes",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AvisosCorreoPlanes",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Bloqueos",
                columns: table => new
                {
                    BloqueadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BloqueadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bloqueos", x => new { x.BloqueadorId, x.BloqueadoId });
                    table.ForeignKey(
                        name: "FK_Bloqueos_Usuarios_BloqueadoId",
                        column: x => x.BloqueadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bloqueos_Usuarios_BloqueadorId",
                        column: x => x.BloqueadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueos_BloqueadoId",
                table: "Bloqueos",
                column: "BloqueadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bloqueos");

            migrationBuilder.DropColumn(
                name: "AceptaNovedades",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AvisosCorreoIntercambios",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AvisosCorreoMensajes",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AvisosCorreoPlanes",
                table: "Usuarios");
        }
    }
}
