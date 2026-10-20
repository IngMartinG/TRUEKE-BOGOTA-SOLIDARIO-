using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class NivelesEcoPuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PuntosParticipacion",
                table: "Usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Nivel de las cuentas que ya existen: lo ganado participando según su historial (nunca lo recargado).
            migrationBuilder.Sql("""
                UPDATE "Usuarios" u
                SET "PuntosParticipacion" = m.total
                FROM (
                    SELECT "UsuarioId", SUM("Puntos") AS total
                    FROM "MovimientosEcoPuntos"
                    WHERE "Puntos" > 0 AND "Concepto" IN ('SaldoInicial', 'Bienvenida', 'Intercambio', 'Invitacion')
                    GROUP BY "UsuarioId"
                ) m
                WHERE u."Id" = m."UsuarioId" AND NOT u."EstaEliminado";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PuntosParticipacion",
                table: "Usuarios");
        }
    }
}
