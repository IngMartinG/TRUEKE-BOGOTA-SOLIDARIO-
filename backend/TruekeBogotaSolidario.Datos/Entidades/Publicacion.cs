using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Foto de una publicación (hasta <see cref="Publicacion.MaxImagenes"/>; la de Orden 0 es la principal).</summary>
public class ImagenPublicacion
{
    private ImagenPublicacion() { Url = ""; } // EF Core
    public ImagenPublicacion(string url, int orden) { Url = url; Orden = orden; }
    public string Url { get; private set; }
    public int Orden { get; private set; }
}

public class Publicacion
{
    public const int MaxImagenes = 5;

    private readonly List<ImagenPublicacion> _imagenes = new();

    private Publicacion() { Titulo = ""; Descripcion = ""; Localidad = ""; } // EF Core

    public Publicacion(Guid propietarioId, string titulo, string descripcion, Categoria categoria, ModoTransaccion modo,
        string localidad, decimal? precioReferenciaCop, double? latitud, double? longitud, IReadOnlyList<string>? imagenes)
    {
        Titulo = ""; Descripcion = ""; Localidad = "";
        Id = Guid.NewGuid();
        PropietarioId = propietarioId;
        Aplicar(titulo, descripcion, categoria, modo, localidad, precioReferenciaCop, latitud, longitud, imagenes);
        Publicar();
    }

    public Guid Id { get; private set; }
    public Guid PropietarioId { get; private set; }
    public Usuario? Propietario { get; private set; }
    public string Titulo { get; private set; }
    public string Descripcion { get; private set; }
    public int CategoriaId { get; private set; }
    public Categoria? Categoria { get; private set; }
    public ModoTransaccion Modo { get; private set; }
    public decimal? PrecioReferenciaCop { get; private set; }
    public string Localidad { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public IReadOnlyList<ImagenPublicacion> Imagenes => _imagenes;
    public EstadoPublicacionEnum Estado { get; private set; }
    public DateTime FechaPublicacion { get; private set; }
    public DateTime? FechaEdicion { get; private set; }
    public string? MotivoCancelacion { get; private set; }

    public DateTime? DestacadaHasta { get; private set; }

    public bool EstaOculta { get; private set; }
    public string? MotivoOcultamiento { get; private set; }

    public byte[]? RowVersion { get; private set; }

    /// <summary>Validación y asignación compartidas por la creación y la edición.</summary>
    private void Aplicar(string titulo, string descripcion, Categoria categoria, ModoTransaccion modo, string localidad,
        decimal? precioReferenciaCop, double? latitud, double? longitud, IReadOnlyList<string>? imagenes)
    {
        titulo = (titulo ?? "").Trim();
        descripcion = (descripcion ?? "").Trim();
        localidad = (localidad ?? "").Trim();
        if (titulo.Length is < 3 or > 120) throw new ReglaDeNegocioException("El título debe tener entre 3 y 120 caracteres.");
        if (descripcion.Length is < 1 or > 2000) throw new ReglaDeNegocioException("La descripción es obligatoria (máximo 2000 caracteres).");
        if (localidad.Length is < 2 or > 60) throw new ReglaDeNegocioException("La localidad debe tener entre 2 y 60 caracteres.");
        if (titulo.Any(char.IsControl) || localidad.Any(char.IsControl)) throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        if (!Enum.IsDefined(modo)) throw new ReglaDeNegocioException("Modo de transacción no válido.");
        if (modo == ModoTransaccion.Compra && (precioReferenciaCop is null or <= 0))
            throw new ReglaDeNegocioException("Una publicación en modo Compra requiere un precio de referencia mayor a cero.");
        if (latitud.HasValue != longitud.HasValue) throw new ReglaDeNegocioException("Latitud y longitud deben enviarse juntas.");
        if (latitud is < -90 or > 90 || longitud is < -180 or > 180) throw new ReglaDeNegocioException("Coordenadas fuera de rango.");

        var fotos = (imagenes ?? Array.Empty<string>()).ToList();
        if (fotos.Count > MaxImagenes) throw new ReglaDeNegocioException($"Puedes subir como máximo {MaxImagenes} fotos.");
        if (fotos.Distinct(StringComparer.Ordinal).Count() != fotos.Count) throw new ReglaDeNegocioException("Hay fotos repetidas.");
        if (fotos.Any(f => !UrlsSeguras.EsValida(f))) throw new ReglaDeNegocioException("Las imágenes deben ser URLs https válidas.");

        Titulo = titulo;
        Descripcion = descripcion;
        CategoriaId = categoria.Id;
        Categoria = categoria;
        Modo = modo;
        Localidad = localidad;
        PrecioReferenciaCop = modo == ModoTransaccion.Compra ? precioReferenciaCop : null; // solo Compra lleva precio
        Latitud = latitud;
        Longitud = longitud;
        _imagenes.Clear();
        _imagenes.AddRange(fotos.Select((url, i) => new ImagenPublicacion(url, i)));
    }

    /// <summary>Solo el dueño y solo mientras está Disponible: lo que ya está en negociación no cambia bajo los pies del interesado.</summary>
    public void Editar(string titulo, string descripcion, Categoria categoria, ModoTransaccion modo, string localidad,
        decimal? precioReferenciaCop, double? latitud, double? longitud, IReadOnlyList<string>? imagenes, DateTime ahoraUtc)
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("Solo se pueden editar publicaciones disponibles (sin solicitudes en curso).");
        if (EstaOculta) throw new ReglaDeNegocioException("No se puede editar una publicación oculta por moderación.");
        Aplicar(titulo, descripcion, categoria, modo, localidad, precioReferenciaCop, latitud, longitud, imagenes);
        FechaEdicion = ahoraUtc;
    }

