using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FundacionesProgresoYRegimen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NecesidadFundacionId",
                table: "Publicaciones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CubiertaUtc",
                table: "NecesidadesFundacion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Recibidos",
                table: "NecesidadesFundacion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NotaRegimen",
                table: "Fundaciones",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegimenRevisadoUtc",
                table: "Fundaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RegimenTributarioEspecial",
                table: "Fundaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_NecesidadFundacionId",
                table: "Publicaciones",
                column: "NecesidadFundacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_NecesidadFundacionId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "NecesidadFundacionId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "CubiertaUtc",
                table: "NecesidadesFundacion");

            migrationBuilder.DropColumn(
                name: "Recibidos",
                table: "NecesidadesFundacion");

            migrationBuilder.DropColumn(
                name: "NotaRegimen",
                table: "Fundaciones");

            migrationBuilder.DropColumn(
                name: "RegimenRevisadoUtc",
                table: "Fundaciones");

            migrationBuilder.DropColumn(
                name: "RegimenTributarioEspecial",
                table: "Fundaciones");
        }
    }
}
