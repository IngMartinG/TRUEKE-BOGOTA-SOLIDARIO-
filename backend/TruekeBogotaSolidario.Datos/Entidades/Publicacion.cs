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
    decimal? PrecioReferenciaCop, double? Latitud, double? Longitud, IReadOnlyList<string>? Imagenes, bool AceptaTrueke = false,
    decimal? PesoEstimadoKg = null, MaterialObjeto? Material = null);

public class Publicacion
{
    public const int MaxImagenes = 5;
    public const int LongitudMaximaDetalleCondicion = 300;
    public const decimal PesoMinimoKg = 0.05m;
    public const decimal PesoMaximoKg = 500m;

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
    /// <summary>Venta que también acepta trueke: quien la pide puede pagar el precio u ofrecer objetos suyos (solo en Compra).</summary>
    public bool AceptaTrueke { get; private set; }
    /// <summary>Se le pueden ofrecer objetos a cambio: un trueke, o una venta que también acepta trueke.</summary>
    public bool AdmiteTrueke => Modo == ModoTransaccion.Trueke || (Modo == ModoTransaccion.Compra && AceptaTrueke);
    /// <summary>Código DIVIPOLA del municipio (5 dígitos). Bogotá = 11001.</summary>
    public string MunicipioCodigo { get; private set; }
    /// <summary>Redundante con los 2 primeros dígitos del municipio: permite filtrar por departamento con índice.</summary>
    public string DepartamentoCodigo { get; private set; }
    /// <summary>Barrio, localidad o sector dentro del municipio (texto libre).</summary>
    public string Localidad { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    /// <summary>Opcionales: si faltan, el impacto usa el peso típico y el material de su categoría (y lo marca como estimado).</summary>
    public decimal? PesoEstimadoKg { get; private set; }
    public MaterialObjeto? Material { get; private set; }
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

    /// <summary>Las de riesgo esperan a un moderador antes de entrar al catálogo; las demás se publican al instante.</summary>
    public EstadoModeracion EstadoModeracion { get; private set; } = EstadoModeracion.Aprobada;
    /// <summary>Donación publicada para una necesidad de una fundación ("Tengo esto"): al entregársela suma a su progreso.</summary>
    public Guid? NecesidadFundacionId { get; private set; }

    public void DestinarANecesidad(Guid necesidadId)
    {
        if (Modo != ModoTransaccion.Donacion) throw new ReglaDeNegocioException("Solo una donación puede ir para la necesidad de una fundación.");
        NecesidadFundacionId = necesidadId;
    }
    /// <summary>Suma de las señales que la mandaron a revisión (más alto = se revisa primero).</summary>
    public int PuntajeRiesgo { get; private set; }
    /// <summary>Señales que vio el evaluador, separadas por "; " (solo las ven los moderadores).</summary>
    public string? MotivosRiesgo { get; private set; }
    /// <summary>Lo que el dueño debe corregir: al editarla vuelve a revisión.</summary>
    public string? MotivoRechazo { get; private set; }
    public DateTime? EnRevisionDesdeUtc { get; private set; }

    /// <summary>La pueden ver los demás: no está oculta ni esperando (o rechazada por) un moderador.</summary>
    public bool EsVisible => !EstaOculta && EstadoModeracion == EstadoModeracion.Aprobada;

    /// <summary>Título normalizado (ver <see cref="Common.TextoBusqueda"/>): pesa más en el orden de los resultados.</summary>
    public string TituloBusqueda { get; private set; } = "";
    /// <summary>Título, descripción, estado y categoría normalizados: es donde se buscan los términos.</summary>
    public string TextoBusqueda { get; private set; } = "";

    public uint RowVersion { get; private set; }

    /// <summary>Recalcula el texto de búsqueda (requiere la categoría cargada). Lo usan Aplicar y el relleno inicial.</summary>
    public void ActualizarTextoBusqueda()
    {
        TituloBusqueda = Common.TextoBusqueda.Normalizar(Titulo);
        TextoBusqueda = Common.TextoBusqueda.Normalizar(Titulo, Descripcion, DetalleCondicion, Categoria?.NombreCategoria);
    }

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
        if (!TextoSeguro.EsSeguro(titulo) || !TextoSeguro.EsSeguro(localidad) || !TextoSeguro.EsSeguro(descripcion, multilinea: true)) throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        if (!Enum.IsDefined(d.Modo)) throw new ReglaDeNegocioException("Modo de transacción no válido.");
        if (!Enum.IsDefined(d.Condicion)) throw new ReglaDeNegocioException("Indica el estado del producto (nuevo, usado, reparado…).");
        if (CondicionRequiereDetalle(d.Condicion) && (detalle is null || detalle.Length < 5))
            throw new ReglaDeNegocioException("Describe el estado del producto: qué detalles tiene, qué se reparó o qué piezas sirven (mínimo 5 caracteres).");
        if (detalle is { Length: > LongitudMaximaDetalleCondicion })
            throw new ReglaDeNegocioException($"La descripción del estado admite como máximo {LongitudMaximaDetalleCondicion} caracteres.");
        if (detalle is not null && !TextoSeguro.EsSeguro(detalle, multilinea: true))
            throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        var municipio = Divipola.Exigir(d.MunicipioCodigo);
        if (d.Modo == ModoTransaccion.Compra && (d.PrecioReferenciaCop is null or <= 0))
            throw new ReglaDeNegocioException("Una publicación en modo Compra y venta requiere un precio de referencia mayor a cero.");
        if (d.Latitud.HasValue != d.Longitud.HasValue) throw new ReglaDeNegocioException("Latitud y longitud deben enviarse juntas.");
        if (d.Latitud is < -90 or > 90 || d.Longitud is < -180 or > 180) throw new ReglaDeNegocioException("Coordenadas fuera de rango.");
        if (d.PesoEstimadoKg is { } peso && (peso < PesoMinimoKg || peso > PesoMaximoKg))
            throw new ReglaDeNegocioException($"El peso aproximado debe estar entre {PesoMinimoKg} y {PesoMaximoKg} kg.");
        if (d.Material is { } material && !Enum.IsDefined(material)) throw new ReglaDeNegocioException("Material no válido.");

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
        AceptaTrueke = d.Modo == ModoTransaccion.Compra && d.AceptaTrueke;
        Latitud = d.Latitud;
        Longitud = d.Longitud;
        PesoEstimadoKg = d.PesoEstimadoKg is { } p ? Math.Round(p, 2) : null;
        Material = d.Material;
        _imagenes.Clear();
        _imagenes.AddRange(fotos.Select((url, i) => new ImagenPublicacion(url, i)));
        ActualizarTextoBusqueda();
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
        ExigirAprobada("impulsar");
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

    // ---------------- Revisión previa (solo las de riesgo) ----------------
    public const int LongitudMaximaMotivosRiesgo = 500;
    public const int LongitudMaximaMotivoRechazo = 300;

    /// <summary>
    /// Sale del catálogo hasta que un moderador la apruebe. Si ya esperaba, conserva su lugar en la fila y suma las
    /// señales nuevas (una edición no puede "limpiar" lo que la mandó a revisión).
    /// </summary>
    public void EnviarARevision(int puntaje, IEnumerable<string> motivos, DateTime ahoraUtc)
    {
        var yaEsperaba = EstadoModeracion == EstadoModeracion.Pendiente;
        var todos = (yaEsperaba && MotivosRiesgo is not null ? MotivosRiesgo.Split("; ") : [])
            .Concat(motivos).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.Ordinal);
        var texto = string.Join("; ", todos);
        EstadoModeracion = EstadoModeracion.Pendiente;
        PuntajeRiesgo = yaEsperaba ? Math.Max(PuntajeRiesgo, puntaje) : puntaje;
        MotivosRiesgo = texto.Length == 0 ? null : texto.Length <= LongitudMaximaMotivosRiesgo ? texto : texto[..LongitudMaximaMotivosRiesgo];
        MotivoRechazo = null;
        if (!yaEsperaba) EnRevisionDesdeUtc = ahoraUtc;
    }

