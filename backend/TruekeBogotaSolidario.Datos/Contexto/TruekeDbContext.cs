using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Contexto;

public class TruekeDbContext : DbContext
{
    public TruekeDbContext(DbContextOptions<TruekeDbContext> opciones) : base(opciones) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Publicacion> Publicaciones => Set<Publicacion>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<Transaccion> Transacciones => Set<Transaccion>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<AuditoriaEvento> Auditoria => Set<AuditoriaEvento>();
    public DbSet<Comentario> Comentarios => Set<Comentario>();
    public DbSet<SesionRefresh> SesionesRefresh => Set<SesionRefresh>();
    public DbSet<TokenUsoUnico> TokensUsoUnico => Set<TokenUsoUnico>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();
    public DbSet<Denuncia> Denuncias => Set<Denuncia>();
    public DbSet<Apelacion> Apelaciones => Set<Apelacion>();
    public DbSet<Bloqueo> Bloqueos => Set<Bloqueo>();
    public DbSet<Favorito> Favoritos => Set<Favorito>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Pqr> Pqrs => Set<Pqr>();
    public DbSet<EstadisticaPublicacionDiaria> EstadisticasPublicaciones => Set<EstadisticaPublicacionDiaria>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // La concurrencia optimista (rowversion) solo existe en SQL Server; el proveedor InMemory no la soporta.
        var sqlServer = Database.IsSqlServer();

