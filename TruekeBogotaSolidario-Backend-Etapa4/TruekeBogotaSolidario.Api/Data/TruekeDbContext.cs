using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Api.Domain.Entities;

namespace TruekeBogotaSolidario.Api.Data;

// ==========================================================================
// CAPA DE DATOS - DbContext (persistencia real via EF Core)
// Es el punto de entrada a la base de datos relacional (SQL Server segun
// el stack academico del proyecto en el Modelo C4 - Nivel 2).
// Reemplaza el "en memoria" del ejemplo de referencia por persistencia real,
// que es lo que exige la Etapa 4 (back-end obligatorio).
// ==========================================================================
public class TruekeDbContext : DbContext
{
    public TruekeDbContext(DbContextOptions<TruekeDbContext> options) : base(options) { }

    public DbSet<Publicacion> Publicaciones => Set<Publicacion>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Publicacion "*" --> "1" Categoria : pertenece a ---
        modelBuilder.Entity<Publicacion>()
            .HasOne(p => p.Categoria)
            .WithMany(c => c.Publicaciones)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Usuario "1" --> "*" Publicacion : crea y gestiona ---
        modelBuilder.Entity<Publicacion>()
            .HasOne(p => p.Usuario)
            .WithMany(u => u.Publicaciones)
            .HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Usuario "1" --> "*" Solicitud : envia ---
        modelBuilder.Entity<Solicitud>()
            .HasOne(s => s.Usuario)
            .WithMany(u => u.SolicitudesEnviadas)
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Solicitud "*" --> "1" Publicacion : referencia ---
        modelBuilder.Entity<Solicitud>()
            .HasOne(s => s.Publicacion)
            .WithMany(p => p.Solicitudes)
            .HasForeignKey(s => s.PublicacionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Publicacion>().Property(p => p.Estado).HasConversion<string>();

        // --- Datos de prueba (seed), igual espiritu que el ProductoRepository prepoblado ---
        var categoriaRopa = new Categoria { Id = 1, NombreCategoria = "Ropa", Descripcion = "Prendas de vestir en buen estado" };
        var categoriaLibros = new Categoria { Id = 2, NombreCategoria = "Libros", Descripcion = "Libros usados de todo genero" };
        var categoriaHogar = new Categoria { Id = 3, NombreCategoria = "Hogar", Descripcion = "Electrodomesticos y articulos del hogar" };

        modelBuilder.Entity<Categoria>().HasData(categoriaRopa, categoriaLibros, categoriaHogar);

        var usuarioDemo = new Usuario
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            NombreCompleto = "Martin Gutierrez",
            Localidad = "Suba",
            Correo = "martin.demo@trueke.co"
        };

        modelBuilder.Entity<Usuario>().HasData(usuarioDemo);

        modelBuilder.Entity<Publicacion>().HasData(new Publicacion
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Titulo = "Chaqueta impermeable talla M",
            Descripcion = "Usada dos veces, ideal para las lluvias de Bogota.",
            FechaPublicacion = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            CategoriaId = categoriaRopa.Id,
            UsuarioId = usuarioDemo.Id
        });
    }
}
