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
            e.Property(x => x.MotivoRechazoVerificacion).HasMaxLength(300);
            e.HasIndex(x => x.Correo).IsUnique();
            e.HasIndex(x => x.EstadoVerificacion);
            e.Property(x => x.GoogleSub).HasMaxLength(64).IsUnicode(false);
            e.HasIndex(x => x.GoogleSub).IsUnique().HasFilter("[GoogleSub] IS NOT NULL");
            e.Property(x => x.PoliticaDatosVersion).HasMaxLength(20);
            e.Ignore(x => x.EsVerificado);
            e.Ignore(x => x.TieneClave);
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
            e.Property(x => x.ImagenUrl).HasMaxLength(500);
            e.Property(x => x.MotivoCancelacion).HasMaxLength(300);
            e.Property(x => x.MotivoOcultamiento).HasMaxLength(300);
            e.Property(x => x.PrecioReferenciaCop).HasPrecision(18, 2);
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Propietario).WithMany().HasForeignKey(x => x.PropietarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Estado, x.EstaOculta });
            e.HasIndex(x => x.PropietarioId);
            e.HasIndex(x => x.CategoriaId);
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
            e.HasIndex(x => new { x.ConversacionId, x.FechaUtc });
            e.HasIndex(x => new { x.ConversacionId, x.AutorId, x.LeidoUtc });
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
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.DenuncianteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DenuncianteId, x.Tipo, x.ObjetivoId }).IsUnique();
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            e.HasIndex(x => new { x.Tipo, x.ObjetivoId, x.Estado });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            e.HasIndex(x => x.Referencia).IsUnique();
            e.HasIndex(x => x.ProveedorTransaccionId).IsUnique().HasFilter("[ProveedorTransaccionId] IS NOT NULL");
            e.HasIndex(x => new { x.UsuarioId, x.Concepto, x.Estado });
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            if (sqlServer) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });
    }
}
