using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TruekeBogotaSolidario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ObjetivoTipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ObjetivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NombreCategoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Localidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ClaveHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VersionSeguridad = table.Column<int>(type: "int", nullable: false),
                    IntentosFallidosLogin = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaldoEcoPuntos = table.Column<int>(type: "int", nullable: false),
                    Reputacion = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    TotalTruekesCompletados = table.Column<int>(type: "int", nullable: false),
                    TotalComprasRealizadas = table.Column<int>(type: "int", nullable: false),
                    TotalDonacionesRealizadas = table.Column<int>(type: "int", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TipoCuenta = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaVencimientoSuscripcion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DestacadosGratisRestantes = table.Column<int>(type: "int", nullable: false),
                    EstadoVerificacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocumentoVerificacionUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MotivoRechazoVerificacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorreoVerificado = table.Column<bool>(type: "bit", nullable: false),
                    FechaVerificacionCorreo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GoogleSub = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    PoliticaDatosVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FechaAceptacionPolitica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstaEliminado = table.Column<bool>(type: "bit", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Denuncias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DenuncianteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ObjetivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ModeradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaResolucion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Denuncias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Denuncias_Usuarios_DenuncianteId",
                        column: x => x.DenuncianteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeidaUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MontoCop = table.Column<int>(type: "int", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PuntosCanjeados = table.Column<int>(type: "int", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProveedorTransaccionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaInterna = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Publicaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropietarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Modo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PrecioReferenciaCop = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Localidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Latitud = table.Column<double>(type: "float", nullable: true),
                    Longitud = table.Column<double>(type: "float", nullable: true),
                    ImagenUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DestacadaHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstaOculta = table.Column<bool>(type: "bit", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publicaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publicaciones_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Publicaciones_Usuarios_PropietarioId",
                        column: x => x.PropietarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SesionesRefresh",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraFamiliaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReemplazadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesRefresh", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesRefresh_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokensUsoUnico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Proposito = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensUsoUnico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensUsoUnico_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transacciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OferenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceptorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PuntosOtorgadosOferente = table.Column<bool>(type: "bit", nullable: false),
                    PuntosOtorgadosReceptor = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transacciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transacciones_Usuarios_OferenteId",
                        column: x => x.OferenteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transacciones_Usuarios_ReceptorId",
                        column: x => x.ReceptorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Comentarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstaOculto = table.Column<bool>(type: "bit", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comentarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comentarios_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Comentarios_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Solicitudes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitudes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Solicitudes_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Solicitudes_Usuarios_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuenioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoMensajeUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversaciones_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversaciones_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversaciones_Usuarios_DuenioId",
                        column: x => x.DuenioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversaciones_Usuarios_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mensajes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeidoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstaOculto = table.Column<bool>(type: "bit", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensajes_Conversaciones_ConversacionId",
                        column: x => x.ConversacionId,
                        principalTable: "Conversaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensajes_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "Descripcion", "NombreCategoria" },
                values: new object[,]
                {
                    { 1, "Prendas, zapatos y accesorios", "Ropa y calzado" },
                    { 2, "Libros, textos escolares y útiles", "Libros y papelería" },
                    { 3, "Aparatos para el hogar", "Electrodomésticos" },
                    { 4, "Decoración, menaje y muebles", "Hogar y muebles" },
                    { 5, "Computadores, celulares y accesorios", "Tecnología" },
                    { 6, "Juguetes y artículos infantiles", "Juguetes y niños" },
                    { 7, "Todo lo demás", "Otros" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_ActorId",
                table: "Auditoria",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaUtc",
                table: "Auditoria",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Comentarios_AutorId_FechaUtc",
                table: "Comentarios",
                columns: new[] { "AutorId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comentarios_PublicacionId_EstaOculto_FechaUtc",
                table: "Comentarios",
                columns: new[] { "PublicacionId", "EstaOculto", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_DuenioId_UltimoMensajeUtc",
                table: "Conversaciones",
                columns: new[] { "DuenioId", "UltimoMensajeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_PublicacionId",
                table: "Conversaciones",
                column: "PublicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_SolicitanteId_UltimoMensajeUtc",
                table: "Conversaciones",
                columns: new[] { "SolicitanteId", "UltimoMensajeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_SolicitudId",
                table: "Conversaciones",
                column: "SolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_DenuncianteId_Tipo_ObjetivoId",
                table: "Denuncias",
                columns: new[] { "DenuncianteId", "Tipo", "ObjetivoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_Estado_FechaUtc",
                table: "Denuncias",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_Tipo_ObjetivoId_Estado",
                table: "Denuncias",
                columns: new[] { "Tipo", "ObjetivoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_AutorId_FechaUtc",
                table: "Mensajes",
                columns: new[] { "AutorId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_AutorId_LeidoUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "AutorId", "LeidoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_FechaUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_LeidaUtc_FechaUtc",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "LeidaUtc", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_Estado_FechaUtc",
                table: "Pagos",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ProveedorTransaccionId",
                table: "Pagos",
                column: "ProveedorTransaccionId",
                unique: true,
                filter: "[ProveedorTransaccionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_Referencia",
                table: "Pagos",
                column: "Referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_UsuarioId_Concepto_Estado",
                table: "Pagos",
                columns: new[] { "UsuarioId", "Concepto", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_CategoriaId",
                table: "Publicaciones",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Estado_EstaOculta",
                table: "Publicaciones",
                columns: new[] { "Estado", "EstaOculta" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Latitud_Longitud",
                table: "Publicaciones",
                columns: new[] { "Latitud", "Longitud" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_PropietarioId",
                table: "Publicaciones",
                column: "PropietarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesRefresh_ExpiraUtc",
                table: "SesionesRefresh",
                column: "ExpiraUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesRefresh_FamiliaId",
                table: "SesionesRefresh",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesRefresh_TokenHash",
                table: "SesionesRefresh",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesRefresh_UsuarioId_RevocadoUtc",
                table: "SesionesRefresh",
                columns: new[] { "UsuarioId", "RevocadoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_PublicacionId_Estado",
                table: "Solicitudes",
                columns: new[] { "PublicacionId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_SolicitanteId_Estado",
                table: "Solicitudes",
                columns: new[] { "SolicitanteId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_TokensUsoUnico_ExpiraUtc",
                table: "TokensUsoUnico",
                column: "ExpiraUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TokensUsoUnico_TokenHash",
                table: "TokensUsoUnico",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensUsoUnico_UsuarioId_Proposito_CreadoUtc",
                table: "TokensUsoUnico",
                columns: new[] { "UsuarioId", "Proposito", "CreadoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_OferenteId_FechaUtc",
                table: "Transacciones",
                columns: new[] { "OferenteId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_ReceptorId_FechaUtc",
                table: "Transacciones",
                columns: new[] { "ReceptorId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EstadoVerificacion",
                table: "Usuarios",
                column: "EstadoVerificacion");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_GoogleSub",
                table: "Usuarios",
                column: "GoogleSub",
                unique: true,
                filter: "[GoogleSub] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Comentarios");

            migrationBuilder.DropTable(
                name: "Denuncias");

            migrationBuilder.DropTable(
                name: "Mensajes");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "SesionesRefresh");

            migrationBuilder.DropTable(
                name: "TokensUsoUnico");

            migrationBuilder.DropTable(
                name: "Transacciones");

            migrationBuilder.DropTable(
                name: "Conversaciones");

            migrationBuilder.DropTable(
                name: "Solicitudes");

            migrationBuilder.DropTable(
                name: "Publicaciones");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
