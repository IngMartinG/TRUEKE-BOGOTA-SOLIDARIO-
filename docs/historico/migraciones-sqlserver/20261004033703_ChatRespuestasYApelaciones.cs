using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ChatRespuestasYApelaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RespuestaAId",
                table: "Mensajes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Accion",
                table: "Denuncias",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DenunciadoId",
                table: "Denuncias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolucionId",
                table: "Denuncias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Apelaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolucionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ObjetivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccionOriginal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ModeradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaResolucion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apelaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apelaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_RespuestaAId",
                table: "Mensajes",
                column: "RespuestaAId");

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_DenunciadoId_Estado",
                table: "Denuncias",
                columns: new[] { "DenunciadoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_ResolucionId",
                table: "Denuncias",
                column: "ResolucionId");

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_Estado_FechaUtc",
                table: "Apelaciones",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_ResolucionId",
                table: "Apelaciones",
                column: "ResolucionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_UsuarioId",
                table: "Apelaciones",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mensajes_Mensajes_RespuestaAId",
                table: "Mensajes",
                column: "RespuestaAId",
                principalTable: "Mensajes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mensajes_Mensajes_RespuestaAId",
                table: "Mensajes");

            migrationBuilder.DropTable(
                name: "Apelaciones");

            migrationBuilder.DropIndex(
                name: "IX_Mensajes_RespuestaAId",
                table: "Mensajes");

            migrationBuilder.DropIndex(
                name: "IX_Denuncias_DenunciadoId_Estado",
                table: "Denuncias");

            migrationBuilder.DropIndex(
                name: "IX_Denuncias_ResolucionId",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "RespuestaAId",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "Accion",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "DenunciadoId",
                table: "Denuncias");

            migrationBuilder.DropColumn(
                name: "ResolucionId",
                table: "Denuncias");
        }
    }
}
