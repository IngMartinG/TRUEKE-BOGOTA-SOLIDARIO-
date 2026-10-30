using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Retencion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ResumenSemanal",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResumenSemanalEnviadoUtc",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisponibilidadConfirmadaUtc",
                table: "Publicaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisponibilidadPedidaUtc",
                table: "Publicaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MetasComunitarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false),
                    Localidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MetaKg = table.Column<decimal>(type: "numeric(10,0)", precision: 10, scale: 0, nullable: false),
                    CreadaPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasComunitarias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Estado_DisponibilidadPedidaUtc",
                table: "Publicaciones",
                columns: new[] { "Estado", "DisponibilidadPedidaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MetasComunitarias_MunicipioCodigo_Localidad",
                table: "MetasComunitarias",
                columns: new[] { "MunicipioCodigo", "Localidad" },
                unique: true);

            // Como todas las tablas (CerrarDataApiSupabase): RLS sin políticas, para que la Data API de Supabase no la vea.
            migrationBuilder.Sql("""ALTER TABLE "MetasComunitarias" ENABLE ROW LEVEL SECURITY;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetasComunitarias");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_Estado_DisponibilidadPedidaUtc",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "ResumenSemanal",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ResumenSemanalEnviadoUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DisponibilidadConfirmadaUtc",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "DisponibilidadPedidaUtc",
                table: "Publicaciones");
        }
    }
}
