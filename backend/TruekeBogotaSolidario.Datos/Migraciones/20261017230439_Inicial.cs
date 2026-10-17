using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Accion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ObjetivoTipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ObjetivoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Detalle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    NombreCategoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Localidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false),
                    Correo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CorreoCanonico = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ClaveHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BonoBienvenidaOtorgado = table.Column<bool>(type: "boolean", nullable: false),
                    NombreComercial = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Nit = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: true),
                    MostrarNit = table.Column<bool>(type: "boolean", nullable: false),
                    FacturacionTipoDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FacturacionDocumento = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: true),
                    FacturacionNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FacturacionCorreo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FacturacionDireccion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FacturacionMunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true),
                    RecordatorioVencimientoPara = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VersionSeguridad = table.Column<int>(type: "integer", nullable: false),
                    IntentosFallidosLogin = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SaldoEcoPuntos = table.Column<int>(type: "integer", nullable: false),
                    Reputacion = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    TotalTruekesCompletados = table.Column<int>(type: "integer", nullable: false),
                    TotalComprasRealizadas = table.Column<int>(type: "integer", nullable: false),
                    TotalDonacionesRealizadas = table.Column<int>(type: "integer", nullable: false),
                    Rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoCuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaVencimientoSuscripcion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DestacadosGratisRestantes = table.Column<int>(type: "integer", nullable: false),
                    ProximaRecargaDestacadosUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PruebaPremiumUsadaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnPruebaPremium = table.Column<bool>(type: "boolean", nullable: false),
                    CodigoInvitacion = table.Column<string>(type: "character varying(12)", unicode: false, maxLength: 12, nullable: true),
                    InvitadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecompensaInvitacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstadoVerificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DocumentoVerificacionUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AvisosCorreoIntercambios = table.Column<bool>(type: "boolean", nullable: false),
                    AvisosCorreoMensajes = table.Column<bool>(type: "boolean", nullable: false),
                    AvisosCorreoPlanes = table.Column<bool>(type: "boolean", nullable: false),
                    AceptaNovedades = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoRechazoVerificacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CorreoVerificado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaVerificacionCorreo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GoogleSub = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    PoliticaDatosVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FechaAceptacionPolitica = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstaEliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstaSuspendido = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendidoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoSuspension = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CalificacionesTotal = table.Column<int>(type: "integer", nullable: false),
                    CalificacionesSuma = table.Column<int>(type: "integer", nullable: false),
                    DosFactoresActivo = table.Column<bool>(type: "boolean", nullable: false),
                    SecretoDosFactoresCifrado = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: true),
                    UltimoPasoDosFactores = table.Column<long>(type: "bigint", nullable: false),
                    CodigosRecuperacionHash = table.Column<string>(type: "character varying(700)", unicode: false, maxLength: 700, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Apelaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObjetivoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccionOriginal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ModeradorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotaResolucion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apelaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apelaciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bloqueos",
                columns: table => new
                {
                    BloqueadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BloqueadoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bloqueos", x => new { x.BloqueadorId, x.BloqueadoId });
                    table.ForeignKey(
                        name: "FK_Bloqueos_Usuarios_BloqueadoId",
                        column: x => x.BloqueadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bloqueos_Usuarios_BloqueadorId",
                        column: x => x.BloqueadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusquedasGuardadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CategoriaId = table.Column<int>(type: "integer", nullable: true),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Condicion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DepartamentoCodigo = table.Column<string>(type: "character varying(2)", unicode: false, maxLength: 2, nullable: true),
                    MunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true),
                    Localidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PrecioMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PrecioMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SoloVerificados = table.Column<bool>(type: "boolean", nullable: false),
                    AlertasActivas = table.Column<bool>(type: "boolean", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisadaHastaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Denuncias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DenuncianteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObjetivoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DenunciadoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModeradorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotaResolucion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Accion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResolucionId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "Deseos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CategoriaId = table.Column<int>(type: "integer", nullable: true),
                    Publico = table.Column<bool>(type: "boolean", nullable: false),
                    AvisosActivos = table.Column<bool>(type: "boolean", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisadoHastaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstaOculto = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "MovimientosEcoPuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Puntos = table.Column<int>(type: "integer", nullable: false),
                    Concepto = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detalle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SaldoResultante = table.Column<int>(type: "integer", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosEcoPuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosEcoPuntos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeidaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Concepto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MontoCop = table.Column<int>(type: "integer", nullable: false),
                    Referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PuntosCanjeados = table.Column<int>(type: "integer", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProveedorTransaccionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FechaResolucionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotaInterna = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    FacturaANombre = table.Column<bool>(type: "boolean", nullable: true),
                    FechaEleccionFacturaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompradorTipoDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompradorDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompradorNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CompradorCorreo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CompradorDireccion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CompradorMunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true)
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
                name: "Pqrs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Radicado = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Asunto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PagoReferencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaLimiteUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Respuesta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaRespuestaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondidaPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Publicaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropietarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Condicion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DetalleCondicion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PrecioReferenciaCop = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false),
                    DepartamentoCodigo = table.Column<string>(type: "character varying(2)", unicode: false, maxLength: 2, nullable: false),
                    Localidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaEdicion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DestacadaHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaImpulso = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaRelevancia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstaOculta = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TituloBusqueda = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TextoBusqueda = table.Column<string>(type: "character varying(2600)", maxLength: 2600, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraFamiliaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConDosFactores = table.Column<bool>(type: "boolean", nullable: false),
                    ReemplazadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "SuscripcionesPush",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(1000)", unicode: false, maxLength: 1000, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesPush", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuscripcionesPush_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokensUsoUnico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Proposito = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: false),
                    OferenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceptorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PuntosOtorgadosOferente = table.Column<bool>(type: "boolean", nullable: false),
                    PuntosOtorgadosReceptor = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElegidaANombre = table.Column<bool>(type: "boolean", nullable: true),
                    FechaEleccionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReemplazaAId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReemplazadaPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorregidaPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCorreccionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoCorreccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PagoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Concepto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalCop = table.Column<int>(type: "integer", nullable: false),
                    IvaPorcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    BaseCop = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IvaCop = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompradorTipoDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompradorDocumento = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    CompradorNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CompradorCorreo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CompradorDireccion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CompradorMunicipioCodigo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroDian = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Cufe = table.Column<string>(type: "character varying(120)", unicode: false, maxLength: 120, nullable: true),
                    FechaEmisionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiereNotaCredito = table.Column<bool>(type: "boolean", nullable: false),
                    NotaInterna = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "Comentarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstaOculto = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "EstadisticasPublicaciones",
                columns: table => new
                {
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Vistas = table.Column<int>(type: "integer", nullable: false)
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
                name: "Favoritos",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favoritos", x => new { x.UsuarioId, x.PublicacionId });
                    table.ForeignKey(
                        name: "FK_Favoritos_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Favoritos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicacionImagenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicacionImagenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicacionImagenes_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Solicitudes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionOfrecidaId = table.Column<Guid>(type: "uuid", nullable: true),
                    SolicitanteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FechaAceptacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmadaPorDuenioUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmadaPorSolicitanteUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCierreUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_Solicitudes_Publicaciones_PublicacionOfrecidaId",
                        column: x => x.PublicacionOfrecidaId,
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
                name: "Calificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalificadoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estrellas = table.Column<int>(type: "integer", nullable: false),
                    Comentario = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CuentaEnPromedio = table.Column<bool>(type: "boolean", nullable: false),
                    ComentarioOculto = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calificaciones_Solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "Solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calificaciones_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calificaciones_Usuarios_CalificadoId",
                        column: x => x.CalificadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DuenioId = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoMensajeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RespuestaAId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntregadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeidoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstaOculto = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoOcultamiento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                        name: "FK_Mensajes_Mensajes_RespuestaAId",
                        column: x => x.RespuestaAId,
                        principalTable: "Mensajes",
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
                name: "IX_Apelaciones_Estado_FechaUtc",
                table: "Apelaciones",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_ResolucionId",
                table: "Apelaciones",
                column: "ResolucionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_UsuarioId",
                table: "Apelaciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_ActorId",
                table: "Auditoria",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaUtc",
                table: "Auditoria",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueos_BloqueadoId",
                table: "Bloqueos",
                column: "BloqueadoId");

            migrationBuilder.CreateIndex(
                name: "IX_BusquedasGuardadas_AlertasActivas_RevisadaHastaUtc",
                table: "BusquedasGuardadas",
                columns: new[] { "AlertasActivas", "RevisadaHastaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusquedasGuardadas_UsuarioId",
                table: "BusquedasGuardadas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Calificaciones_AutorId_CalificadoId_FechaUtc",
                table: "Calificaciones",
                columns: new[] { "AutorId", "CalificadoId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Calificaciones_CalificadoId_FechaUtc",
                table: "Calificaciones",
                columns: new[] { "CalificadoId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Calificaciones_SolicitudId_AutorId",
                table: "Calificaciones",
                columns: new[] { "SolicitudId", "AutorId" },
                unique: true);

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
                name: "IX_Denuncias_DenunciadoId_Estado",
                table: "Denuncias",
                columns: new[] { "DenunciadoId", "Estado" });

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
                name: "IX_Denuncias_ResolucionId",
                table: "Denuncias",
                column: "ResolucionId");

            migrationBuilder.CreateIndex(
                name: "IX_Denuncias_Tipo_ObjetivoId_Estado",
                table: "Denuncias",
                columns: new[] { "Tipo", "ObjetivoId", "Estado" });

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

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Estado_FechaUtc",
                table: "Facturas",
                columns: new[] { "Estado", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_PagoId",
                table: "Facturas",
                column: "PagoId",
                unique: true,
                filter: "\"Estado\" IN ('Pendiente', 'Emitida')");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_UsuarioId_FechaUtc",
                table: "Facturas",
                columns: new[] { "UsuarioId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Favoritos_PublicacionId",
                table: "Favoritos",
                column: "PublicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Favoritos_UsuarioId_FechaUtc",
                table: "Favoritos",
                columns: new[] { "UsuarioId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_AutorId_FechaUtc",
                table: "Mensajes",
                columns: new[] { "AutorId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_AutorId_EntregadoUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "AutorId", "EntregadoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_AutorId_LeidoUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "AutorId", "LeidoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_FechaUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_RespuestaAId",
                table: "Mensajes",
                column: "RespuestaAId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosEcoPuntos_UsuarioId_FechaUtc",
                table: "MovimientosEcoPuntos",
                columns: new[] { "UsuarioId", "FechaUtc" });

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
                filter: "\"ProveedorTransaccionId\" IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_CategoriaId",
                table: "Publicaciones",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_DepartamentoCodigo_MunicipioCodigo",
                table: "Publicaciones",
                columns: new[] { "DepartamentoCodigo", "MunicipioCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_DestacadaHasta",
                table: "Publicaciones",
                column: "DestacadaHasta");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Estado_EstaOculta",
                table: "Publicaciones",
                columns: new[] { "Estado", "EstaOculta" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_FechaRelevancia",
                table: "Publicaciones",
                column: "FechaRelevancia");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Latitud_Longitud",
                table: "Publicaciones",
                columns: new[] { "Latitud", "Longitud" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_PropietarioId",
                table: "Publicaciones",
                column: "PropietarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicacionImagenes_PublicacionId",
                table: "PublicacionImagenes",
                column: "PublicacionId");

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
                name: "IX_Solicitudes_PublicacionOfrecidaId_Estado",
                table: "Solicitudes",
                columns: new[] { "PublicacionOfrecidaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_SolicitanteId_Estado",
                table: "Solicitudes",
                columns: new[] { "SolicitanteId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPush_Endpoint",
                table: "SuscripcionesPush",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPush_UsuarioId",
                table: "SuscripcionesPush",
                column: "UsuarioId");

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
                name: "IX_Usuarios_CodigoInvitacion",
                table: "Usuarios",
                column: "CodigoInvitacion",
                unique: true,
                filter: "\"CodigoInvitacion\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CorreoCanonico",
                table: "Usuarios",
                column: "CorreoCanonico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EstadoVerificacion",
                table: "Usuarios",
                column: "EstadoVerificacion");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EstaSuspendido",
                table: "Usuarios",
                column: "EstaSuspendido");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_GoogleSub",
                table: "Usuarios",
                column: "GoogleSub",
                unique: true,
                filter: "\"GoogleSub\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_InvitadoPorId_RecompensaInvitacionUtc",
                table: "Usuarios",
                columns: new[] { "InvitadoPorId", "RecompensaInvitacionUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TipoCuenta_FechaVencimientoSuscripcion",
                table: "Usuarios",
                columns: new[] { "TipoCuenta", "FechaVencimientoSuscripcion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apelaciones");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Bloqueos");

            migrationBuilder.DropTable(
                name: "BusquedasGuardadas");

            migrationBuilder.DropTable(
                name: "Calificaciones");

            migrationBuilder.DropTable(
                name: "Comentarios");

            migrationBuilder.DropTable(
                name: "Denuncias");

            migrationBuilder.DropTable(
                name: "Deseos");

            migrationBuilder.DropTable(
                name: "EstadisticasPublicaciones");

            migrationBuilder.DropTable(
                name: "Facturas");

            migrationBuilder.DropTable(
                name: "Favoritos");

            migrationBuilder.DropTable(
                name: "Mensajes");

            migrationBuilder.DropTable(
                name: "MovimientosEcoPuntos");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "Pqrs");

            migrationBuilder.DropTable(
                name: "PublicacionImagenes");

            migrationBuilder.DropTable(
                name: "SesionesRefresh");

            migrationBuilder.DropTable(
                name: "SuscripcionesPush");

            migrationBuilder.DropTable(
                name: "TokensUsoUnico");

            migrationBuilder.DropTable(
                name: "Transacciones");

            migrationBuilder.DropTable(
                name: "Pagos");

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
