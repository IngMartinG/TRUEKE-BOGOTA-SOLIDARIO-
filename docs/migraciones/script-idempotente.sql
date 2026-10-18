CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Auditoria" (
        "Id" uuid NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "ActorId" uuid NOT NULL,
        "Accion" character varying(60) NOT NULL,
        "ObjetivoTipo" character varying(40) NOT NULL,
        "ObjetivoId" uuid NOT NULL,
        "Detalle" character varying(300),
        CONSTRAINT "PK_Auditoria" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Categorias" (
        "Id" integer NOT NULL,
        "NombreCategoria" character varying(60) NOT NULL,
        "Descripcion" character varying(200) NOT NULL,
        CONSTRAINT "PK_Categorias" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Usuarios" (
        "Id" uuid NOT NULL,
        "NombreCompleto" character varying(120) NOT NULL,
        "Localidad" character varying(60) NOT NULL,
        "MunicipioCodigo" character varying(5) NOT NULL,
        "Correo" character varying(160) NOT NULL,
        "CorreoCanonico" character varying(160) NOT NULL,
        "ClaveHash" character varying(256) NOT NULL,
        "FechaRegistro" timestamp with time zone NOT NULL,
        "BonoBienvenidaOtorgado" boolean NOT NULL,
        "NombreComercial" character varying(120),
        "Nit" character varying(20),
        "MostrarNit" boolean NOT NULL,
        "FacturacionTipoDocumento" character varying(20),
        "FacturacionDocumento" character varying(20),
        "FacturacionNombre" character varying(150),
        "FacturacionCorreo" character varying(160),
        "FacturacionDireccion" character varying(150),
        "FacturacionMunicipioCodigo" character varying(5),
        "RecordatorioVencimientoPara" timestamp with time zone,
        "VersionSeguridad" integer NOT NULL,
        "IntentosFallidosLogin" integer NOT NULL,
        "BloqueadoHasta" timestamp with time zone,
        "SaldoEcoPuntos" integer NOT NULL,
        "Reputacion" numeric(3,2) NOT NULL,
        "TotalTruekesCompletados" integer NOT NULL,
        "TotalComprasRealizadas" integer NOT NULL,
        "TotalDonacionesRealizadas" integer NOT NULL,
        "Rol" character varying(20) NOT NULL,
        "TipoCuenta" character varying(20) NOT NULL,
        "FechaVencimientoSuscripcion" timestamp with time zone,
        "DestacadosGratisRestantes" integer NOT NULL,
        "ProximaRecargaDestacadosUtc" timestamp with time zone,
        "PruebaPremiumUsadaUtc" timestamp with time zone,
        "EnPruebaPremium" boolean NOT NULL,
        "CodigoInvitacion" character varying(12),
        "InvitadoPorId" uuid,
        "RecompensaInvitacionUtc" timestamp with time zone,
        "EstadoVerificacion" character varying(20) NOT NULL,
        "DocumentoVerificacionUrl" character varying(500),
        "FotoUrl" character varying(500),
        "AvisosCorreoIntercambios" boolean NOT NULL,
        "AvisosCorreoMensajes" boolean NOT NULL,
        "AvisosCorreoPlanes" boolean NOT NULL,
        "AceptaNovedades" boolean NOT NULL,
        "MotivoRechazoVerificacion" character varying(300),
        "CorreoVerificado" boolean NOT NULL,
        "FechaVerificacionCorreo" timestamp with time zone,
        "GoogleSub" character varying(64),
        "PoliticaDatosVersion" character varying(20),
        "FechaAceptacionPolitica" timestamp with time zone,
        "EstaEliminado" boolean NOT NULL,
        "FechaEliminacion" timestamp with time zone,
        "EstaSuspendido" boolean NOT NULL,
        "SuspendidoHasta" timestamp with time zone,
        "MotivoSuspension" character varying(300),
        "CalificacionesTotal" integer NOT NULL,
        "CalificacionesSuma" integer NOT NULL,
        "DosFactoresActivo" boolean NOT NULL,
        "SecretoDosFactoresCifrado" character varying(200),
        "UltimoPasoDosFactores" bigint NOT NULL,
        "CodigosRecuperacionHash" character varying(700),
        CONSTRAINT "PK_Usuarios" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Apelaciones" (
        "Id" uuid NOT NULL,
        "ResolucionId" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Tipo" character varying(20) NOT NULL,
        "ObjetivoId" uuid NOT NULL,
        "AccionOriginal" character varying(20) NOT NULL,
        "Texto" character varying(1000) NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "Estado" character varying(20) NOT NULL,
        "ModeradorId" uuid,
        "FechaResolucionUtc" timestamp with time zone,
        "NotaResolucion" character varying(300),
        CONSTRAINT "PK_Apelaciones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Apelaciones_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Bloqueos" (
        "BloqueadorId" uuid NOT NULL,
        "BloqueadoId" uuid NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Bloqueos" PRIMARY KEY ("BloqueadorId", "BloqueadoId"),
        CONSTRAINT "FK_Bloqueos_Usuarios_BloqueadoId" FOREIGN KEY ("BloqueadoId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Bloqueos_Usuarios_BloqueadorId" FOREIGN KEY ("BloqueadorId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "BusquedasGuardadas" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Texto" character varying(100),
        "CategoriaId" integer,
        "Modo" character varying(20),
        "Condicion" character varying(20),
        "DepartamentoCodigo" character varying(2),
        "MunicipioCodigo" character varying(5),
        "Localidad" character varying(60),
        "PrecioMin" numeric(18,2),
        "PrecioMax" numeric(18,2),
        "SoloVerificados" boolean NOT NULL,
        "AlertasActivas" boolean NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "RevisadaHastaUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BusquedasGuardadas" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BusquedasGuardadas_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Denuncias" (
        "Id" uuid NOT NULL,
        "DenuncianteId" uuid NOT NULL,
        "Tipo" character varying(20) NOT NULL,
        "ObjetivoId" uuid NOT NULL,
        "Motivo" character varying(30) NOT NULL,
        "Detalle" character varying(500),
        "FechaUtc" timestamp with time zone NOT NULL,
        "Estado" character varying(20) NOT NULL,
        "DenunciadoId" uuid,
        "ModeradorId" uuid,
        "FechaResolucionUtc" timestamp with time zone,
        "NotaResolucion" character varying(300),
        "Accion" character varying(20),
        "ResolucionId" uuid,
        CONSTRAINT "PK_Denuncias" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Denuncias_Usuarios_DenuncianteId" FOREIGN KEY ("DenuncianteId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Deseos" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Texto" character varying(60) NOT NULL,
        "CategoriaId" integer,
        "Publico" boolean NOT NULL,
        "AvisosActivos" boolean NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "RevisadoHastaUtc" timestamp with time zone NOT NULL,
        "EstaOculto" boolean NOT NULL,
        "MotivoOcultamiento" character varying(300),
        CONSTRAINT "PK_Deseos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Deseos_Categorias_CategoriaId" FOREIGN KEY ("CategoriaId") REFERENCES "Categorias" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Deseos_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "MovimientosEcoPuntos" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Puntos" integer NOT NULL,
        "Concepto" character varying(30) NOT NULL,
        "ReferenciaId" uuid,
        "Detalle" character varying(160),
        "SaldoResultante" integer NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MovimientosEcoPuntos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MovimientosEcoPuntos_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Notificaciones" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Tipo" character varying(40) NOT NULL,
        "Mensaje" character varying(300) NOT NULL,
        "RecursoId" uuid,
        "FechaUtc" timestamp with time zone NOT NULL,
        "LeidaUtc" timestamp with time zone,
        CONSTRAINT "PK_Notificaciones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Notificaciones_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Pagos" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Concepto" character varying(20) NOT NULL,
        "MontoCop" integer NOT NULL,
        "Referencia" character varying(100) NOT NULL,
        "PuntosCanjeados" integer NOT NULL,
        "PublicacionId" uuid,
        "DocumentoUrl" character varying(500),
        "FechaUtc" timestamp with time zone NOT NULL,
        "Estado" character varying(20) NOT NULL,
        "ProveedorTransaccionId" character varying(100),
        "FechaResolucionUtc" timestamp with time zone,
        "NotaInterna" character varying(300),
        "FacturaANombre" boolean,
        "FechaEleccionFacturaUtc" timestamp with time zone,
        "CompradorTipoDocumento" character varying(20),
        "CompradorDocumento" character varying(20),
        "CompradorNombre" character varying(150),
        "CompradorCorreo" character varying(160),
        "CompradorDireccion" character varying(150),
        "CompradorMunicipioCodigo" character varying(5),
        CONSTRAINT "PK_Pagos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Pagos_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Pqrs" (
        "Id" uuid NOT NULL,
        "Radicado" character varying(30) NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Tipo" character varying(20) NOT NULL,
        "Asunto" character varying(120) NOT NULL,
        "Descripcion" character varying(2000) NOT NULL,
        "PagoReferencia" character varying(100),
        "Estado" character varying(20) NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "FechaLimiteUtc" timestamp with time zone NOT NULL,
        "Respuesta" character varying(2000),
        "FechaRespuestaUtc" timestamp with time zone,
        "RespondidaPorId" uuid,
        CONSTRAINT "PK_Pqrs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Pqrs_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Publicaciones" (
        "Id" uuid NOT NULL,
        "PropietarioId" uuid NOT NULL,
        "Titulo" character varying(120) NOT NULL,
        "Descripcion" character varying(2000) NOT NULL,
        "CategoriaId" integer NOT NULL,
        "Modo" character varying(20) NOT NULL,
        "Condicion" character varying(20) NOT NULL,
        "DetalleCondicion" character varying(300),
        "PrecioReferenciaCop" numeric(18,2),
        "MunicipioCodigo" character varying(5) NOT NULL,
        "DepartamentoCodigo" character varying(2) NOT NULL,
        "Localidad" character varying(60) NOT NULL,
        "Latitud" double precision,
        "Longitud" double precision,
        "Estado" character varying(20) NOT NULL,
        "FechaPublicacion" timestamp with time zone NOT NULL,
        "FechaEdicion" timestamp with time zone,
        "MotivoCancelacion" character varying(300),
        "DestacadaHasta" timestamp with time zone,
        "FechaImpulso" timestamp with time zone,
        "FechaRelevancia" timestamp with time zone NOT NULL,
        "EstaOculta" boolean NOT NULL,
        "MotivoOcultamiento" character varying(300),
        "TituloBusqueda" character varying(200) NOT NULL,
        "TextoBusqueda" character varying(2600) NOT NULL,
        CONSTRAINT "PK_Publicaciones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Publicaciones_Categorias_CategoriaId" FOREIGN KEY ("CategoriaId") REFERENCES "Categorias" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Publicaciones_Usuarios_PropietarioId" FOREIGN KEY ("PropietarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "SesionesRefresh" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "FamiliaId" uuid NOT NULL,
        "TokenHash" character varying(64) NOT NULL,
        "CreadoUtc" timestamp with time zone NOT NULL,
        "ExpiraUtc" timestamp with time zone NOT NULL,
        "ExpiraFamiliaUtc" timestamp with time zone NOT NULL,
        "ConDosFactores" boolean NOT NULL,
        "ReemplazadoPorId" uuid,
        "RevocadoUtc" timestamp with time zone,
        CONSTRAINT "PK_SesionesRefresh" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SesionesRefresh_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "SuscripcionesPush" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Endpoint" character varying(1000) NOT NULL,
        "P256dh" character varying(200) NOT NULL,
        "Auth" character varying(100) NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SuscripcionesPush" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SuscripcionesPush_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "TokensUsoUnico" (
        "Id" uuid NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Proposito" character varying(20) NOT NULL,
        "TokenHash" character varying(64) NOT NULL,
        "CreadoUtc" timestamp with time zone NOT NULL,
        "ExpiraUtc" timestamp with time zone NOT NULL,
        "UsadoUtc" timestamp with time zone,
        CONSTRAINT "PK_TokensUsoUnico" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TokensUsoUnico_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Transacciones" (
        "Id" uuid NOT NULL,
        "PublicacionId" uuid NOT NULL,
        "SolicitudId" uuid NOT NULL,
        "OferenteId" uuid NOT NULL,
        "ReceptorId" uuid NOT NULL,
        "Modo" character varying(20) NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "PuntosOtorgadosOferente" boolean NOT NULL,
        "PuntosOtorgadosReceptor" boolean NOT NULL,
        CONSTRAINT "PK_Transacciones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Transacciones_Usuarios_OferenteId" FOREIGN KEY ("OferenteId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Transacciones_Usuarios_ReceptorId" FOREIGN KEY ("ReceptorId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Facturas" (
        "Id" uuid NOT NULL,
        "ElegidaANombre" boolean,
        "FechaEleccionUtc" timestamp with time zone,
        "ReemplazaAId" uuid,
        "ReemplazadaPorId" uuid,
        "CorregidaPorId" uuid,
        "FechaCorreccionUtc" timestamp with time zone,
        "MotivoCorreccion" character varying(300),
        "PagoId" uuid NOT NULL,
        "Referencia" character varying(100) NOT NULL,
        "UsuarioId" uuid NOT NULL,
        "Concepto" character varying(20) NOT NULL,
        "Descripcion" character varying(200) NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "TotalCop" integer NOT NULL,
        "IvaPorcentaje" numeric(5,2) NOT NULL,
        "BaseCop" numeric(18,2) NOT NULL,
        "IvaCop" numeric(18,2) NOT NULL,
        "CompradorTipoDocumento" character varying(20),
        "CompradorDocumento" character varying(20) NOT NULL,
        "CompradorNombre" character varying(150) NOT NULL,
        "CompradorCorreo" character varying(160) NOT NULL,
        "CompradorDireccion" character varying(150),
        "CompradorMunicipioCodigo" character varying(5),
        "Estado" character varying(20) NOT NULL,
        "NumeroDian" character varying(50),
        "Cufe" character varying(120),
        "FechaEmisionUtc" timestamp with time zone,
        "RequiereNotaCredito" boolean NOT NULL,
        "NotaInterna" character varying(300),
        CONSTRAINT "PK_Facturas" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Facturas_Pagos_PagoId" FOREIGN KEY ("PagoId") REFERENCES "Pagos" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Facturas_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Comentarios" (
        "Id" uuid NOT NULL,
        "PublicacionId" uuid NOT NULL,
        "AutorId" uuid NOT NULL,
        "Texto" character varying(500) NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        "EstaOculto" boolean NOT NULL,
        "MotivoOcultamiento" character varying(300),
        CONSTRAINT "PK_Comentarios" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Comentarios_Publicaciones_PublicacionId" FOREIGN KEY ("PublicacionId") REFERENCES "Publicaciones" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Comentarios_Usuarios_AutorId" FOREIGN KEY ("AutorId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "EstadisticasPublicaciones" (
        "PublicacionId" uuid NOT NULL,
        "Fecha" date NOT NULL,
        "Vistas" integer NOT NULL,
        CONSTRAINT "PK_EstadisticasPublicaciones" PRIMARY KEY ("PublicacionId", "Fecha"),
        CONSTRAINT "FK_EstadisticasPublicaciones_Publicaciones_PublicacionId" FOREIGN KEY ("PublicacionId") REFERENCES "Publicaciones" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Favoritos" (
        "UsuarioId" uuid NOT NULL,
        "PublicacionId" uuid NOT NULL,
        "FechaUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Favoritos" PRIMARY KEY ("UsuarioId", "PublicacionId"),
        CONSTRAINT "FK_Favoritos_Publicaciones_PublicacionId" FOREIGN KEY ("PublicacionId") REFERENCES "Publicaciones" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Favoritos_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "PublicacionImagenes" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Url" character varying(500) NOT NULL,
        "Orden" integer NOT NULL,
        "PublicacionId" uuid NOT NULL,
        CONSTRAINT "PK_PublicacionImagenes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PublicacionImagenes_Publicaciones_PublicacionId" FOREIGN KEY ("PublicacionId") REFERENCES "Publicaciones" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Solicitudes" (
        "Id" uuid NOT NULL,
        "PublicacionId" uuid NOT NULL,
        "PublicacionOfrecidaId" uuid,
        "SolicitanteId" uuid NOT NULL,
        "FechaSolicitud" timestamp with time zone NOT NULL,
        "Mensaje" character varying(500) NOT NULL,
        "Estado" character varying(20) NOT NULL,
        "MotivoRechazo" character varying(300),
        "FechaAceptacionUtc" timestamp with time zone,
        "ConfirmadaPorDuenioUtc" timestamp with time zone,
        "ConfirmadaPorSolicitanteUtc" timestamp with time zone,
        "FechaCierreUtc" timestamp with time zone,
        CONSTRAINT "PK_Solicitudes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Solicitudes_Publicaciones_PublicacionId" FOREIGN KEY ("PublicacionId") REFERENCES "Publicaciones" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Solicitudes_Publicaciones_PublicacionOfrecidaId" FOREIGN KEY ("PublicacionOfrecidaId") REFERENCES "Publicaciones" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Solicitudes_Usuarios_SolicitanteId" FOREIGN KEY ("SolicitanteId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Calificaciones" (
        "Id" uuid NOT NULL,
        "SolicitudId" uuid NOT NULL,
        "AutorId" uuid NOT NULL,
        "CalificadoId" uuid NOT NULL,
        "Estrellas" integer NOT NULL,
        "Comentario" character varying(300),
        "FechaUtc" timestamp with time zone NOT NULL,
        "CuentaEnPromedio" boolean NOT NULL,
        "ComentarioOculto" boolean NOT NULL,
        "MotivoOcultamiento" character varying(300),
        CONSTRAINT "PK_Calificaciones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Calificaciones_Solicitudes_SolicitudId" FOREIGN KEY ("SolicitudId") REFERENCES "Solicitudes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Calificaciones_Usuarios_AutorId" FOREIGN KEY ("AutorId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Calificaciones_Usuarios_CalificadoId" FOREIGN KEY ("CalificadoId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Conversaciones" (
        "Id" uuid NOT NULL,
        "SolicitudId" uuid NOT NULL,
        "PublicacionId" uuid NOT NULL,
        "DuenioId" uuid NOT NULL,
        "SolicitanteId" uuid NOT NULL,
        "CreadaUtc" timestamp with time zone NOT NULL,
        "UltimoMensajeUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Conversaciones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Conversaciones_Publicaciones_PublicacionId" FOREIGN KEY ("PublicacionId") REFERENCES "Publicaciones" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Conversaciones_Solicitudes_SolicitudId" FOREIGN KEY ("SolicitudId") REFERENCES "Solicitudes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Conversaciones_Usuarios_DuenioId" FOREIGN KEY ("DuenioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Conversaciones_Usuarios_SolicitanteId" FOREIGN KEY ("SolicitanteId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE TABLE "Mensajes" (
        "Id" uuid NOT NULL,
        "ConversacionId" uuid NOT NULL,
        "AutorId" uuid NOT NULL,
        "Texto" character varying(1000) NOT NULL,
        "RespuestaAId" uuid,
        "FechaUtc" timestamp with time zone NOT NULL,
        "EntregadoUtc" timestamp with time zone,
        "LeidoUtc" timestamp with time zone,
        "EstaOculto" boolean NOT NULL,
        "MotivoOcultamiento" character varying(300),
        CONSTRAINT "PK_Mensajes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Mensajes_Conversaciones_ConversacionId" FOREIGN KEY ("ConversacionId") REFERENCES "Conversaciones" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Mensajes_Mensajes_RespuestaAId" FOREIGN KEY ("RespuestaAId") REFERENCES "Mensajes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Mensajes_Usuarios_AutorId" FOREIGN KEY ("AutorId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (1, 'Prendas, zapatos y accesorios', 'Ropa y calzado');
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (2, 'Libros, textos escolares y útiles', 'Libros y papelería');
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (3, 'Aparatos para el hogar', 'Electrodomésticos');
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (4, 'Decoración, menaje y muebles', 'Hogar y muebles');
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (5, 'Computadores, celulares y accesorios', 'Tecnología');
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (6, 'Juguetes y artículos infantiles', 'Juguetes y niños');
    INSERT INTO "Categorias" ("Id", "Descripcion", "NombreCategoria")
    VALUES (7, 'Todo lo demás', 'Otros');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Apelaciones_Estado_FechaUtc" ON "Apelaciones" ("Estado", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Apelaciones_ResolucionId" ON "Apelaciones" ("ResolucionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Apelaciones_UsuarioId" ON "Apelaciones" ("UsuarioId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Auditoria_ActorId" ON "Auditoria" ("ActorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Auditoria_FechaUtc" ON "Auditoria" ("FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Bloqueos_BloqueadoId" ON "Bloqueos" ("BloqueadoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_BusquedasGuardadas_AlertasActivas_RevisadaHastaUtc" ON "BusquedasGuardadas" ("AlertasActivas", "RevisadaHastaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_BusquedasGuardadas_UsuarioId" ON "BusquedasGuardadas" ("UsuarioId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Calificaciones_AutorId_CalificadoId_FechaUtc" ON "Calificaciones" ("AutorId", "CalificadoId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Calificaciones_CalificadoId_FechaUtc" ON "Calificaciones" ("CalificadoId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Calificaciones_SolicitudId_AutorId" ON "Calificaciones" ("SolicitudId", "AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Comentarios_AutorId_FechaUtc" ON "Comentarios" ("AutorId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Comentarios_PublicacionId_EstaOculto_FechaUtc" ON "Comentarios" ("PublicacionId", "EstaOculto", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Conversaciones_DuenioId_UltimoMensajeUtc" ON "Conversaciones" ("DuenioId", "UltimoMensajeUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Conversaciones_PublicacionId" ON "Conversaciones" ("PublicacionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Conversaciones_SolicitanteId_UltimoMensajeUtc" ON "Conversaciones" ("SolicitanteId", "UltimoMensajeUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Conversaciones_SolicitudId" ON "Conversaciones" ("SolicitudId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Denuncias_DenunciadoId_Estado" ON "Denuncias" ("DenunciadoId", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Denuncias_DenuncianteId_Tipo_ObjetivoId" ON "Denuncias" ("DenuncianteId", "Tipo", "ObjetivoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Denuncias_Estado_FechaUtc" ON "Denuncias" ("Estado", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Denuncias_ResolucionId" ON "Denuncias" ("ResolucionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Denuncias_Tipo_ObjetivoId_Estado" ON "Denuncias" ("Tipo", "ObjetivoId", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Deseos_AvisosActivos_RevisadoHastaUtc" ON "Deseos" ("AvisosActivos", "RevisadoHastaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Deseos_CategoriaId" ON "Deseos" ("CategoriaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Deseos_UsuarioId" ON "Deseos" ("UsuarioId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Facturas_Estado_FechaUtc" ON "Facturas" ("Estado", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Facturas_PagoId" ON "Facturas" ("PagoId") WHERE "Estado" IN ('Pendiente', 'Emitida');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Facturas_UsuarioId_FechaUtc" ON "Facturas" ("UsuarioId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Favoritos_PublicacionId" ON "Favoritos" ("PublicacionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Favoritos_UsuarioId_FechaUtc" ON "Favoritos" ("UsuarioId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Mensajes_AutorId_FechaUtc" ON "Mensajes" ("AutorId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Mensajes_ConversacionId_AutorId_EntregadoUtc" ON "Mensajes" ("ConversacionId", "AutorId", "EntregadoUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Mensajes_ConversacionId_AutorId_LeidoUtc" ON "Mensajes" ("ConversacionId", "AutorId", "LeidoUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Mensajes_ConversacionId_FechaUtc" ON "Mensajes" ("ConversacionId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Mensajes_RespuestaAId" ON "Mensajes" ("RespuestaAId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_MovimientosEcoPuntos_UsuarioId_FechaUtc" ON "MovimientosEcoPuntos" ("UsuarioId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Notificaciones_UsuarioId_LeidaUtc_FechaUtc" ON "Notificaciones" ("UsuarioId", "LeidaUtc", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Pagos_Estado_FechaUtc" ON "Pagos" ("Estado", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Pagos_ProveedorTransaccionId" ON "Pagos" ("ProveedorTransaccionId") WHERE "ProveedorTransaccionId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Pagos_Referencia" ON "Pagos" ("Referencia");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Pagos_UsuarioId_Concepto_Estado" ON "Pagos" ("UsuarioId", "Concepto", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Pqrs_Estado_FechaLimiteUtc" ON "Pqrs" ("Estado", "FechaLimiteUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Pqrs_Radicado" ON "Pqrs" ("Radicado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Pqrs_UsuarioId_FechaUtc" ON "Pqrs" ("UsuarioId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_CategoriaId" ON "Publicaciones" ("CategoriaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_DepartamentoCodigo_MunicipioCodigo" ON "Publicaciones" ("DepartamentoCodigo", "MunicipioCodigo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_DestacadaHasta" ON "Publicaciones" ("DestacadaHasta");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_Estado_EstaOculta" ON "Publicaciones" ("Estado", "EstaOculta");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_FechaRelevancia" ON "Publicaciones" ("FechaRelevancia");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_Latitud_Longitud" ON "Publicaciones" ("Latitud", "Longitud");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Publicaciones_PropietarioId" ON "Publicaciones" ("PropietarioId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_PublicacionImagenes_PublicacionId" ON "PublicacionImagenes" ("PublicacionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_SesionesRefresh_ExpiraUtc" ON "SesionesRefresh" ("ExpiraUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_SesionesRefresh_FamiliaId" ON "SesionesRefresh" ("FamiliaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_SesionesRefresh_TokenHash" ON "SesionesRefresh" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_SesionesRefresh_UsuarioId_RevocadoUtc" ON "SesionesRefresh" ("UsuarioId", "RevocadoUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Solicitudes_PublicacionId_Estado" ON "Solicitudes" ("PublicacionId", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Solicitudes_PublicacionOfrecidaId_Estado" ON "Solicitudes" ("PublicacionOfrecidaId", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Solicitudes_SolicitanteId_Estado" ON "Solicitudes" ("SolicitanteId", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_SuscripcionesPush_Endpoint" ON "SuscripcionesPush" ("Endpoint");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_SuscripcionesPush_UsuarioId" ON "SuscripcionesPush" ("UsuarioId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_TokensUsoUnico_ExpiraUtc" ON "TokensUsoUnico" ("ExpiraUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_TokensUsoUnico_TokenHash" ON "TokensUsoUnico" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_TokensUsoUnico_UsuarioId_Proposito_CreadoUtc" ON "TokensUsoUnico" ("UsuarioId", "Proposito", "CreadoUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Transacciones_OferenteId_FechaUtc" ON "Transacciones" ("OferenteId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Transacciones_ReceptorId_FechaUtc" ON "Transacciones" ("ReceptorId", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Usuarios_CodigoInvitacion" ON "Usuarios" ("CodigoInvitacion") WHERE "CodigoInvitacion" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Usuarios_Correo" ON "Usuarios" ("Correo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Usuarios_CorreoCanonico" ON "Usuarios" ("CorreoCanonico");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Usuarios_EstadoVerificacion" ON "Usuarios" ("EstadoVerificacion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Usuarios_EstaSuspendido" ON "Usuarios" ("EstaSuspendido");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE UNIQUE INDEX "IX_Usuarios_GoogleSub" ON "Usuarios" ("GoogleSub") WHERE "GoogleSub" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Usuarios_InvitadoPorId_RecompensaInvitacionUtc" ON "Usuarios" ("InvitadoPorId", "RecompensaInvitacionUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    CREATE INDEX "IX_Usuarios_TipoCuenta_FechaVencimientoSuscripcion" ON "Usuarios" ("TipoCuenta", "FechaVencimientoSuscripcion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017230439_Inicial') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017230439_Inicial', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017231326_CerrarDataApiSupabase') THEN
    DO $$
    DECLARE t record;
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'anon')
           OR NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'authenticated') THEN
            RETURN;
        END IF;
        REVOKE ALL ON ALL TABLES IN SCHEMA public FROM anon, authenticated;
        REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM anon, authenticated;
        REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM anon, authenticated;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON TABLES FROM anon, authenticated;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON SEQUENCES FROM anon, authenticated;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON FUNCTIONS FROM anon, authenticated;
        FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
            EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t.tablename);
        END LOOP;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017231326_CerrarDataApiSupabase') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017231326_CerrarDataApiSupabase', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018081031_FechaNacimiento') THEN
    ALTER TABLE "Usuarios" ADD "FechaNacimiento" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018081031_FechaNacimiento') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018081031_FechaNacimiento', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018094121_AceptacionTerminos') THEN
    ALTER TABLE "Usuarios" ADD "FechaAceptacionTerminos" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018094121_AceptacionTerminos') THEN
    ALTER TABLE "Usuarios" ADD "TerminosVersion" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018094121_AceptacionTerminos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018094121_AceptacionTerminos', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018100428_PqrsPublica') THEN
    ALTER TABLE "Pqrs" ALTER COLUMN "UsuarioId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018100428_PqrsPublica') THEN
    ALTER TABLE "Pqrs" ADD "CorreoSolicitante" character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018100428_PqrsPublica') THEN
    ALTER TABLE "Pqrs" ADD "NombreSolicitante" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018100428_PqrsPublica') THEN
    CREATE INDEX "IX_Pqrs_CorreoSolicitante_FechaUtc" ON "Pqrs" ("CorreoSolicitante", "FechaUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018100428_PqrsPublica') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018100428_PqrsPublica', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018172611_MenorDetectado') THEN
    ALTER TABLE "Usuarios" ADD "MenorDetectadoUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018172611_MenorDetectado') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018172611_MenorDetectado', '10.0.13');
    END IF;
END $EF$;
COMMIT;

