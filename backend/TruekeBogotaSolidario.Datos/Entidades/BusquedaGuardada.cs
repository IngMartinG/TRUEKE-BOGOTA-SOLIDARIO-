using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Repositorios;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Búsqueda del catálogo que la persona guardó. Con alertas activas, una tarea periódica le avisa cuando aparecen
/// publicaciones NUEVAS que coinciden (desde <see cref="RevisadaHastaUtc"/>), como mucho una vez por revisión.
/// </summary>
public class BusquedaGuardada
{
    public const int MaxPorUsuario = 10;

    private BusquedaGuardada() { } // EF Core

    public BusquedaGuardada(Guid usuarioId, string? texto, int? categoriaId, ModoTransaccion? modo, CondicionProducto? condicion,
        string? departamentoCodigo, string? municipioCodigo, string? localidad, decimal? precioMin, decimal? precioMax,
        bool soloVerificados, bool alertas, DateTime ahoraUtc)
    {
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (texto is { Length: > 100 }) throw new ReglaDeNegocioException("El texto de búsqueda admite máximo 100 caracteres.");
        if (precioMin.HasValue && precioMax.HasValue && precioMin > precioMax)
            throw new ReglaDeNegocioException("El precio mínimo no puede ser mayor que el máximo.");
        if (texto is null && categoriaId is null && modo is null && condicion is null && departamentoCodigo is null && municipioCodigo is null
            && string.IsNullOrWhiteSpace(localidad) && precioMin is null && precioMax is null && !soloVerificados)
            throw new ReglaDeNegocioException("Escribe algo o elige al menos un filtro para guardar la búsqueda.");

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Texto = texto;
        CategoriaId = categoriaId;
        Modo = modo;
        Condicion = condicion;
        DepartamentoCodigo = string.IsNullOrWhiteSpace(departamentoCodigo) ? null : departamentoCodigo.Trim();
        MunicipioCodigo = string.IsNullOrWhiteSpace(municipioCodigo) ? null : municipioCodigo.Trim();
        Localidad = string.IsNullOrWhiteSpace(localidad) ? null : localidad.Trim();
        PrecioMin = precioMin;
        PrecioMax = precioMax;
        SoloVerificados = soloVerificados;
        AlertasActivas = alertas;
        FechaUtc = ahoraUtc;
        RevisadaHastaUtc = ahoraUtc; // solo avisa de lo que se publique DESPUÉS de guardarla
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string? Texto { get; private set; }
    public int? CategoriaId { get; private set; }
    public ModoTransaccion? Modo { get; private set; }
    public CondicionProducto? Condicion { get; private set; }
    public string? DepartamentoCodigo { get; private set; }
    public string? MunicipioCodigo { get; private set; }
    public string? Localidad { get; private set; }
    public decimal? PrecioMin { get; private set; }
    public decimal? PrecioMax { get; private set; }
    public bool SoloVerificados { get; private set; }
    public bool AlertasActivas { get; private set; }
    public DateTime FechaUtc { get; private set; }
    /// <summary>Hasta cuándo ya se revisaron publicaciones nuevas (o se avisó).</summary>
    public DateTime RevisadaHastaUtc { get; private set; }

    public void CambiarAlertas(bool activas, DateTime ahoraUtc)
    {
        // Al reactivarlas no se avisa de lo publicado mientras estuvieron apagadas.
        if (activas && !AlertasActivas) RevisadaHastaUtc = ahoraUtc;
        AlertasActivas = activas;
    }

    public void MarcarRevisada(DateTime hastaUtc) => RevisadaHastaUtc = hastaUtc;

    /// <summary>Mismos filtros que el catálogo.</summary>
    public FiltroPublicaciones AFiltro() => new()
    {
        Texto = Texto, CategoriaId = CategoriaId, Modo = Modo, Condicion = Condicion, DepartamentoCodigo = DepartamentoCodigo,
        MunicipioCodigo = MunicipioCodigo, Localidad = Localidad, PrecioMin = PrecioMin, PrecioMax = PrecioMax, SoloVerificados = SoloVerificados
    };

    /// <summary>Misma búsqueda (para no guardarla dos veces).</summary>
    public bool EquivaleA(BusquedaGuardada o)
        => string.Equals(TextoBusqueda.Normalizar(Texto), TextoBusqueda.Normalizar(o.Texto), StringComparison.Ordinal)
           && CategoriaId == o.CategoriaId && Modo == o.Modo && Condicion == o.Condicion && DepartamentoCodigo == o.DepartamentoCodigo
           && MunicipioCodigo == o.MunicipioCodigo && Localidad == o.Localidad && PrecioMin == o.PrecioMin && PrecioMax == o.PrecioMax
           && SoloVerificados == o.SoloVerificados;
}
