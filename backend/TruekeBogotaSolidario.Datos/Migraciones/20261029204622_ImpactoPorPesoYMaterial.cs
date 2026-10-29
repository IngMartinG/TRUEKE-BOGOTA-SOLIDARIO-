using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ImpactoPorPesoYMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "Publicaciones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoEstimadoKg",
                table: "Publicaciones",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FactoresImpacto",
                columns: table => new
                {
                    Material = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KgCo2PorKg = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    Fuente = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActualizadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoPorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoresImpacto", x => x.Material);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosImpacto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransaccionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OferenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceptorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    OrigenPeso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Material = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FactorKgCo2PorKg = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    KgCo2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false),
                    Localidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosImpacto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosImpacto_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosImpacto_Transacciones_TransaccionId",
                        column: x => x.TransaccionId,
                        principalTable: "Transacciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "FactoresImpacto",
                columns: new[] { "Material", "ActualizadoPorId", "ActualizadoUtc", "Fuente", "KgCo2PorKg", "Version" },
                values: new object[,]
                {
                    { "Electrodomestico", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 5.0m, "2.0.0" },
                    { "Electronica", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 12.0m, "2.0.0" },
                    { "Madera", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 1.2m, "2.0.0" },
                    { "Metal", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 4.0m, "2.0.0" },
                    { "Mixto", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 1.5m, "2.0.0" },
                    { "Papel", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 0.9m, "2.0.0" },
                    { "Plastico", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 3.0m, "2.0.0" },
                    { "Textil", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 8.0m, "2.0.0" },
                    { "Vidrio", null, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja", 0.7m, "2.0.0" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_FechaUtc",
                table: "RegistrosImpacto",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_MunicipioCodigo_Localidad",
                table: "RegistrosImpacto",
                columns: new[] { "MunicipioCodigo", "Localidad" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_OferenteId",
                table: "RegistrosImpacto",
                column: "OferenteId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_PublicacionId",
                table: "RegistrosImpacto",
                column: "PublicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_ReceptorId",
                table: "RegistrosImpacto",
                column: "ReceptorId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_TransaccionId_PublicacionId",
                table: "RegistrosImpacto",
                columns: new[] { "TransaccionId", "PublicacionId" },
                unique: true);
            // Como todas las tablas (CerrarDataApiSupabase): RLS sin políticas, para que la Data API de Supabase no las vea.
            migrationBuilder.Sql("""ALTER TABLE "FactoresImpacto" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "RegistrosImpacto" ENABLE ROW LEVEL SECURITY;""");

            // Intercambios anteriores que ambas partes confirmaron: un registro por objeto entregado, con el peso y el material
            // típicos de su categoría (las publicaciones viejas no los declaraban) y los factores 2.0.0. Los que se cerraron solos
            // con una sola confirmación no se cuentan (metodología: solo lo confirmado por las dos partes).
            migrationBuilder.Sql("""
                WITH tipico(categoria, kg, material) AS (
                    VALUES (1, 0.5, 'Textil'), (2, 0.4, 'Papel'), (3, 6.0, 'Electrodomestico'), (4, 8.0, 'Mixto'),
                           (5, 1.0, 'Electronica'), (6, 0.8, 'Plastico'), (7, 1.0, 'Mixto')),
                entregas AS (
                    SELECT t."Id" AS transaccion, t."PublicacionId" AS publicacion, t."OferenteId" AS de, t."ReceptorId" AS para,
                           t."Modo" AS modo, t."FechaUtc" AS fecha
                    FROM "Transacciones" t JOIN "Solicitudes" s ON s."Id" = t."SolicitudId"
                    WHERE s."ConfirmadaPorDuenioUtc" IS NOT NULL AND s."ConfirmadaPorSolicitanteUtc" IS NOT NULL
                    UNION ALL
                    SELECT t."Id", o."PublicacionId", t."ReceptorId", t."OferenteId", t."Modo", t."FechaUtc"
                    FROM "Transacciones" t JOIN "Solicitudes" s ON s."Id" = t."SolicitudId"
                    JOIN "SolicitudObjetos" o ON o."SolicitudId" = s."Id"
                    WHERE s."ConfirmadaPorDuenioUtc" IS NOT NULL AND s."ConfirmadaPorSolicitanteUtc" IS NOT NULL)
                INSERT INTO "RegistrosImpacto" ("Id", "TransaccionId", "PublicacionId", "CategoriaId", "Modo", "OferenteId", "ReceptorId",
                    "Kg", "OrigenPeso", "Material", "FactorKgCo2PorKg", "KgCo2", "Version", "MunicipioCodigo", "Localidad", "FechaUtc")
                SELECT gen_random_uuid(), e.transaccion, p."Id", p."CategoriaId", e.modo, e.de, e.para,
                       COALESCE(ti.kg, 1.0), 'EstimadoPorCategoria', f."Material", f."KgCo2PorKg",
                       round(COALESCE(ti.kg, 1.0) * f."KgCo2PorKg", 2), f."Version", p."MunicipioCodigo", p."Localidad", e.fecha
                FROM entregas e
                JOIN "Publicaciones" p ON p."Id" = e.publicacion
                LEFT JOIN tipico ti ON ti.categoria = p."CategoriaId"
                JOIN "FactoresImpacto" f ON f."Material" = COALESCE(ti.material, 'Mixto')
                ON CONFLICT ("TransaccionId", "PublicacionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoresImpacto");

            migrationBuilder.DropTable(
                name: "RegistrosImpacto");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "PesoEstimadoKg",
                table: "Publicaciones");
        }
    }
}
