using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Repositorios;

public class FiltroPublicaciones
{
    public int? CategoriaId { get; set; }
    public ModoTransaccion? Modo { get; set; }
    public string? Localidad { get; set; }
    public string? Texto { get; set; }
    public int Pagina { get; set; } = 1;
    public int Tamano { get; set; } = 20;
}

/// <summary>
/// Unidad de trabajo: los repositorios solo agregan/consultan; TODOS los cambios de una operación
/// de negocio se confirman juntos en una única transacción (todo o nada).
/// Lanza ConflictoDeConcurrenciaException si otro proceso modificó los mismos registros.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync();
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorIdAsync(Guid id);
    Task<Usuario?> ObtenerPorCorreoAsync(string correoNormalizado);
    Task<Usuario?> ObtenerPorGoogleSubAsync(string googleSub);
    Task<bool> ExisteCorreoAsync(string correoNormalizado);
    Task<IReadOnlyList<Usuario>> ObtenerPorVerificacionAsync(EstadoVerificacion estado);
    Task<int> ContarPorRolAsync(RolUsuarioEnum rol);
    /// <summary>Consulta ligera (sin tracking) para validar sesiones JWT. Null si el usuario no existe.</summary>
    Task<int?> ObtenerVersionSeguridadAsync(Guid id);
    void Agregar(Usuario usuario);
}

public interface ICategoriaRepository
{
    Task<Categoria?> ObtenerPorIdAsync(int id);
    Task<IReadOnlyList<Categoria>> ListarAsync();
}

public interface IPublicacionRepository
{
    /// <summary>Incluye Categoria y Propietario.</summary>
    Task<Publicacion?> ObtenerPorIdAsync(Guid id);
    /// <summary>Solo Disponibles y NO ocultas; destacadas vigentes primero.</summary>
    Task<(IReadOnlyList<Publicacion> Items, int Total)> ListarVisiblesAsync(FiltroPublicaciones filtro, DateTime ahoraUtc);
    /// <summary>Visibles (Disponibles, no ocultas) con coordenadas dentro del rectángulo indicado. Máximo <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<Publicacion>> ListarVisiblesEnAreaAsync(double minLat, double maxLat, double minLon, double maxLon,
        int? categoriaId, ModoTransaccion? modo, int maximo);
    Task<IReadOnlyList<Publicacion>> ListarPorPropietarioAsync(Guid propietarioId);
    Task<int> ContarActivasPorUsuarioAsync(Guid usuarioId);
    void Agregar(Publicacion publicacion);
}

public interface ISolicitudRepository
{
    /// <summary>Incluye Publicacion y Solicitante.</summary>
    Task<Solicitud?> ObtenerPorIdAsync(Guid id);
    Task<IReadOnlyList<Solicitud>> ListarPorSolicitanteAsync(Guid solicitanteId);
    Task<IReadOnlyList<Solicitud>> ListarRecibidasAsync(Guid propietarioId);
    Task<Solicitud?> ObtenerPendientePorPublicacionAsync(Guid publicacionId);
    Task<int> ContarPendientesPorSolicitanteAsync(Guid solicitanteId);
    Task<bool> ExisteAceptadaAsync(Guid publicacionId, Guid solicitanteId);
    void Agregar(Solicitud solicitud);
}

public interface ITransaccionRepository
{
    /// <summary>Transacciones desde <paramref name="desdeUtc"/> en las que ESTE usuario recibió puntos (tope anti-farmeo).</summary>
    Task<int> ContarDesdeAsync(Guid usuarioId, DateTime desdeUtc);
    Task<IReadOnlyList<Transaccion>> ListarPorUsuarioAsync(Guid usuarioId);
    void Agregar(Transaccion transaccion);
}

public interface IPagoRepository
{
    Task<Pago?> ObtenerPorReferenciaAsync(string referencia);
    Task<Pago?> ObtenerUltimoAprobadoAsync(Guid usuarioId, ConceptoPago concepto);
    Task<int> ContarPendientesAsync(Guid usuarioId, DateTime desdeUtc);
    /// <summary>Evita pagar dos veces lo mismo (mismo concepto y, si aplica, misma publicación).</summary>
    Task<bool> ExistePendienteAsync(Guid usuarioId, ConceptoPago concepto, Guid? publicacionId);
    Task<IReadOnlyList<Pago>> ListarPendientesAnterioresAAsync(DateTime limiteUtc, int maximo);
    void Agregar(Pago pago);
}

/// <summary>Usado por el health check de readiness.</summary>
public interface ISaludBaseDatos
{
    Task<bool> PuedeConectarAsync(CancellationToken ct);
}

public interface IAuditoriaRepository
{
    void Agregar(AuditoriaEvento evento);
}

public interface ISesionRefreshRepository
{
    Task<SesionRefresh?> ObtenerPorHashAsync(string tokenHash);
    /// <summary>Revoca (sin guardar) todos los tokens vigentes de la familia.</summary>
    Task RevocarFamiliaAsync(Guid familiaId, DateTime ahoraUtc);
    /// <summary>Revoca (sin guardar) todos los tokens vigentes del usuario: cierre de sesión global.</summary>
    Task RevocarTodasDelUsuarioAsync(Guid usuarioId, DateTime ahoraUtc);
    /// <summary>Elimina (y guarda) hasta <paramref name="maximo"/> tokens vencidos o revocados antes de la fecha. Devuelve cuántos.</summary>
    Task<int> PurgarAsync(DateTime antesDeUtc, int maximo);
    void Agregar(SesionRefresh sesion);
}

public interface ITokenUsoUnicoRepository
{
    Task<TokenUsoUnico?> ObtenerPorHashAsync(string tokenHash, PropositoToken proposito);
    /// <summary>Consume (sin guardar) los tokens vigentes de ese propósito: solo el último enlace enviado sirve.</summary>
    Task InvalidarVigentesAsync(Guid usuarioId, PropositoToken proposito, DateTime ahoraUtc);
    Task<int> ContarDesdeAsync(Guid usuarioId, PropositoToken proposito, DateTime desdeUtc);
    Task<int> PurgarAsync(DateTime antesDeUtc, int maximo);
    void Agregar(TokenUsoUnico token);
}

public interface IComentarioRepository
{
    Task<Comentario?> ObtenerPorIdAsync(Guid id);
    /// <summary>Incluye Autor. Más recientes primero. Si <paramref name="incluirOcultos"/> es false, los ocultos no se devuelven.</summary>
    Task<(IReadOnlyList<Comentario> Items, int Total)> ListarPorPublicacionAsync(Guid publicacionId, bool incluirOcultos, int pagina, int tamano);
    Task<int> ContarDelAutorDesdeAsync(Guid autorId, DateTime desdeUtc);
    void Agregar(Comentario comentario);
}
