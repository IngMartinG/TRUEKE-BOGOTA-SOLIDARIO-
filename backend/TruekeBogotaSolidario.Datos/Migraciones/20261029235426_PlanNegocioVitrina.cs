using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class PlanNegocioVitrina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionNegocio",
                table: "Usuarios",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HorarioNegocio",
                table: "Usuarios",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NitRechazadoUtc",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NitVerificadoUtc",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionNegocio",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "HorarioNegocio",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NitRechazadoUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NitVerificadoUtc",
                table: "Usuarios");
        }
    }
}
