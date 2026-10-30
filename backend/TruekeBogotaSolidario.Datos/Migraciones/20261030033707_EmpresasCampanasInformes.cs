using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class EmpresasCampanasInformes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampanaId",
                table: "RegistrosImpacto",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampanaId",
                table: "Publicaciones",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RazonSocial = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Nit = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CertificadoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MotivoDecision = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SolicitadaPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaSolicitudUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaDecisionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenciasEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenciasEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenciasEntrega_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenciasEntrega_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Campanas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Fin = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadaPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campanas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campanas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MiembrosEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodigoHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaInvitacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaIngresoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembrosEmpresa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiembrosEmpresa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MiembrosEmpresa_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InformesImpacto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampanaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    EmitidoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmitidoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Versiones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contenido = table.Column<string>(type: "text", nullable: false),
                    Huella = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformesImpacto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformesImpacto_Campanas_CampanaId",
                        column: x => x.CampanaId,
                        principalTable: "Campanas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InformesImpacto_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosImpacto_CampanaId_FechaUtc",
                table: "RegistrosImpacto",
                columns: new[] { "CampanaId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_CampanaId",
                table: "Publicaciones",
                column: "CampanaId");

            migrationBuilder.CreateIndex(
                name: "IX_Campanas_EmpresaId",
                table: "Campanas",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Estado",
                table: "Empresas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Nit",
                table: "Empresas",
                column: "Nit");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasEntrega_SolicitudId_UsuarioId",
                table: "EvidenciasEntrega",
                columns: new[] { "SolicitudId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasEntrega_UsuarioId",
                table: "EvidenciasEntrega",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_InformesImpacto_CampanaId",
                table: "InformesImpacto",
                column: "CampanaId");

            migrationBuilder.CreateIndex(
                name: "IX_InformesImpacto_EmpresaId_EmitidoUtc",
                table: "InformesImpacto",
                columns: new[] { "EmpresaId", "EmitidoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosEmpresa_CodigoHash",
                table: "MiembrosEmpresa",
                column: "CodigoHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosEmpresa_EmpresaId",
                table: "MiembrosEmpresa",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosEmpresa_UsuarioId",
                table: "MiembrosEmpresa",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Publicaciones_Campanas_CampanaId",
                table: "Publicaciones",
                column: "CampanaId",
                principalTable: "Campanas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosImpacto_Campanas_CampanaId",
                table: "RegistrosImpacto",
                column: "CampanaId",
                principalTable: "Campanas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Como todas las tablas (CerrarDataApiSupabase): RLS sin políticas, para que la Data API de Supabase no las vea.
            migrationBuilder.Sql("""ALTER TABLE "Empresas" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "MiembrosEmpresa" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "Campanas" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "InformesImpacto" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "EvidenciasEntrega" ENABLE ROW LEVEL SECURITY;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Publicaciones_Campanas_CampanaId",
                table: "Publicaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosImpacto_Campanas_CampanaId",
                table: "RegistrosImpacto");

            migrationBuilder.DropTable(
                name: "EvidenciasEntrega");

            migrationBuilder.DropTable(
                name: "InformesImpacto");

            migrationBuilder.DropTable(
                name: "MiembrosEmpresa");

            migrationBuilder.DropTable(
                name: "Campanas");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosImpacto_CampanaId_FechaUtc",
                table: "RegistrosImpacto");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_CampanaId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "CampanaId",
                table: "RegistrosImpacto");

            migrationBuilder.DropColumn(
                name: "CampanaId",
                table: "Publicaciones");
        }
    }
}
