using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <summary>
    /// Contraofertas y trueke de varios objetos: el objeto ofrecido pasa de una columna de Solicitudes a la tabla
    /// SolicitudObjetos (se copia antes de borrar la columna) y las solicitudes existentes quedan con el turno del dueño.
    /// </summary>
    public partial class Contraofertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudObjetos",
                columns: table => new
                {
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudObjetos", x => new { x.SolicitudId, x.PublicacionId });
                    table.ForeignKey(
                        name: "FK_SolicitudObjetos_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudObjetos_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudObjetos_PublicacionId",
                table: "SolicitudObjetos",
                column: "PublicacionId");

            // Como todas las tablas (CerrarDataApiSupabase): RLS sin políticas, para que la Data API de Supabase no la vea.
            // La API es la dueña de la tabla y no se ve afectada.
            migrationBuilder.Sql("""ALTER TABLE "SolicitudObjetos" ENABLE ROW LEVEL SECURITY;""");

            migrationBuilder.Sql("""
                INSERT INTO "SolicitudObjetos" ("SolicitudId", "PublicacionId", "Orden")
                SELECT "Id", "PublicacionOfrecidaId", 0 FROM "Solicitudes" WHERE "PublicacionOfrecidaId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Solicitudes_Publicaciones_PublicacionOfrecidaId",
                table: "Solicitudes");

            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_PublicacionOfrecidaId_Estado",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "PublicacionOfrecidaId",
                table: "Solicitudes");

            migrationBuilder.AddColumn<int>(
                name: "Contraofertas",
                table: "Solicitudes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioPropuestoCop",
                table: "Solicitudes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Hasta ahora solo el dueño respondía: las solicitudes existentes quedan con su turno.
            migrationBuilder.AddColumn<bool>(
                name: "TurnoDuenio",
                table: "Solicitudes",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicacionOfrecidaId",
                table: "Solicitudes",
                type: "uuid",
                nullable: true);

            // Solo cabe un objeto por solicitud: se conserva el primero de cada propuesta.
            migrationBuilder.Sql("""
                UPDATE "Solicitudes" s SET "PublicacionOfrecidaId" = o."PublicacionId"
                FROM "SolicitudObjetos" o WHERE o."SolicitudId" = s."Id" AND o."Orden" = 0;
                """);

            migrationBuilder.DropTable(
                name: "SolicitudObjetos");

            migrationBuilder.DropColumn(
                name: "Contraofertas",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "PrecioPropuestoCop",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "TurnoDuenio",
                table: "Solicitudes");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_PublicacionOfrecidaId_Estado",
                table: "Solicitudes",
                columns: new[] { "PublicacionOfrecidaId", "Estado" });

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitudes_Publicaciones_PublicacionOfrecidaId",
                table: "Solicitudes",
                column: "PublicacionOfrecidaId",
                principalTable: "Publicaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
