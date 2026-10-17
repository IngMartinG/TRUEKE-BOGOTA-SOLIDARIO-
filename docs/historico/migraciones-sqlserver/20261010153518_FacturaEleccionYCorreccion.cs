using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FacturaEleccionYCorreccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facturas_PagoId",
                table: "Facturas");

            migrationBuilder.AddColumn<string>(
                name: "CompradorCorreo",
                table: "Pagos",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompradorDireccion",
                table: "Pagos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompradorDocumento",
                table: "Pagos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompradorMunicipioCodigo",
                table: "Pagos",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompradorNombre",
                table: "Pagos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompradorTipoDocumento",
                table: "Pagos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FacturaANombre",
                table: "Pagos",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEleccionFacturaUtc",
                table: "Pagos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorregidaPorId",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ElegidaANombre",
                table: "Facturas",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCorreccionUtc",
                table: "Facturas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEleccionUtc",
                table: "Facturas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCorreccion",
                table: "Facturas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReemplazaAId",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReemplazadaPorId",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_PagoId",
                table: "Facturas",
                column: "PagoId",
                unique: true,
                filter: "[Estado] IN (N'Pendiente', N'Emitida')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facturas_PagoId",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "CompradorCorreo",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CompradorDireccion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CompradorDocumento",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CompradorMunicipioCodigo",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CompradorNombre",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CompradorTipoDocumento",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FacturaANombre",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FechaEleccionFacturaUtc",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "CorregidaPorId",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ElegidaANombre",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FechaCorreccionUtc",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FechaEleccionUtc",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "MotivoCorreccion",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ReemplazaAId",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ReemplazadaPorId",
                table: "Facturas");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_PagoId",
                table: "Facturas",
                column: "PagoId",
                unique: true);
        }
    }
}
