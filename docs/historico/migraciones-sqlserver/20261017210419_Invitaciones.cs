using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Invitaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoInvitacion",
                table: "Usuarios",
                type: "varchar(12)",
                unicode: false,
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitadoPorId",
                table: "Usuarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecompensaInvitacionUtc",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CodigoInvitacion",
                table: "Usuarios",
                column: "CodigoInvitacion",
                unique: true,
                filter: "[CodigoInvitacion] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_InvitadoPorId_RecompensaInvitacionUtc",
                table: "Usuarios",
                columns: new[] { "InvitadoPorId", "RecompensaInvitacionUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CodigoInvitacion",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_InvitadoPorId_RecompensaInvitacionUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoInvitacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "InvitadoPorId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "RecompensaInvitacionUtc",
                table: "Usuarios");
        }
    }
}
