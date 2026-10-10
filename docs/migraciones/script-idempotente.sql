IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Auditoria] (
        [Id] uniqueidentifier NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Accion] nvarchar(60) NOT NULL,
        [ObjetivoTipo] nvarchar(40) NOT NULL,
        [ObjetivoId] uniqueidentifier NOT NULL,
        [Detalle] nvarchar(300) NULL,
        CONSTRAINT [PK_Auditoria] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Categorias] (
        [Id] int NOT NULL,
        [NombreCategoria] nvarchar(60) NOT NULL,
        [Descripcion] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Categorias] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [Id] uniqueidentifier NOT NULL,
        [NombreCompleto] nvarchar(120) NOT NULL,
        [Localidad] nvarchar(60) NOT NULL,
        [Correo] nvarchar(160) NOT NULL,
        [ClaveHash] nvarchar(256) NOT NULL,
        [FechaRegistro] datetime2 NOT NULL,
        [VersionSeguridad] int NOT NULL,
        [IntentosFallidosLogin] int NOT NULL,
        [BloqueadoHasta] datetime2 NULL,
        [SaldoEcoPuntos] int NOT NULL,
        [Reputacion] decimal(3,2) NOT NULL,
        [TotalTruekesCompletados] int NOT NULL,
        [TotalComprasRealizadas] int NOT NULL,
        [TotalDonacionesRealizadas] int NOT NULL,
        [Rol] nvarchar(20) NOT NULL,
        [TipoCuenta] nvarchar(20) NOT NULL,
        [FechaVencimientoSuscripcion] datetime2 NULL,
        [DestacadosGratisRestantes] int NOT NULL,
        [EstadoVerificacion] nvarchar(20) NOT NULL,
        [DocumentoVerificacionUrl] nvarchar(500) NULL,
        [MotivoRechazoVerificacion] nvarchar(300) NULL,
        [CorreoVerificado] bit NOT NULL,
        [FechaVerificacionCorreo] datetime2 NULL,
        [GoogleSub] varchar(64) NULL,
        [PoliticaDatosVersion] nvarchar(20) NULL,
        [FechaAceptacionPolitica] datetime2 NULL,
        [EstaEliminado] bit NOT NULL,
        [FechaEliminacion] datetime2 NULL,
        [EstaSuspendido] bit NOT NULL,
        [SuspendidoHasta] datetime2 NULL,
        [MotivoSuspension] nvarchar(300) NULL,
        [CalificacionesTotal] int NOT NULL,
        [CalificacionesSuma] int NOT NULL,
        [DosFactoresActivo] bit NOT NULL,
        [SecretoDosFactoresCifrado] varchar(200) NULL,
        [UltimoPasoDosFactores] bigint NOT NULL,
        [CodigosRecuperacionHash] varchar(700) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Denuncias] (
        [Id] uniqueidentifier NOT NULL,
        [DenuncianteId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        [ObjetivoId] uniqueidentifier NOT NULL,
        [Motivo] nvarchar(30) NOT NULL,
        [Detalle] nvarchar(500) NULL,
        [FechaUtc] datetime2 NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [ModeradorId] uniqueidentifier NULL,
        [FechaResolucionUtc] datetime2 NULL,
        [NotaResolucion] nvarchar(300) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Denuncias] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Denuncias_Usuarios_DenuncianteId] FOREIGN KEY ([DenuncianteId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Notificaciones] (
        [Id] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(40) NOT NULL,
        [Mensaje] nvarchar(300) NOT NULL,
        [RecursoId] uniqueidentifier NULL,
        [FechaUtc] datetime2 NOT NULL,
        [LeidaUtc] datetime2 NULL,
        CONSTRAINT [PK_Notificaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notificaciones_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Pagos] (
        [Id] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Concepto] nvarchar(20) NOT NULL,
        [MontoCop] int NOT NULL,
        [Referencia] nvarchar(100) NOT NULL,
        [PuntosCanjeados] int NOT NULL,
        [PublicacionId] uniqueidentifier NULL,
        [DocumentoUrl] nvarchar(500) NULL,
        [FechaUtc] datetime2 NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [ProveedorTransaccionId] nvarchar(100) NULL,
        [FechaResolucionUtc] datetime2 NULL,
        [NotaInterna] nvarchar(300) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Pagos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pagos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Publicaciones] (
        [Id] uniqueidentifier NOT NULL,
        [PropietarioId] uniqueidentifier NOT NULL,
        [Titulo] nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(2000) NOT NULL,
        [CategoriaId] int NOT NULL,
        [Modo] nvarchar(20) NOT NULL,
        [PrecioReferenciaCop] decimal(18,2) NULL,
        [Localidad] nvarchar(60) NOT NULL,
        [Latitud] float NULL,
        [Longitud] float NULL,
        [Estado] nvarchar(20) NOT NULL,
        [FechaPublicacion] datetime2 NOT NULL,
        [FechaEdicion] datetime2 NULL,
        [MotivoCancelacion] nvarchar(300) NULL,
        [DestacadaHasta] datetime2 NULL,
        [EstaOculta] bit NOT NULL,
        [MotivoOcultamiento] nvarchar(300) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Publicaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Publicaciones_Categorias_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [Categorias] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Publicaciones_Usuarios_PropietarioId] FOREIGN KEY ([PropietarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [SesionesRefresh] (
        [Id] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [FamiliaId] uniqueidentifier NOT NULL,
        [TokenHash] varchar(64) NOT NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        [ExpiraFamiliaUtc] datetime2 NOT NULL,
        [ConDosFactores] bit NOT NULL,
        [ReemplazadoPorId] uniqueidentifier NULL,
        [RevocadoUtc] datetime2 NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SesionesRefresh] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SesionesRefresh_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [TokensUsoUnico] (
        [Id] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Proposito] nvarchar(20) NOT NULL,
        [TokenHash] varchar(64) NOT NULL,
        [CreadoUtc] datetime2 NOT NULL,
        [ExpiraUtc] datetime2 NOT NULL,
        [UsadoUtc] datetime2 NULL,
        CONSTRAINT [PK_TokensUsoUnico] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TokensUsoUnico_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Transacciones] (
        [Id] uniqueidentifier NOT NULL,
        [PublicacionId] uniqueidentifier NOT NULL,
        [SolicitudId] uniqueidentifier NOT NULL,
        [OferenteId] uniqueidentifier NOT NULL,
        [ReceptorId] uniqueidentifier NOT NULL,
        [Modo] nvarchar(20) NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [PuntosOtorgadosOferente] bit NOT NULL,
        [PuntosOtorgadosReceptor] bit NOT NULL,
        CONSTRAINT [PK_Transacciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transacciones_Usuarios_OferenteId] FOREIGN KEY ([OferenteId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Transacciones_Usuarios_ReceptorId] FOREIGN KEY ([ReceptorId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Comentarios] (
        [Id] uniqueidentifier NOT NULL,
        [PublicacionId] uniqueidentifier NOT NULL,
        [AutorId] uniqueidentifier NOT NULL,
        [Texto] nvarchar(500) NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [EstaOculto] bit NOT NULL,
        [MotivoOcultamiento] nvarchar(300) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Comentarios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Comentarios_Publicaciones_PublicacionId] FOREIGN KEY ([PublicacionId]) REFERENCES [Publicaciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Comentarios_Usuarios_AutorId] FOREIGN KEY ([AutorId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Favoritos] (
        [UsuarioId] uniqueidentifier NOT NULL,
        [PublicacionId] uniqueidentifier NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Favoritos] PRIMARY KEY ([UsuarioId], [PublicacionId]),
        CONSTRAINT [FK_Favoritos_Publicaciones_PublicacionId] FOREIGN KEY ([PublicacionId]) REFERENCES [Publicaciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Favoritos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [PublicacionImagenes] (
        [Id] int NOT NULL IDENTITY,
        [Url] nvarchar(500) NOT NULL,
        [Orden] int NOT NULL,
        [PublicacionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PublicacionImagenes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PublicacionImagenes_Publicaciones_PublicacionId] FOREIGN KEY ([PublicacionId]) REFERENCES [Publicaciones] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Solicitudes] (
        [Id] uniqueidentifier NOT NULL,
        [PublicacionId] uniqueidentifier NOT NULL,
        [SolicitanteId] uniqueidentifier NOT NULL,
        [FechaSolicitud] datetime2 NOT NULL,
        [Mensaje] nvarchar(500) NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [MotivoRechazo] nvarchar(300) NULL,
        [FechaAceptacionUtc] datetime2 NULL,
        [ConfirmadaPorDuenioUtc] datetime2 NULL,
        [ConfirmadaPorSolicitanteUtc] datetime2 NULL,
        [FechaCierreUtc] datetime2 NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Solicitudes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Solicitudes_Publicaciones_PublicacionId] FOREIGN KEY ([PublicacionId]) REFERENCES [Publicaciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Solicitudes_Usuarios_SolicitanteId] FOREIGN KEY ([SolicitanteId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Calificaciones] (
        [Id] uniqueidentifier NOT NULL,
        [SolicitudId] uniqueidentifier NOT NULL,
        [AutorId] uniqueidentifier NOT NULL,
        [CalificadoId] uniqueidentifier NOT NULL,
        [Estrellas] int NOT NULL,
        [Comentario] nvarchar(300) NULL,
        [FechaUtc] datetime2 NOT NULL,
        [ComentarioOculto] bit NOT NULL,
        [MotivoOcultamiento] nvarchar(300) NULL,
        CONSTRAINT [PK_Calificaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Calificaciones_Solicitudes_SolicitudId] FOREIGN KEY ([SolicitudId]) REFERENCES [Solicitudes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Calificaciones_Usuarios_AutorId] FOREIGN KEY ([AutorId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Calificaciones_Usuarios_CalificadoId] FOREIGN KEY ([CalificadoId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Conversaciones] (
        [Id] uniqueidentifier NOT NULL,
        [SolicitudId] uniqueidentifier NOT NULL,
        [PublicacionId] uniqueidentifier NOT NULL,
        [DuenioId] uniqueidentifier NOT NULL,
        [SolicitanteId] uniqueidentifier NOT NULL,
        [CreadaUtc] datetime2 NOT NULL,
        [UltimoMensajeUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Conversaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversaciones_Publicaciones_PublicacionId] FOREIGN KEY ([PublicacionId]) REFERENCES [Publicaciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Conversaciones_Solicitudes_SolicitudId] FOREIGN KEY ([SolicitudId]) REFERENCES [Solicitudes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Conversaciones_Usuarios_DuenioId] FOREIGN KEY ([DuenioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Conversaciones_Usuarios_SolicitanteId] FOREIGN KEY ([SolicitanteId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE TABLE [Mensajes] (
        [Id] uniqueidentifier NOT NULL,
        [ConversacionId] uniqueidentifier NOT NULL,
        [AutorId] uniqueidentifier NOT NULL,
        [Texto] nvarchar(1000) NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [LeidoUtc] datetime2 NULL,
        [EstaOculto] bit NOT NULL,
        [MotivoOcultamiento] nvarchar(300) NULL,
        CONSTRAINT [PK_Mensajes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Mensajes_Conversaciones_ConversacionId] FOREIGN KEY ([ConversacionId]) REFERENCES [Conversaciones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Mensajes_Usuarios_AutorId] FOREIGN KEY ([AutorId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Descripcion', N'NombreCategoria') AND [object_id] = OBJECT_ID(N'[Categorias]'))
        SET IDENTITY_INSERT [Categorias] ON;
    EXEC(N'INSERT INTO [Categorias] ([Id], [Descripcion], [NombreCategoria])
    VALUES (1, N''Prendas, zapatos y accesorios'', N''Ropa y calzado''),
    (2, N''Libros, textos escolares y útiles'', N''Libros y papelería''),
    (3, N''Aparatos para el hogar'', N''Electrodomésticos''),
    (4, N''Decoración, menaje y muebles'', N''Hogar y muebles''),
    (5, N''Computadores, celulares y accesorios'', N''Tecnología''),
    (6, N''Juguetes y artículos infantiles'', N''Juguetes y niños''),
    (7, N''Todo lo demás'', N''Otros'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Descripcion', N'NombreCategoria') AND [object_id] = OBJECT_ID(N'[Categorias]'))
        SET IDENTITY_INSERT [Categorias] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Auditoria_ActorId] ON [Auditoria] ([ActorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Auditoria_FechaUtc] ON [Auditoria] ([FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Calificaciones_AutorId] ON [Calificaciones] ([AutorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Calificaciones_CalificadoId_FechaUtc] ON [Calificaciones] ([CalificadoId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Calificaciones_SolicitudId_AutorId] ON [Calificaciones] ([SolicitudId], [AutorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Comentarios_AutorId_FechaUtc] ON [Comentarios] ([AutorId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Comentarios_PublicacionId_EstaOculto_FechaUtc] ON [Comentarios] ([PublicacionId], [EstaOculto], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Conversaciones_DuenioId_UltimoMensajeUtc] ON [Conversaciones] ([DuenioId], [UltimoMensajeUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Conversaciones_PublicacionId] ON [Conversaciones] ([PublicacionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Conversaciones_SolicitanteId_UltimoMensajeUtc] ON [Conversaciones] ([SolicitanteId], [UltimoMensajeUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Conversaciones_SolicitudId] ON [Conversaciones] ([SolicitudId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Denuncias_DenuncianteId_Tipo_ObjetivoId] ON [Denuncias] ([DenuncianteId], [Tipo], [ObjetivoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Denuncias_Estado_FechaUtc] ON [Denuncias] ([Estado], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Denuncias_Tipo_ObjetivoId_Estado] ON [Denuncias] ([Tipo], [ObjetivoId], [Estado]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Favoritos_PublicacionId] ON [Favoritos] ([PublicacionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Favoritos_UsuarioId_FechaUtc] ON [Favoritos] ([UsuarioId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Mensajes_AutorId_FechaUtc] ON [Mensajes] ([AutorId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Mensajes_ConversacionId_AutorId_LeidoUtc] ON [Mensajes] ([ConversacionId], [AutorId], [LeidoUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Mensajes_ConversacionId_FechaUtc] ON [Mensajes] ([ConversacionId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Notificaciones_UsuarioId_LeidaUtc_FechaUtc] ON [Notificaciones] ([UsuarioId], [LeidaUtc], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Pagos_Estado_FechaUtc] ON [Pagos] ([Estado], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Pagos_ProveedorTransaccionId] ON [Pagos] ([ProveedorTransaccionId]) WHERE [ProveedorTransaccionId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Pagos_Referencia] ON [Pagos] ([Referencia]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Pagos_UsuarioId_Concepto_Estado] ON [Pagos] ([UsuarioId], [Concepto], [Estado]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_CategoriaId] ON [Publicaciones] ([CategoriaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_Estado_EstaOculta] ON [Publicaciones] ([Estado], [EstaOculta]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_Latitud_Longitud] ON [Publicaciones] ([Latitud], [Longitud]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_PropietarioId] ON [Publicaciones] ([PropietarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_PublicacionImagenes_PublicacionId] ON [PublicacionImagenes] ([PublicacionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_SesionesRefresh_ExpiraUtc] ON [SesionesRefresh] ([ExpiraUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_SesionesRefresh_FamiliaId] ON [SesionesRefresh] ([FamiliaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SesionesRefresh_TokenHash] ON [SesionesRefresh] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_SesionesRefresh_UsuarioId_RevocadoUtc] ON [SesionesRefresh] ([UsuarioId], [RevocadoUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Solicitudes_PublicacionId_Estado] ON [Solicitudes] ([PublicacionId], [Estado]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Solicitudes_SolicitanteId_Estado] ON [Solicitudes] ([SolicitanteId], [Estado]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_TokensUsoUnico_ExpiraUtc] ON [TokensUsoUnico] ([ExpiraUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokensUsoUnico_TokenHash] ON [TokensUsoUnico] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_TokensUsoUnico_UsuarioId_Proposito_CreadoUtc] ON [TokensUsoUnico] ([UsuarioId], [Proposito], [CreadoUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Transacciones_OferenteId_FechaUtc] ON [Transacciones] ([OferenteId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Transacciones_ReceptorId_FechaUtc] ON [Transacciones] ([ReceptorId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_Correo] ON [Usuarios] ([Correo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Usuarios_EstadoVerificacion] ON [Usuarios] ([EstadoVerificacion]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    CREATE INDEX [IX_Usuarios_EstaSuspendido] ON [Usuarios] ([EstaSuspendido]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Usuarios_GoogleSub] ON [Usuarios] ([GoogleSub]) WHERE [GoogleSub] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002174413_Inicial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002174413_Inicial', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    DROP INDEX [IX_Calificaciones_AutorId] ON [Calificaciones];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [BonoBienvenidaOtorgado] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [CorreoCanonico] nvarchar(160) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FacturacionCorreo] nvarchar(160) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FacturacionDireccion] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FacturacionDocumento] varchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FacturacionMunicipioCodigo] varchar(5) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FacturacionNombre] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FacturacionTipoDocumento] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [MunicipioCodigo] varchar(5) NOT NULL DEFAULT '11001';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [Nit] varchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [NombreComercial] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [RecordatorioVencimientoPara] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [Condicion] nvarchar(20) NOT NULL DEFAULT N'Usado';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [DepartamentoCodigo] varchar(2) NOT NULL DEFAULT '11';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [DetalleCondicion] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [FechaImpulso] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [FechaRelevancia] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [MunicipioCodigo] varchar(5) NOT NULL DEFAULT '11001';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    ALTER TABLE [Calificaciones] ADD [CuentaEnPromedio] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    UPDATE [Usuarios] SET [CorreoCanonico] = [Correo];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    UPDATE [Publicaciones] SET [FechaRelevancia] = [FechaPublicacion];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE TABLE [EstadisticasPublicaciones] (
        [PublicacionId] uniqueidentifier NOT NULL,
        [Fecha] date NOT NULL,
        [Vistas] int NOT NULL,
        CONSTRAINT [PK_EstadisticasPublicaciones] PRIMARY KEY ([PublicacionId], [Fecha]),
        CONSTRAINT [FK_EstadisticasPublicaciones_Publicaciones_PublicacionId] FOREIGN KEY ([PublicacionId]) REFERENCES [Publicaciones] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE TABLE [Facturas] (
        [Id] uniqueidentifier NOT NULL,
        [PagoId] uniqueidentifier NOT NULL,
        [Referencia] nvarchar(100) NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Concepto] nvarchar(20) NOT NULL,
        [Descripcion] nvarchar(200) NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [TotalCop] int NOT NULL,
        [IvaPorcentaje] decimal(5,2) NOT NULL,
        [BaseCop] decimal(18,2) NOT NULL,
        [IvaCop] decimal(18,2) NOT NULL,
        [CompradorTipoDocumento] nvarchar(20) NULL,
        [CompradorDocumento] varchar(20) NOT NULL,
        [CompradorNombre] nvarchar(150) NOT NULL,
        [CompradorCorreo] nvarchar(160) NOT NULL,
        [CompradorDireccion] nvarchar(150) NULL,
        [CompradorMunicipioCodigo] varchar(5) NULL,
        [Estado] nvarchar(20) NOT NULL,
        [NumeroDian] nvarchar(50) NULL,
        [Cufe] varchar(120) NULL,
        [FechaEmisionUtc] datetime2 NULL,
        [RequiereNotaCredito] bit NOT NULL,
        [NotaInterna] nvarchar(300) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Facturas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Facturas_Pagos_PagoId] FOREIGN KEY ([PagoId]) REFERENCES [Pagos] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Facturas_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE TABLE [Pqrs] (
        [Id] uniqueidentifier NOT NULL,
        [Radicado] varchar(30) NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        [Asunto] nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(2000) NOT NULL,
        [PagoReferencia] nvarchar(100) NULL,
        [Estado] nvarchar(20) NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [FechaLimiteUtc] datetime2 NOT NULL,
        [Respuesta] nvarchar(2000) NULL,
        [FechaRespuestaUtc] datetime2 NULL,
        [RespondidaPorId] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Pqrs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pqrs_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_CorreoCanonico] ON [Usuarios] ([CorreoCanonico]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Usuarios_TipoCuenta_FechaVencimientoSuscripcion] ON [Usuarios] ([TipoCuenta], [FechaVencimientoSuscripcion]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_DepartamentoCodigo_MunicipioCodigo] ON [Publicaciones] ([DepartamentoCodigo], [MunicipioCodigo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_DestacadaHasta] ON [Publicaciones] ([DestacadaHasta]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Publicaciones_FechaRelevancia] ON [Publicaciones] ([FechaRelevancia]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Calificaciones_AutorId_CalificadoId_FechaUtc] ON [Calificaciones] ([AutorId], [CalificadoId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Facturas_Estado_FechaUtc] ON [Facturas] ([Estado], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Facturas_PagoId] ON [Facturas] ([PagoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Facturas_UsuarioId_FechaUtc] ON [Facturas] ([UsuarioId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Pqrs_Estado_FechaLimiteUtc] ON [Pqrs] ([Estado], [FechaLimiteUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Pqrs_Radicado] ON [Pqrs] ([Radicado]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    CREATE INDEX [IX_Pqrs_UsuarioId_FechaUtc] ON [Pqrs] ([UsuarioId], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200138_EscalaNacionalYMonetizacion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003200138_EscalaNacionalYMonetizacion', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    ALTER TABLE [Mensajes] ADD [RespuestaAId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    ALTER TABLE [Denuncias] ADD [Accion] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    ALTER TABLE [Denuncias] ADD [DenunciadoId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    ALTER TABLE [Denuncias] ADD [ResolucionId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE TABLE [Apelaciones] (
        [Id] uniqueidentifier NOT NULL,
        [ResolucionId] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        [ObjetivoId] uniqueidentifier NOT NULL,
        [AccionOriginal] nvarchar(20) NOT NULL,
        [Texto] nvarchar(1000) NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [ModeradorId] uniqueidentifier NULL,
        [FechaResolucionUtc] datetime2 NULL,
        [NotaResolucion] nvarchar(300) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Apelaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Apelaciones_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE INDEX [IX_Mensajes_RespuestaAId] ON [Mensajes] ([RespuestaAId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE INDEX [IX_Denuncias_DenunciadoId_Estado] ON [Denuncias] ([DenunciadoId], [Estado]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE INDEX [IX_Denuncias_ResolucionId] ON [Denuncias] ([ResolucionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE INDEX [IX_Apelaciones_Estado_FechaUtc] ON [Apelaciones] ([Estado], [FechaUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Apelaciones_ResolucionId] ON [Apelaciones] ([ResolucionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    CREATE INDEX [IX_Apelaciones_UsuarioId] ON [Apelaciones] ([UsuarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    ALTER TABLE [Mensajes] ADD CONSTRAINT [FK_Mensajes_Mensajes_RespuestaAId] FOREIGN KEY ([RespuestaAId]) REFERENCES [Mensajes] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033703_ChatRespuestasYApelaciones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004033703_ChatRespuestasYApelaciones', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004045210_ChatEstadosYPresencia'
)
BEGIN
    ALTER TABLE [Mensajes] ADD [EntregadoUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004045210_ChatEstadosYPresencia'
)
BEGIN
    UPDATE Mensajes SET EntregadoUtc = LeidoUtc WHERE LeidoUtc IS NOT NULL AND EntregadoUtc IS NULL
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004045210_ChatEstadosYPresencia'
)
BEGIN
    CREATE INDEX [IX_Mensajes_ConversacionId_AutorId_EntregadoUtc] ON [Mensajes] ([ConversacionId], [AutorId], [EntregadoUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004045210_ChatEstadosYPresencia'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004045210_ChatEstadosYPresencia', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004045913_FotoPerfil'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [FotoUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004045913_FotoPerfil'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004045913_FotoPerfil', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [AceptaNovedades] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [AvisosCorreoIntercambios] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [AvisosCorreoMensajes] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [AvisosCorreoPlanes] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    CREATE TABLE [Bloqueos] (
        [BloqueadorId] uniqueidentifier NOT NULL,
        [BloqueadoId] uniqueidentifier NOT NULL,
        [FechaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Bloqueos] PRIMARY KEY ([BloqueadorId], [BloqueadoId]),
        CONSTRAINT [FK_Bloqueos_Usuarios_BloqueadoId] FOREIGN KEY ([BloqueadoId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Bloqueos_Usuarios_BloqueadorId] FOREIGN KEY ([BloqueadorId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    CREATE INDEX [IX_Bloqueos_BloqueadoId] ON [Bloqueos] ([BloqueadoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004051728_BloqueosYPreferenciasAvisos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004051728_BloqueosYPreferenciasAvisos', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010141552_LiberarPublicacionesSinAceptada'
)
BEGIN
    UPDATE p SET p.Estado = 'Disponible'
    FROM Publicaciones p
    WHERE p.Estado = 'EnNegociacion'
      AND NOT EXISTS (SELECT 1 FROM Solicitudes s WHERE s.PublicacionId = p.Id AND s.Estado = 'Aceptada');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010141552_LiberarPublicacionesSinAceptada'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010141552_LiberarPublicacionesSinAceptada', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    DROP INDEX [IX_Facturas_PagoId] ON [Facturas];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [CompradorCorreo] nvarchar(160) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [CompradorDireccion] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [CompradorDocumento] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [CompradorMunicipioCodigo] varchar(5) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [CompradorNombre] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [CompradorTipoDocumento] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [FacturaANombre] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Pagos] ADD [FechaEleccionFacturaUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [CorregidaPorId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [ElegidaANombre] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [FechaCorreccionUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [FechaEleccionUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [MotivoCorreccion] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [ReemplazaAId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    ALTER TABLE [Facturas] ADD [ReemplazadaPorId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Facturas_PagoId] ON [Facturas] ([PagoId]) WHERE [Estado] IN (N''Pendiente'', N''Emitida'')');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010153518_FacturaEleccionYCorreccion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010153518_FacturaEleccionYCorreccion', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010204951_BusquedaNormalizada'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [TextoBusqueda] nvarchar(2600) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010204951_BusquedaNormalizada'
)
BEGIN
    ALTER TABLE [Publicaciones] ADD [TituloBusqueda] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010204951_BusquedaNormalizada'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010204951_BusquedaNormalizada', N'8.0.31');
END;
GO

COMMIT;
GO