    // ---------------- Ciclo de vida ----------------
    public void Publicar()
    {
        if (Estado == EstadoPublicacionEnum.Intercambiada) throw new ReglaDeNegocioException("Una publicación intercambiada no puede volver a publicarse.");
        Estado = EstadoPublicacionEnum.Disponible;
        FechaPublicacion = DateTime.UtcNow;
    }

    public void MarcarEnNegociacion()
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("La publicación no está disponible.");
        Estado = EstadoPublicacionEnum.EnNegociacion;
    }

    /// <summary>Se usa cuando una solicitud se rechaza, cancela o no se concreta (NO reinicia FechaPublicacion).</summary>
    public void VolverADisponible()
    {
        if (Estado != EstadoPublicacionEnum.EnNegociacion) throw new ReglaDeNegocioException("La publicación no está en negociación.");
        Estado = EstadoPublicacionEnum.Disponible;
    }

    public void ConfirmarIntercambio()
    {
        if (Estado != EstadoPublicacionEnum.EnNegociacion) throw new ReglaDeNegocioException("Solo se puede confirmar una publicación en negociación.");
        Estado = EstadoPublicacionEnum.Intercambiada;
    }

    public void Cancelar(string motivo)
    {
        if (Estado is EstadoPublicacionEnum.Intercambiada or EstadoPublicacionEnum.Cancelada)
            throw new ReglaDeNegocioException("La publicación ya no se puede cancelar.");
        Estado = EstadoPublicacionEnum.Cancelada;
        MotivoCancelacion = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }

    // ---------------- Moderación ----------------
    public void Ocultar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Debes indicar el motivo del ocultamiento.");
        if (EstaOculta) throw new ReglaDeNegocioException("La publicación ya está oculta.");
        EstaOculta = true;
        MotivoOcultamiento = motivo.Trim();
    }

    public void Mostrar()
    {
        if (!EstaOculta) throw new ReglaDeNegocioException("La publicación no está oculta.");
        EstaOculta = false;
        MotivoOcultamiento = null;
    }

    // ---------------- Destacado (vence a los 7 días) ----------------
    public bool EstaDestacadaVigente(DateTime ahoraUtc) => DestacadaHasta.HasValue && DestacadaHasta.Value > ahoraUtc;

    /// <summary>Valida SIN modificar (se llama antes de cobrar).</summary>
    public void ValidarPuedeDestacarse(DateTime ahoraUtc)
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("Solo se pueden destacar publicaciones disponibles.");
        if (EstaOculta) throw new ReglaDeNegocioException("No se puede destacar una publicación oculta por moderación.");
        if (EstaDestacadaVigente(ahoraUtc)) throw new ReglaDeNegocioException("La publicación ya está destacada.");
    }

    public void MarcarDestacada(DateTime ahoraUtc)
    {
        ValidarPuedeDestacarse(ahoraUtc);
        DestacadaHasta = ahoraUtc.AddDays(PoliticaEcoPuntos.DuracionDestacadoDias);
    }
}
