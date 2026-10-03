using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class EscalaNacionalYMonetizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Calificaciones_AutorId",
                table: "Calificaciones");

            migrationBuilder.AddColumn<bool>(
                name: "BonoBienvenidaOtorgado",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: true); // las cuentas existentes ya recibieron el bono al registrarse

            migrationBuilder.AddColumn<string>(
                name: "CorreoCanonico",
                table: "Usuarios",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FacturacionCorreo",
                table: "Usuarios",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturacionDireccion",
                table: "Usuarios",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturacionDocumento",
                table: "Usuarios",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturacionMunicipioCodigo",
                table: "Usuarios",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturacionNombre",
                table: "Usuarios",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturacionTipoDocumento",
                table: "Usuarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipioCodigo",
                table: "Usuarios",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: false,
                defaultValue: "11001"); // hasta ahora la plataforma solo operaba en Bogotá

            migrationBuilder.AddColumn<string>(
                name: "Nit",
                table: "Usuarios",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreComercial",
                table: "Usuarios",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordatorioVencimientoPara",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Condicion",
                table: "Publicaciones",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Usado");

            migrationBuilder.AddColumn<string>(
                name: "DepartamentoCodigo",
                table: "Publicaciones",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: false,
                defaultValue: "11");

            migrationBuilder.AddColumn<string>(
                name: "DetalleCondicion",
                table: "Publicaciones",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaImpulso",
                table: "Publicaciones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRelevancia",
                table: "Publicaciones",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "MunicipioCodigo",
                table: "Publicaciones",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: false,
                defaultValue: "11001");

            migrationBuilder.AddColumn<bool>(
                name: "CuentaEnPromedio",
                table: "Calificaciones",
                type: "bit",
                nullable: false,
                defaultValue: true); // las calificaciones existentes ya suman al promedio

            // Datos existentes: el correo ya es único (sirve como canónico inicial) y el orden de "recientes" parte de la fecha de publicación.
            migrationBuilder.Sql("UPDATE [Usuarios] SET [CorreoCanonico] = [Correo];");
            migrationBuilder.Sql("UPDATE [Publicaciones] SET [FechaRelevancia] = [FechaPublicacion];");

            migrationBuilder.CreateTable(
                name: "EstadisticasPublicaciones",
                columns: table => new
                {
                    PublicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Vistas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadisticasPublicaciones", x => new { x.PublicacionId, x.Fecha });
                    table.ForeignKey(
                        name: "FK_EstadisticasPublicaciones_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PagoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalCop = table.Column<int>(type: "int", nullable: false),
                    IvaPorcentaje = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BaseCop = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IvaCop = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompradorTipoDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompradorDocumento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CompradorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CompradorCorreo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CompradorDireccion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CompradorMunicipioCodigo = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumeroDian = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Cufe = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    FechaEmisionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiereNotaCredito = table.Column<bool>(type: "bit", nullable: false),
                    NotaInterna = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facturas_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pqrs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Radicado = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PagoReferencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaLimiteUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Respuesta = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaRespuestaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondidaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pqrs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pqrs_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CorreoCanonico",
                table: "Usuarios",
                column: "CorreoCanonico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TipoCuenta_FechaVencimientoSuscripcion",
                table: "Usuarios",
                columns: new[] { "TipoCuenta", "FechaVencimientoSuscripcion" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_DepartamentoCodigo_MunicipioCodigo",
                table: "Publicaciones",
                columns: new[] { "DepartamentoCodigo", "MunicipioCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_DestacadaHasta",
                table: "Publicaciones",
                column: "DestacadaHasta");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_FechaRelevancia",
                table: "Publicaciones",
                column: "FechaRelevancia");

            migrationBuilder.CreateIndex(
                name: "IX_Calificaciones_AutorId_CalificadoId_FechaUtc",
                table: "Calificaciones",
                columns: new[] { "AutorId", "CalificadoId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Estado_FechaUtc",
                table: "Facturas",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_PagoId",
                table: "Facturas",
                column: "PagoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_UsuarioId_FechaUtc",
                table: "Facturas",
                columns: new[] { "UsuarioId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pqrs_Estado_FechaLimiteUtc",
                table: "Pqrs",
                columns: new[] { "Estado", "FechaLimiteUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pqrs_Radicado",
                table: "Pqrs",
                column: "Radicado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pqrs_UsuarioId_FechaUtc",
                table: "Pqrs",
                columns: new[] { "UsuarioId", "FechaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EstadisticasPublicaciones");

            migrationBuilder.DropTable(
                name: "Facturas");

            migrationBuilder.DropTable(
                name: "Pqrs");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CorreoCanonico",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TipoCuenta_FechaVencimientoSuscripcion",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_DepartamentoCodigo_MunicipioCodigo",
                table: "Publicaciones");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_DestacadaHasta",
                table: "Publicaciones");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_FechaRelevancia",
                table: "Publicaciones");

            migrationBuilder.DropIndex(
                name: "IX_Calificaciones_AutorId_CalificadoId_FechaUtc",
                table: "Calificaciones");

            migrationBuilder.DropColumn(
                name: "BonoBienvenidaOtorgado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CorreoCanonico",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FacturacionCorreo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FacturacionDireccion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FacturacionDocumento",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FacturacionMunicipioCodigo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FacturacionNombre",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FacturacionTipoDocumento",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MunicipioCodigo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Nit",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NombreComercial",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "RecordatorioVencimientoPara",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Condicion",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "DepartamentoCodigo",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "DetalleCondicion",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "FechaImpulso",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "FechaRelevancia",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "MunicipioCodigo",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "CuentaEnPromedio",
                table: "Calificaciones");

            migrationBuilder.CreateIndex(
                name: "IX_Calificaciones_AutorId",
                table: "Calificaciones",
                column: "AutorId");
        }
    }
}
