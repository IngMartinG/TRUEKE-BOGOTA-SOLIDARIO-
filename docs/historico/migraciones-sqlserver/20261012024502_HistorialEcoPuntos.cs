using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class HistorialEcoPuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovimientosEcoPuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Puntos = table.Column<int>(type: "int", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    SaldoResultante = table.Column<int>(type: "int", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosEcoPuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosEcoPuntos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosEcoPuntos_UsuarioId_FechaUtc",
                table: "MovimientosEcoPuntos",
                columns: new[] { "UsuarioId", "FechaUtc" });

            // Quien ya tenía puntos arranca su historial con un movimiento "SaldoInicial" por ese saldo, para que la
            // suma del historial siempre coincida con el saldo.
            migrationBuilder.Sql(
                "INSERT INTO [MovimientosEcoPuntos] ([Id], [UsuarioId], [Puntos], [Concepto], [ReferenciaId], [Detalle], [SaldoResultante], [FechaUtc]) " +
                "SELECT NEWID(), [Id], [SaldoEcoPuntos], 'SaldoInicial', NULL, NULL, [SaldoEcoPuntos], SYSUTCDATETIME() FROM [Usuarios] WHERE [SaldoEcoPuntos] > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosEcoPuntos");
        }
    }
}
