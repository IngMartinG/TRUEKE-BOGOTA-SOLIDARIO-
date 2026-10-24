using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <summary>
    /// Fundaciones verificadas: perfil de fundación por cuenta (con el certificado privado mientras se revisa), "Lo que necesitamos"
    /// y la razón social copiada en Usuarios para mostrar la insignia sin consultar otra tabla.
    /// </summary>
    public partial class FundacionesVerificadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NombreFundacion",
                table: "Usuarios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fundaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    RazonSocial = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Nit = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    MostrarNit = table.Column<bool>(type: "boolean", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CertificadoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MotivoDecision = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FechaSolicitudUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaDecisionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fundaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fundaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NecesidadesFundacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoriaId = table.Column<int>(type: "integer", nullable: true),
                    Cantidad = table.Column<int>(type: "integer", nullable: true),
                    Urgente = table.Column<bool>(type: "boolean", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NecesidadesFundacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NecesidadesFundacion_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NecesidadesFundacion_Fundaciones_FundacionId",
                        column: x => x.FundacionId,
                        principalTable: "Fundaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fundaciones_Estado",
                table: "Fundaciones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Fundaciones_Nit",
                table: "Fundaciones",
                column: "Nit");

            migrationBuilder.CreateIndex(
                name: "IX_Fundaciones_UsuarioId",
                table: "Fundaciones",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NecesidadesFundacion_CategoriaId",
                table: "NecesidadesFundacion",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_NecesidadesFundacion_FundacionId",
                table: "NecesidadesFundacion",
                column: "FundacionId");

            // Como todas las tablas (CerrarDataApiSupabase): RLS sin políticas, para que la Data API de Supabase no las vea.
            // La API es la dueña de las tablas y no se ve afectada.
            migrationBuilder.Sql("""ALTER TABLE "Fundaciones" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "NecesidadesFundacion" ENABLE ROW LEVEL SECURITY;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NecesidadesFundacion");

            migrationBuilder.DropTable(
                name: "Fundaciones");

            migrationBuilder.DropColumn(
                name: "NombreFundacion",
                table: "Usuarios");
        }
    }
}
