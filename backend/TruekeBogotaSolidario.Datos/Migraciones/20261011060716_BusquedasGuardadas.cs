using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class BusquedasGuardadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusquedasGuardadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    Modo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Condicion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DepartamentoCodigo = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: true),
                    MunicipioCodigo = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    Localidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PrecioMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PrecioMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SoloVerificados = table.Column<bool>(type: "bit", nullable: false),
                    AlertasActivas = table.Column<bool>(type: "bit", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisadaHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusquedasGuardadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusquedasGuardadas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusquedasGuardadas_AlertasActivas_RevisadaHastaUtc",
                table: "BusquedasGuardadas",
                columns: new[] { "AlertasActivas", "RevisadaHastaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusquedasGuardadas_UsuarioId",
                table: "BusquedasGuardadas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusquedasGuardadas");
        }
    }
}
