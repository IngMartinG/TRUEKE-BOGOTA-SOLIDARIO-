using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ListaDeDeseos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Deseos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    Publico = table.Column<bool>(type: "bit", nullable: false),
                    AvisosActivos = table.Column<bool>(type: "bit", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisadoHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstaOculto = table.Column<bool>(type: "bit", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deseos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deseos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deseos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deseos_AvisosActivos_RevisadoHastaUtc",
                table: "Deseos",
                columns: new[] { "AvisosActivos", "RevisadoHastaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Deseos_CategoriaId",
                table: "Deseos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Deseos_UsuarioId",
                table: "Deseos",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deseos");
        }
    }
}
