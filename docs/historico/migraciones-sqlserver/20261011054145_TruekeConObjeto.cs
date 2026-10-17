using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class TruekeConObjeto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicacionOfrecidaId",
                table: "Solicitudes",
                type: "uniqueidentifier",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Solicitudes_Publicaciones_PublicacionOfrecidaId",
                table: "Solicitudes");

            migrationBuilder.DropIndex(
                name: "IX_Solicitudes_PublicacionOfrecidaId_Estado",
                table: "Solicitudes");

            migrationBuilder.DropColumn(
                name: "PublicacionOfrecidaId",
                table: "Solicitudes");
        }
    }
}
