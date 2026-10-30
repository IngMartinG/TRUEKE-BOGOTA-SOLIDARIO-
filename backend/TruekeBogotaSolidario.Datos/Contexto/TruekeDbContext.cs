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
    public DbSet<FactorImpacto> FactoresImpacto => Set<FactorImpacto>();
    public DbSet<RegistroImpacto> RegistrosImpacto => Set<RegistroImpacto>();
    public DbSet<MetaComunitaria> MetasComunitarias => Set<MetaComunitaria>();
    public DbSet<MovimientoEcoPuntos> MovimientosEcoPuntos => Set<MovimientoEcoPuntos>();
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
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();
    public DbSet<BusquedaGuardada> BusquedasGuardadas => Set<BusquedaGuardada>();
    public DbSet<Deseo> Deseos => Set<Deseo>();
    public DbSet<Fundacion> Fundaciones => Set<Fundacion>();
    public DbSet<NecesidadFundacion> NecesidadesFundacion => Set<NecesidadFundacion>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<MiembroEmpresa> MiembrosEmpresa => Set<MiembroEmpresa>();
    public DbSet<Campana> Campanas => Set<Campana>();
    public DbSet<InformeImpacto> InformesImpacto => Set<InformeImpacto>();
    public DbSet<EvidenciaEntrega> EvidenciasEntrega => Set<EvidenciaEntrega>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Pqr> Pqrs => Set<Pqr>();
    public DbSet<EstadisticaPublicacionDiaria> EstadisticasPublicaciones => Set<EstadisticaPublicacionDiaria>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // La concurrencia optimista usa xmin de PostgreSQL (cambia en cada UPDATE); el proveedor InMemory no la soporta.
        var postgres = Database.IsNpgsql();

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
            e.Property(x => x.EstadoFoto).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivoRechazoFoto).HasMaxLength(300);
            e.HasIndex(x => x.EstadoFoto);
            e.Property(x => x.MotivoRechazoVerificacion).HasMaxLength(300);
            e.HasIndex(x => x.Correo).IsUnique();
            e.HasIndex(x => x.EstadoVerificacion);
            e.Property(x => x.GoogleSub).HasMaxLength(64).IsUnicode(false);
            e.HasIndex(x => x.GoogleSub).IsUnique().HasFilter("\"GoogleSub\" IS NOT NULL");
            e.Property(x => x.PoliticaDatosVersion).HasMaxLength(20);
            e.Property(x => x.TerminosVersion).HasMaxLength(20);
            e.Property(x => x.MotivoSuspension).HasMaxLength(300);
            e.Property(x => x.SecretoDosFactoresCifrado).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.CodigosRecuperacionHash).HasMaxLength(700).IsUnicode(false);
            e.HasIndex(x => x.EstaSuspendido);
            e.Property(x => x.CorreoCanonico).HasMaxLength(160).IsRequired();
            e.HasIndex(x => x.CorreoCanonico).IsUnique();
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false).IsRequired();
            e.Property(x => x.NombreComercial).HasMaxLength(120);
            e.Property(x => x.DescripcionNegocio).HasMaxLength(Usuario.LongitudDescripcionNegocio);
            e.Property(x => x.HorarioNegocio).HasMaxLength(Usuario.LongitudHorarioNegocio);
            e.Property(x => x.NombreFundacion).HasMaxLength(150);
            e.Property(x => x.Nit).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.FacturacionTipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FacturacionDocumento).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.FacturacionNombre).HasMaxLength(150);
            e.Property(x => x.FacturacionCorreo).HasMaxLength(160);
            e.Property(x => x.FacturacionDireccion).HasMaxLength(150);
            e.Property(x => x.FacturacionMunicipioCodigo).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.OferenteTipoDocumento).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.OferenteNombre).HasMaxLength(150);
            e.Property(x => x.OferenteDocumentoCifrado).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.OferenteDireccionCifrada).HasMaxLength(800).IsUnicode(false);
            e.Property(x => x.OferenteMunicipioCodigo).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.OferenteTelefonoCifrado).HasMaxLength(200).IsUnicode(false);
            e.HasIndex(x => new { x.TipoCuenta, x.FechaVencimientoSuscripcion }); // recordatorios e ingresos recurrentes
            e.Property(x => x.CodigoInvitacion).HasMaxLength(12).IsUnicode(false);
            e.HasIndex(x => x.CodigoInvitacion).IsUnique().HasFilter("\"CodigoInvitacion\" IS NOT NULL");
            e.HasIndex(x => new { x.InvitadoPorId, x.RecompensaInvitacionUtc }); // contador y tope de invitaciones
            e.Ignore(x => x.TieneDatosFacturacion);
            e.Ignore(x => x.DatosFacturacion);
            e.Ignore(x => x.EsVerificado);
            e.Ignore(x => x.TieneClave);
            e.Ignore(x => x.CalificacionPromedio);
            e.Ignore(x => x.CodigosRecuperacionRestantes);
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            e.Property(x => x.TituloBusqueda).HasMaxLength(200).IsRequired();
            e.Property(x => x.TextoBusqueda).HasMaxLength(Common.TextoBusqueda.LongitudMaxima).IsRequired();
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
            e.Property(x => x.EstadoModeracion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivosRiesgo).HasMaxLength(Publicacion.LongitudMaximaMotivosRiesgo);
            e.Property(x => x.MotivoRechazo).HasMaxLength(Publicacion.LongitudMaximaMotivoRechazo);
            e.HasIndex(x => x.EstadoModeracion);
            // Sin llave foránea: la fundación puede borrar la necesidad y la donación sigue igual.
            e.HasIndex(x => x.NecesidadFundacionId);
            e.HasOne<Campana>().WithMany().HasForeignKey(x => x.CampanaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CampanaId);
            e.HasIndex(x => new { x.Estado, x.DisponibilidadPedidaUtc }); // "¿Sigue disponible?"
            e.Ignore(x => x.EsVisible);
            e.Property(x => x.PrecioReferenciaCop).HasPrecision(18, 2);
            e.Property(x => x.PesoEstimadoKg).HasPrecision(8, 2);
            e.Property(x => x.Material).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Condicion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DetalleCondicion).HasMaxLength(Publicacion.LongitudMaximaDetalleCondicion);
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false).IsRequired();
            e.Property(x => x.DepartamentoCodigo).HasMaxLength(2).IsUnicode(false).IsRequired();
            e.Ignore(x => x.ProximoImpulsoPosible);
            e.Ignore(x => x.AdmiteTrueke);
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Propietario).WithMany().HasForeignKey(x => x.PropietarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Estado, x.EstaOculta });
            e.HasIndex(x => x.PropietarioId);
            e.HasIndex(x => x.CategoriaId);
            e.HasIndex(x => new { x.DepartamentoCodigo, x.MunicipioCodigo }); // catálogo por departamento / municipio
            e.HasIndex(x => x.FechaRelevancia);                              // orden "Más recientes" (con impulsos)
            e.HasIndex(x => x.DestacadaHasta);
            e.HasIndex(x => new { x.Latitud, x.Longitud }); // prefiltro de /publicaciones/cercanas
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<Solicitud>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Mensaje).HasMaxLength(500).IsRequired();
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Publicacion).WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Solicitante).WithMany().HasForeignKey(x => x.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.PrecioPropuestoCop).HasPrecision(18, 2);
            e.HasMany(x => x.Ofrecidas).WithOne().HasForeignKey(o => o.SolicitudId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Ofrecidas).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasIndex(x => new { x.PublicacionId, x.Estado });
            e.HasIndex(x => new { x.SolicitanteId, x.Estado });
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<SolicitudObjeto>(e =>
        {
            e.ToTable("SolicitudObjetos");
            e.HasKey(x => new { x.SolicitudId, x.PublicacionId });
            e.HasOne(x => x.Publicacion).WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PublicacionId);
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
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            e.Property(x => x.FotoUrl).HasMaxLength(Mensaje.LongitudMaximaFoto);
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
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<BusquedaGuardada>(e =>
        {
            e.ToTable("BusquedasGuardadas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Texto).HasMaxLength(100);
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Condicion).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DepartamentoCodigo).HasMaxLength(2).IsUnicode(false);
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.Localidad).HasMaxLength(60);
            e.Property(x => x.PrecioMin).HasPrecision(18, 2);
            e.Property(x => x.PrecioMax).HasPrecision(18, 2);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UsuarioId);
            e.HasIndex(x => new { x.AlertasActivas, x.RevisadaHastaUtc });
        });

        mb.Entity<Deseo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Texto).HasMaxLength(Deseo.LongitudMaxima).IsRequired();
            e.Property(x => x.MotivoOcultamiento).HasMaxLength(300);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Categoria>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UsuarioId);
            e.HasIndex(x => new { x.AvisosActivos, x.RevisadoHastaUtc });
        });

        mb.Entity<Fundacion>(e =>
        {
            e.ToTable("Fundaciones");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RazonSocial).HasMaxLength(150).IsRequired();
            e.Property(x => x.Nit).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(Fundacion.LongitudDescripcion).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CertificadoUrl).HasMaxLength(500);
            e.Property(x => x.MotivoDecision).HasMaxLength(300);
            e.Property(x => x.NotaRegimen).HasMaxLength(300);
            e.HasOne<Usuario>().WithOne().HasForeignKey<Fundacion>(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UsuarioId).IsUnique();
            e.HasIndex(x => x.Estado);
            e.HasIndex(x => x.Nit);
        });

        mb.Entity<NecesidadFundacion>(e =>
        {
            e.ToTable("NecesidadesFundacion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Texto).HasMaxLength(NecesidadFundacion.LongitudMaxima).IsRequired();
            e.Ignore(x => x.EstaCubierta);
            e.HasOne<Fundacion>().WithMany().HasForeignKey(x => x.FundacionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Categoria>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.FundacionId);
        });

        mb.Entity<SuscripcionPush>(e =>
        {
            e.ToTable("SuscripcionesPush");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Endpoint).HasMaxLength(1000).IsUnicode(false).IsRequired(); // ASCII: el índice único cabe en 1.000 bytes
            e.Property(x => x.P256dh).HasMaxLength(200).IsUnicode(false).IsRequired();
            e.Property(x => x.Auth).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.HasIndex(x => x.UsuarioId);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
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

        mb.Entity<MovimientoEcoPuntos>(e =>
        {
            e.ToTable("MovimientosEcoPuntos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Concepto).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Detalle).HasMaxLength(160);
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
        });
        mb.Entity<Usuario>()
            .HasMany(x => x.MovimientosEcoPuntos).WithOne().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Usuario>().Navigation(x => x.MovimientosEcoPuntos).UsePropertyAccessMode(PropertyAccessMode.Field);

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

        mb.Entity<FactorImpacto>(e =>
        {
            e.ToTable("FactoresImpacto");
            e.HasKey(x => x.Material);
            e.Property(x => x.Material).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.KgCo2PorKg).HasPrecision(8, 3);
            e.Property(x => x.Fuente).HasMaxLength(FactorImpacto.LongitudMaximaFuente).IsRequired();
            e.Property(x => x.Version).HasMaxLength(FactorImpacto.LongitudMaximaVersion).IsRequired();
            // Metodología 2.0.0 (docs/METODOLOGIA-IMPACTO.md): factores conservadores por material.
            var inicio = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);
            const string fuente = "Promedios conservadores de estudios de ciclo de vida (WRAP, EPA), recortados a la baja";
            e.HasData(
                new { Material = MaterialObjeto.Textil, KgCo2PorKg = 8.0m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Electronica, KgCo2PorKg = 12.0m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Electrodomestico, KgCo2PorKg = 5.0m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Madera, KgCo2PorKg = 1.2m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Papel, KgCo2PorKg = 0.9m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Plastico, KgCo2PorKg = 3.0m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Vidrio, KgCo2PorKg = 0.7m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Metal, KgCo2PorKg = 4.0m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio },
                new { Material = MaterialObjeto.Mixto, KgCo2PorKg = 1.5m, Fuente = fuente, Version = "2.0.0", ActualizadoUtc = inicio });
        });

        mb.Entity<RegistroImpacto>(e =>
        {
            e.ToTable("RegistrosImpacto");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Modo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Material).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.OrigenPeso).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Kg).HasPrecision(10, 2);
            e.Property(x => x.KgCo2).HasPrecision(12, 2);
            e.Property(x => x.FactorKgCo2PorKg).HasPrecision(8, 3);
            e.Property(x => x.Version).HasMaxLength(FactorImpacto.LongitudMaximaVersion).IsRequired();
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false).IsRequired();
            e.Property(x => x.Localidad).HasMaxLength(60).IsRequired();
            e.HasOne<Transaccion>().WithMany().HasForeignKey(x => x.TransaccionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Publicacion>().WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TransaccionId, x.PublicacionId }).IsUnique(); // un objeto se cuenta una vez por intercambio
            e.HasIndex(x => x.FechaUtc);
            e.HasIndex(x => new { x.MunicipioCodigo, x.Localidad });
            e.HasIndex(x => x.OferenteId);
            e.HasIndex(x => x.ReceptorId);
            e.HasOne<Campana>().WithMany().HasForeignKey(x => x.CampanaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CampanaId, x.FechaUtc });
        });

        mb.Entity<MetaComunitaria>(e =>
        {
            e.ToTable("MetasComunitarias");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.MunicipioCodigo).HasMaxLength(5).IsUnicode(false).IsRequired();
            e.Property(x => x.Localidad).HasMaxLength(60).IsRequired();
            e.Property(x => x.MetaKg).HasPrecision(10, 0);
            e.HasIndex(x => new { x.MunicipioCodigo, x.Localidad }).IsUnique(); // una meta por localidad
        });


        mb.Entity<Empresa>(e =>
        {
            e.ToTable("Empresas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RazonSocial).HasMaxLength(150).IsRequired();
            e.Property(x => x.Nit).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CertificadoUrl).HasMaxLength(500);
            e.Property(x => x.MotivoDecision).HasMaxLength(300);
            e.Ignore(x => x.EstaAprobada);
            e.HasIndex(x => x.Estado);
            e.HasIndex(x => x.Nit);
        });

        mb.Entity<MiembroEmpresa>(e =>
        {
            e.ToTable("MiembrosEmpresa");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CodigoHash).HasMaxLength(64).IsUnicode(false);
            e.Ignore(x => x.EsActivo);
            e.Ignore(x => x.EsAdmin);
            e.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UsuarioId).IsUnique(); // una cuenta, una empresa (las invitaciones tienen UsuarioId null)
            e.HasIndex(x => x.CodigoHash).IsUnique();
            e.HasIndex(x => x.EmpresaId);
        });

        mb.Entity<Campana>(e =>
        {
            e.ToTable("Campanas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(Campana.LongitudNombre).IsRequired();
            e.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.EmpresaId);
        });

        mb.Entity<InformeImpacto>(e =>
        {
            e.ToTable("InformesImpacto");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Versiones).HasMaxLength(200).IsRequired();
            e.Property(x => x.Contenido).IsRequired();
            e.Property(x => x.Huella).HasMaxLength(64).IsUnicode(false).IsRequired();
            e.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Campana>().WithMany().HasForeignKey(x => x.CampanaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EmpresaId, x.EmitidoUtc });
        });

        mb.Entity<EvidenciaEntrega>(e =>
        {
            e.ToTable("EvidenciasEntrega");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FotoUrl).HasMaxLength(500).IsRequired();
            e.HasOne<Solicitud>().WithMany().HasForeignKey(x => x.SolicitudId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SolicitudId, x.UsuarioId }).IsUnique(); // una foto por persona y entrega
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
            e.HasIndex(x => x.ProveedorTransaccionId).IsUnique().HasFilter("\"ProveedorTransaccionId\" IS NOT NULL");
            e.HasIndex(x => new { x.UsuarioId, x.Concepto, x.Estado });
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            e.HasIndex(x => x.PagoId).IsUnique().HasFilter("\"Estado\" IN ('Pendiente', 'Emitida')");
            e.Property(x => x.MotivoCorreccion).HasMaxLength(300);
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
            e.HasIndex(x => new { x.Estado, x.FechaUtc });
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
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
            e.Property(x => x.NombreSolicitante).HasMaxLength(120);
            e.Property(x => x.CorreoSolicitante).HasMaxLength(160);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CorreoSolicitante, x.FechaUtc });
            e.HasIndex(x => x.Radicado).IsUnique();
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
            e.HasIndex(x => new { x.Estado, x.FechaLimiteUtc });
            if (postgres) e.Property(x => x.RowVersion).IsRowVersion(); else e.Ignore(x => x.RowVersion);
        });

        mb.Entity<EstadisticaPublicacionDiaria>(e =>
        {
            e.ToTable("EstadisticasPublicaciones");
            e.HasKey(x => new { x.PublicacionId, x.Fecha });
            if (postgres) e.Property(x => x.Fecha).HasColumnType("date");
            e.HasOne<Publicacion>().WithMany().HasForeignKey(x => x.PublicacionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
