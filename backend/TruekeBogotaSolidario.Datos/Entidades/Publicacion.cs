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

/// <summary>Datos que el dueño define al crear o editar una publicación.</summary>
public sealed record DatosPublicacion(string Titulo, string Descripcion, Categoria Categoria, ModoTransaccion Modo,
    CondicionProducto Condicion, string? DetalleCondicion, string MunicipioCodigo, string Localidad,
    decimal? PrecioReferenciaCop, double? Latitud, double? Longitud, IReadOnlyList<string>? Imagenes);

public class Publicacion
{
    public const int MaxImagenes = 5;
    public const int LongitudMaximaDetalleCondicion = 300;

    private readonly List<ImagenPublicacion> _imagenes = new();

    private Publicacion() { Titulo = ""; Descripcion = ""; Localidad = ""; MunicipioCodigo = Divipola.CodigoBogota; DepartamentoCodigo = "11"; } // EF Core

    public Publicacion(Guid propietarioId, DatosPublicacion datos)
    {
        Titulo = ""; Descripcion = ""; Localidad = ""; MunicipioCodigo = ""; DepartamentoCodigo = "";
        Id = Guid.NewGuid();
        PropietarioId = propietarioId;
        Aplicar(datos);
        Publicar();
    }

    /// <summary>Exigen describir el estado: el interesado debe saber qué detalle tiene o qué se reparó.</summary>
    public static bool CondicionRequiereDetalle(CondicionProducto c)
        => c is CondicionProducto.UsadoConDetalles or CondicionProducto.Reparado or CondicionProducto.ParaRepuestos;

