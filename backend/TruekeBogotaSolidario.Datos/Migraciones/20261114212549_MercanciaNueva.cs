using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class MercanciaNueva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsMercanciaNueva",
                table: "Publicaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Lo que un negocio (plan Negocio vigente) ya tenía publicado como "Nuevo" es mercancía; el resto sigue como estaba.
            migrationBuilder.Sql("""
                UPDATE "Publicaciones" p SET "EsMercanciaNueva" = TRUE
                FROM "Usuarios" u
                WHERE u."Id" = p."PropietarioId" AND p."Condicion" = 'Nuevo'
                  AND u."TipoCuenta" = 'Empresa' AND u."FechaVencimientoSuscripcion" > now();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsMercanciaNueva",
                table: "Publicaciones");
        }
    }
}
