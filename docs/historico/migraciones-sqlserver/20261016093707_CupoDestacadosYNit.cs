using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class CupoDestacadosYNit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MostrarNit",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProximaRecargaDestacadosUtc",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            // Planes vigentes: su ciclo de destacados termina con el periodo ya pagado (antes el cupo se reponía al pagar)
            migrationBuilder.Sql(
                "UPDATE [Usuarios] SET [ProximaRecargaDestacadosUtc] = [FechaVencimientoSuscripcion] " +
                "WHERE [TipoCuenta] IN ('Premium', 'Empresa') AND [FechaVencimientoSuscripcion] > SYSUTCDATETIME();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MostrarNit",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ProximaRecargaDestacadosUtc",
                table: "Usuarios");
        }
    }
}