    /// <summary>Entra al catálogo como recién publicada: la espera no la deja enterrada bajo las de ese rato.</summary>
    public void AprobarModeracion(DateTime ahoraUtc)
    {
        if (EstadoModeracion != EstadoModeracion.Pendiente) throw new ReglaDeNegocioException("La publicación no está esperando revisión.");
        EstadoModeracion = EstadoModeracion.Aprobada;
        MotivoRechazo = null;
        EnRevisionDesdeUtc = null;
        if (ahoraUtc > FechaRelevancia) FechaRelevancia = ahoraUtc;
    }

    public void RechazarModeracion(string motivo)
    {
        if (EstadoModeracion != EstadoModeracion.Pendiente) throw new ReglaDeNegocioException("La publicación no está esperando revisión.");
        var texto = (motivo ?? "").Trim();
        if (texto.Length is < 3 or > LongitudMaximaMotivoRechazo)
            throw new ReglaDeNegocioException($"Escribe el motivo del rechazo (entre 3 y {LongitudMaximaMotivoRechazo} caracteres).");
        EstadoModeracion = EstadoModeracion.Rechazada;
        MotivoRechazo = texto;
        EnRevisionDesdeUtc = null;
    }

    private void ExigirAprobada(string accion)
    {
        if (EstadoModeracion != EstadoModeracion.Aprobada)
            throw new ReglaDeNegocioException($"No se puede {accion} una publicación que aún no está en el catálogo (espera la revisión).");
    }

    // ---------------- Destacado (vence a los 7 días) ----------------
    public bool EstaDestacadaVigente(DateTime ahoraUtc) => DestacadaHasta.HasValue && DestacadaHasta.Value > ahoraUtc;

    /// <summary>Valida SIN modificar (se llama antes de cobrar).</summary>
    public void ValidarPuedeDestacarse(DateTime ahoraUtc)
    {
        if (Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("Solo se pueden destacar publicaciones disponibles.");
        if (EstaOculta) throw new ReglaDeNegocioException("No se puede destacar una publicación oculta por moderación.");
        ExigirAprobada("destacar");
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