    public Guid Id { get; private set; }
    public Guid PropietarioId { get; private set; }
    public Usuario? Propietario { get; private set; }
    public string Titulo { get; private set; }
    public string Descripcion { get; private set; }
    public int CategoriaId { get; private set; }
    public Categoria? Categoria { get; private set; }
    public ModoTransaccion Modo { get; private set; }
    public CondicionProducto Condicion { get; private set; }
    public string? DetalleCondicion { get; private set; }
    public decimal? PrecioReferenciaCop { get; private set; }
    /// <summary>Código DIVIPOLA del municipio (5 dígitos). Bogotá = 11001.</summary>
    public string MunicipioCodigo { get; private set; }
    /// <summary>Redundante con los 2 primeros dígitos del municipio: permite filtrar por departamento con índice.</summary>
    public string DepartamentoCodigo { get; private set; }
    /// <summary>Barrio, localidad o sector dentro del municipio (texto libre).</summary>
    public string Localidad { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public IReadOnlyList<ImagenPublicacion> Imagenes => _imagenes;
    public EstadoPublicacionEnum Estado { get; private set; }
    public DateTime FechaPublicacion { get; private set; }
    public DateTime? FechaEdicion { get; private set; }
    public string? MotivoCancelacion { get; private set; }

    public DateTime? DestacadaHasta { get; private set; }

    /// <summary>Última vez que el dueño la impulsó con Eco-Puntos.</summary>
    public DateTime? FechaImpulso { get; private set; }
    /// <summary>Orden de "Más recientes": la fecha de publicación o la del último impulso.</summary>
    public DateTime FechaRelevancia { get; private set; }

    public bool EstaOculta { get; private set; }
    public string? MotivoOcultamiento { get; private set; }

    public byte[]? RowVersion { get; private set; }

    /// <summary>Validación y asignación compartidas por la creación y la edición.</summary>
    private void Aplicar(DatosPublicacion d)
    {
        var titulo = (d.Titulo ?? "").Trim();
        var descripcion = (d.Descripcion ?? "").Trim();
        var localidad = (d.Localidad ?? "").Trim();
        var detalle = string.IsNullOrWhiteSpace(d.DetalleCondicion) ? null : d.DetalleCondicion.Trim();
        if (titulo.Length is < 3 or > 120) throw new ReglaDeNegocioException("El título debe tener entre 3 y 120 caracteres.");
        if (descripcion.Length is < 1 or > 2000) throw new ReglaDeNegocioException("La descripción es obligatoria (máximo 2000 caracteres).");
        if (localidad.Length is < 2 or > 60) throw new ReglaDeNegocioException("La localidad, barrio o sector debe tener entre 2 y 60 caracteres.");
        if (titulo.Any(char.IsControl) || localidad.Any(char.IsControl)) throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        if (!Enum.IsDefined(d.Modo)) throw new ReglaDeNegocioException("Modo de transacción no válido.");
        if (!Enum.IsDefined(d.Condicion)) throw new ReglaDeNegocioException("Indica el estado del producto (nuevo, usado, reparado…).");
        if (CondicionRequiereDetalle(d.Condicion) && (detalle is null || detalle.Length < 5))
            throw new ReglaDeNegocioException("Describe el estado del producto: qué detalles tiene, qué se reparó o qué piezas sirven (mínimo 5 caracteres).");
        if (detalle is { Length: > LongitudMaximaDetalleCondicion })
            throw new ReglaDeNegocioException($"La descripción del estado admite como máximo {LongitudMaximaDetalleCondicion} caracteres.");
        if (detalle is not null && detalle.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        var municipio = Divipola.Exigir(d.MunicipioCodigo);
        if (d.Modo == ModoTransaccion.Compra && (d.PrecioReferenciaCop is null or <= 0))
            throw new ReglaDeNegocioException("Una publicación en modo Compra requiere un precio de referencia mayor a cero.");
        if (d.Latitud.HasValue != d.Longitud.HasValue) throw new ReglaDeNegocioException("Latitud y longitud deben enviarse juntas.");
        if (d.Latitud is < -90 or > 90 || d.Longitud is < -180 or > 180) throw new ReglaDeNegocioException("Coordenadas fuera de rango.");

        var fotos = (d.Imagenes ?? Array.Empty<string>()).ToList();
        if (fotos.Count > MaxImagenes) throw new ReglaDeNegocioException($"Puedes subir como máximo {MaxImagenes} fotos.");
        if (fotos.Distinct(StringComparer.Ordinal).Count() != fotos.Count) throw new ReglaDeNegocioException("Hay fotos repetidas.");
        if (fotos.Any(f => !UrlsSeguras.EsValida(f))) throw new ReglaDeNegocioException("Las imágenes deben ser URLs https válidas.");

        Titulo = titulo;
        Descripcion = descripcion;
        CategoriaId = d.Categoria.Id;
        Categoria = d.Categoria;
        Modo = d.Modo;
        Condicion = d.Condicion;
        DetalleCondicion = detalle;
        MunicipioCodigo = municipio.Codigo;
        DepartamentoCodigo = municipio.DepartamentoCodigo;
        Localidad = localidad;
        PrecioReferenciaCop = d.Modo == ModoTransaccion.Compra ? d.PrecioReferenciaCop : null; // solo Compra lleva precio
        Latitud = d.Latitud;
        Longitud = d.Longitud;
        _imagenes.Clear();
        _imagenes.AddRange(fotos.Select((url, i) => new ImagenPublicacion(url, i)));
    }

    /// <summary>Solo el dueño y solo mientras está Disponible: lo que ya está en negociación no cambia bajo los pies del interesado.</summary>
    public void Editar(DatosPublicacion datos, DateTime ahoraUtc)
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("Solo se pueden editar publicaciones disponibles (sin solicitudes en curso).");
        if (EstaOculta) throw new ReglaDeNegocioException("No se puede editar una publicación oculta por moderación.");
        Aplicar(datos);
        FechaEdicion = ahoraUtc;
    }

    // ---------------- Ciclo de vida ----------------
    public void Publicar()
    {
        if (Estado == EstadoPublicacionEnum.Intercambiada) throw new ReglaDeNegocioException("Una publicación intercambiada no puede volver a publicarse.");
        Estado = EstadoPublicacionEnum.Disponible;
        FechaPublicacion = DateTime.UtcNow;
        FechaRelevancia = FechaPublicacion;
    }

    // ---------------- Impulso (se paga con Eco-Puntos) ----------------
    public DateTime? ProximoImpulsoPosible => FechaImpulso?.AddHours(PoliticaEcoPuntos.HorasEntreImpulsos);

    /// <summary>Valida SIN modificar (se llama antes de debitar los puntos).</summary>
    public void ValidarPuedeImpulsarse(DateTime ahoraUtc)
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("Solo se pueden impulsar publicaciones disponibles.");
        if (EstaOculta) throw new ReglaDeNegocioException("No se puede impulsar una publicación oculta por moderación.");
        if (ProximoImpulsoPosible is { } proximo && proximo > ahoraUtc)
            throw new ReglaDeNegocioException($"Ya la impulsaste. Podrás volver a hacerlo cada {PoliticaEcoPuntos.HorasEntreImpulsos} horas.");
    }

    public void Impulsar(DateTime ahoraUtc)
    {
        ValidarPuedeImpulsarse(ahoraUtc);
        FechaImpulso = ahoraUtc;
        FechaRelevancia = ahoraUtc;
    }

    /// <summary>El dueño eligió a una de las personas interesadas: queda reservada y sale del catálogo.</summary>
    public void MarcarEnNegociacion()
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("La publicación no está disponible.");
        Estado = EstadoPublicacionEnum.EnNegociacion;
    }

    /// <summary>Se usa cuando el intercambio aceptado no se concreta: vuelve al catálogo (NO reinicia FechaPublicacion).</summary>
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

    /// <summary>Al reembolsar un destacado se retira el beneficio.</summary>
    public void QuitarDestacado(DateTime ahoraUtc)
    {
        if (EstaDestacadaVigente(ahoraUtc)) DestacadaHasta = ahoraUtc;
    }
}
