using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class PqrsPublica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "Pqrs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "CorreoSolicitante",
                table: "Pqrs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreSolicitante",
                table: "Pqrs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pqrs_CorreoSolicitante_FechaUtc",
                table: "Pqrs",
                columns: new[] { "CorreoSolicitante", "FechaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pqrs_CorreoSolicitante_FechaUtc",
                table: "Pqrs");

            migrationBuilder.DropColumn(
                name: "CorreoSolicitante",
                table: "Pqrs");

            migrationBuilder.DropColumn(
                name: "NombreSolicitante",
                table: "Pqrs");

            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "Pqrs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