        mb.Entity<Usuario>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.NombreCompleto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Localidad).HasMaxLength(60).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(160).IsRequired();
            e.Property(x => x.ClaveHash).HasMaxLength(256).IsRequired(); // "" = sin contraseña (Google o eliminada)
            e.Property(x => x.Reputacion).HasPrecision(3, 2);
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TipoCuenta).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EstadoVerificacion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DocumentoVerificacionUrl).HasMaxLength(500);
            e.Property(x => x.FotoUrl).HasMaxLength(500);
            e.Property(x => x.MotivoRechazoVerificacion).HasMaxLength(300);
            e.HasIndex(x => x.Correo).IsUnique();
            e.HasIndex(x => x.EstadoVerificacion);
            e.Property(x => x.GoogleSub).HasMaxLength(64).IsUnicode(false);
            e.HasIndex(x => x.GoogleSub).IsUnique().HasFilter("[GoogleSub] IS NOT NULL");
            e.Property(x => x.PoliticaDatosVersion).HasMaxLength(20);
            e.Property(x => x.MotivoSuspension).HasMaxLength(300);
            e.Property(x => x.SecretoDosFactoresCifrado).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.CodigosRecuperacionHash).HasMaxLength(700).IsUnicode(false);
            e.HasIndex(x => x.EstaSuspendido);
            e.Property(x => x.CorreoCanonico).HasMaxLength(160).IsRequired();
            e.HasIndex(x => x.CorreoCanonico).IsUnique();
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false).IsRequired();
            e.Property(x => x.NombreComercial).HasMaxLength(120);
            e.Property(x => x.Nit).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.FacturacionTipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FacturacionDocumento).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.FacturacionNombre).HasMaxLength(150);
            e.Property(x => x.FacturacionCorreo).HasMaxLength(160);
            e.Property(x => x.FacturacionDireccion).HasMaxLength(150);
            e.Property(x => x.FacturacionMunicipioCodigo).HasMaxLength(5).IsUnicode(false);
            e.HasIndex(x => new { x.TipoCuenta, x.FechaVencimientoSuscripcion }); // recordatorios e ingresos recurrentes
            e.Ignore(x => x.TieneDatosFacturacion);
            e.Ignore(x => x.DatosFacturacion);
            e.Ignore(x => x.EsVerificado);
            e.Ignore(x => x.TieneClave);
            e.Ignore(x => x.CalificacionPromedio);
            e.Ignore(x => x.CodigosRecuperacionRestantes);
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Categoria>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.NombreCategoria).HasMaxLength(60).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(200);
            e.HasData(
                new { Id = 1, NombreCategoria = "Ropa y calzado", Descripcion = "Prendas, zapatos y accesorios" },
                new { Id = 2, NombreCategoria = "Libros y papelería", Descripcion = "Libros, textos escolares y útiles" },
                new { Id = 3, NombreCategoria = "Electrodomésticos", Descripcion = "Aparatos para el hogar" },
                new { Id = 4, NombreCategoria = "Hogar y muebles", Descripcion = "Decoración, menaje y muebles" },
                new { Id = 5, NombreCategoria = "Tecnología", Descripcion = "Computadores, celulares y accesorios" },
                new { Id = 6, NombreCategoria = "Juguetes y niños", Descripcion = "Juguetes y artículos infantiles" },
                new { Id = 7, NombreCategoria = "Otros", Descripcion = "Todo lo demás" });
        });

        mb.Entity<Publicacion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Localidad).HasMaxLength(60).IsRequired();
            e.OwnsMany(x => x.Imagenes, i =>
            {
                i.ToTable("PublicacionImagenes");
                i.WithOwner().HasForeignKey("PublicacionId");
                i.Property<int>("Id");
                i.HasKey("Id");
                i.Property(x => x.Url).HasMaxLength(500).IsRequired();
            });
            e.Navigation(x => x.Imagenes).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Property(x => x.MotivoCancelacion).HasMaxLength(300);
            e.Property(x => x.MotivoOcultamiento).HasMaxLength(300);
            e.Property(x => x.PrecioReferenciaCop).HasPrecision(18, 2);
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Condicion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DetalleCondicion).HasMaxLength(Publicacion.LongitudMaximaDetalleCondicion);
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false).IsRequired();
            e.Property(x => x.DepartamentoCodigo).HasMaxLength(2).IsUnicode(false).IsRequired();
            e.Ignore(x => x.ProximoImpulsoPosible);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Propietario).WithMany().HasForeignKey(x => x.PropietarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Estado, x.EstaOculta });
            e.HasIndex(x => x.PropietarioId);
            e.HasIndex(x => x.CategoriaId);
            e.HasIndex(x => new { x.DepartamentoCodigo, x.MunicipioCodigo }); // catálogo por departamento / municipio
            e.HasIndex(x => x.FechaRelevancia);                              // orden "Más recientes" (con impulsos)
            e.HasIndex(x => x.DestacadaHasta);
            e.HasIndex(x => new { x.Latitud, x.Longitud }); // prefiltro de /publicaciones/cercanas
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Solicitud>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Mensaje).HasMaxLength(500).IsRequired();
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Publicacion).WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Solicitante).WithMany().HasForeignKey(x => x.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PublicacionId, x.Estado });
            e.HasIndex(x => new { x.SolicitanteId, x.Estado });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Comentario>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Texto).HasMaxLength(Comentario.LongitudMaxima).IsRequired();
            e.Property(x => x.MotivoOcultamiento).HasMaxLength(300);
            e.HasOne(x => x.Publicacion).WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Autor).WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PublicacionId, x.EstaOculto, x.FechaUtc });
            e.HasIndex(x => new { x.AutorId, x.FechaUtc });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<SesionRefresh>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired().IsUnicode(false);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UsuarioId, x.RevocadoUtc });
            e.HasIndex(x => x.FamiliaId);
            e.HasIndex(x => x.ExpiraUtc);
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<TokenUsoUnico>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired().IsUnicode(false);
            e.Property(x => x.Proposito).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UsuarioId, x.Proposito, x.CreadoUtc });
            e.HasIndex(x => x.ExpiraUtc);
        });

        mb.Entity<Notificacion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Tipo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Mensaje).HasMaxLength(300).IsRequired();
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UsuarioId, x.LeidaUtc, x.FechaUtc });
        });

        mb.Entity<Conversacion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne(x => x.Solicitud).WithMany().HasForeignKey(x => x.SolicitudId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Publicacion>().WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Duenio).WithMany().HasForeignKey(x => x.DuenioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Solicitante).WithMany().HasForeignKey(x => x.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SolicitudId).IsUnique();
            e.HasIndex(x => new { x.DuenioId, x.UltimoMensajeUtc });
            e.HasIndex(x => new { x.SolicitanteId, x.UltimoMensajeUtc });
        });

        mb.Entity<Mensaje>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Texto).HasMaxLength(Mensaje.LongitudMaxima).IsRequired();
            e.Property(x => x.MotivoOcultamiento).HasMaxLength(300);
            e.HasOne(x => x.Conversacion).WithMany().HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RespuestaA).WithMany().HasForeignKey(x => x.RespuestaAId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ConversacionId, x.FechaUtc });
            e.HasIndex(x => new { x.ConversacionId, x.AutorId, x.LeidoUtc });
            e.HasIndex(x => new { x.ConversacionId, x.AutorId, x.EntregadoUtc });
            e.HasIndex(x => new { x.AutorId, x.FechaUtc });
        });

        mb.Entity<Denuncia>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Motivo).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Detalle).HasMaxLength(500);
            e.Property(x => x.NotaResolucion).HasMaxLength(300);
            e.Property(x => x.Accion).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DenuncianteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DenuncianteId, x.Tipo, x.ObjetivoId }).IsUnique();
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            e.HasIndex(x => new { x.Tipo, x.ObjetivoId, x.Estado });
            e.HasIndex(x => new { x.DenunciadoId, x.Estado });
            e.HasIndex(x => x.ResolucionId);
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Bloqueo>(e =>
        {
            e.HasKey(x => new { x.BloqueadorId, x.BloqueadoId });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.BloqueadorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Bloqueado).WithMany().HasForeignKey(x => x.BloqueadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BloqueadoId);
        });

        mb.Entity<Apelacion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.AccionOriginal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Texto).HasMaxLength(Apelacion.LongitudMaxima).IsRequired();
            e.Property(x => x.NotaResolucion).HasMaxLength(300);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ResolucionId).IsUnique();   // una apelación por resolución
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            e.HasIndex(x => x.UsuarioId);
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Favorito>(e =>
        {
            e.HasKey(x => new { x.UsuarioId, x.PublicacionId });
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Publicacion>().WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
        });

        mb.Entity<Calificacion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.Property(x => x.MotivoOcultamiento).HasMaxLength(300);
            e.HasOne<Solicitud>().WithMany().HasForeignKey(x => x.SolicitudId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Autor).WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CalificadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SolicitudId, x.AutorId }).IsUnique();
            e.HasIndex(x => new { x.CalificadoId, x.FechaUtc });
            e.HasIndex(x => new { x.AutorId, x.CalificadoId, x.FechaUtc }); // anti-inflado: una calificación que cuenta por pareja y mes
        });

        mb.Entity<Transaccion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.OferenteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ReceptorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OferenteId, x.FechaUtc });
            e.HasIndex(x => new { x.ReceptorId, x.FechaUtc });
        });

        mb.Entity<AuditoriaEvento>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Accion).HasMaxLength(60).IsRequired();
            e.Property(x => x.ObjetivoTipo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Detalle).HasMaxLength(300);
            e.HasIndex(x => x.FechaUtc);
            e.HasIndex(x => x.ActorId);
        });

        mb.Entity<Pago>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Referencia).HasMaxLength(100).IsRequired();
            e.Property(x => x.Concepto).HasConversion<string>().HasMaxLength(20);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DocumentoUrl).HasMaxLength(500);
            e.Property(x => x.ProveedorTransaccionId).HasMaxLength(100);
            e.Property(x => x.NotaInterna).HasMaxLength(300);
            e.Ignore(x => x.MontoEnCentavos);
            e.Ignore(x => x.DatosCompradorElegidos);
            e.Property(x => x.CompradorTipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CompradorDocumento).HasMaxLength(20);
            e.Property(x => x.CompradorNombre).HasMaxLength(150);
            e.Property(x => x.CompradorCorreo).HasMaxLength(160);
            e.Property(x => x.CompradorDireccion).HasMaxLength(150);
            e.Property(x => x.CompradorMunicipioCodigo).HasMaxLength(5).IsUnicode(false);
            e.HasIndex(x => x.Referencia).IsUnique();
            e.HasIndex(x => x.ProveedorTransaccionId).IsUnique().HasFilter("[ProveedorTransaccionId] IS NOT NULL");
            e.HasIndex(x => new { x.UsuarioId, x.Concepto, x.Estado });
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Factura>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Referencia).HasMaxLength(100).IsRequired();
            e.Property(x => x.Concepto).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
            e.Property(x => x.IvaPorcentaje).HasPrecision(5, 2);
            e.Property(x => x.BaseCop).HasPrecision(18, 2);
            e.Property(x => x.IvaCop).HasPrecision(18, 2);
            e.Property(x => x.CompradorTipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CompradorDocumento).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.CompradorNombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.CompradorCorreo).HasMaxLength(160).IsRequired();
            e.Property(x => x.CompradorDireccion).HasMaxLength(150);
            e.Property(x => x.CompradorMunicipioCodigo).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.NumeroDian).HasMaxLength(50);
            e.Property(x => x.Cufe).HasMaxLength(120).IsUnicode(false);
            e.Property(x => x.NotaInterna).HasMaxLength(300);
            e.HasOne<Pago>().WithMany().HasForeignKey(x => x.PagoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            // Una factura VIGENTE por pago (idempotencia); las anuladas o reemplazadas quedan como historial.
            e.HasIndex(x => x.PagoId).IsUnique().HasFilter("[Estado] IN (N'Pendiente', N'Emitida')");
            e.Property(x => x.MotivoCorreccion).HasMaxLength(300);
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Pqr>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Radicado).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Asunto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(Pqr.LongitudMaximaTexto).IsRequired();
            e.Property(x => x.Respuesta).HasMaxLength(Pqr.LongitudMaximaTexto);
            e.Property(x => x.PagoReferencia).HasMaxLength(100);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Radicado).IsUnique();
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
            e.HasIndex(x => new { x.Estado, x.FechaLimiteUtc });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<EstadisticaPublicacionDiaria>(e =>
        {
            e.ToTable("EstadisticasPublicaciones");
            e.HasKey(x => new { x.PublicacionId, x.Fecha });
            if (sqlServer) e.Property(x => x.Fecha).HasColumnType("date");
            e.HasOne<Publicacion>().WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
