using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Repositorios;

public enum OrdenPublicaciones { Recientes = 1, PrecioAsc = 2, PrecioDesc = 3 }

public class FiltroPublicaciones
{
    public int? CategoriaId { get; set; }
    public ModoTransaccion? Modo { get; set; }
    public string? Localidad { get; set; }
    public string? Texto { get; set; }
    public decimal? PrecioMin { get; set; }
    public decimal? PrecioMax { get; set; }
    public bool SoloVerificados { get; set; }
    public OrdenPublicaciones Orden { get; set; } = OrdenPublicaciones.Recientes;
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
    /// <summary>Búsqueda para administración por nombre o correo (contiene). Más recientes primero.</summary>
    Task<(IReadOnlyList<Usuario> Items, int Total)> BuscarAsync(string? texto, bool soloSuspendidos, int pagina, int tamano);
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
    /// <summary>Visibles = Disponibles, no ocultas y de dueños no suspendidos. Destacadas vigentes primero (orden Recientes).</summary>
    Task<(IReadOnlyList<Publicacion> Items, int Total)> ListarVisiblesAsync(FiltroPublicaciones filtro, DateTime ahoraUtc);
    /// <summary>Visibles con coordenadas dentro del rectángulo indicado. Máximo <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<Publicacion>> ListarVisiblesEnAreaAsync(double minLat, double maxLat, double minLon, double maxLon,
        int? categoriaId, ModoTransaccion? modo, int maximo, DateTime ahoraUtc);
    /// <summary>Visibles de un propietario (su perfil público).</summary>
    Task<(IReadOnlyList<Publicacion> Items, int Total)> ListarVisiblesDePropietarioAsync(Guid propietarioId, int pagina, int tamano, DateTime ahoraUtc);
    /// <summary>Visibles, en el orden de <paramref name="ids"/> no garantizado (favoritos).</summary>
    Task<IReadOnlyList<Publicacion>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids);
    Task<IReadOnlyList<Publicacion>> ListarPorPropietarioAsync(Guid propietarioId);
    Task<int> ContarActivasPorUsuarioAsync(Guid usuarioId);
    /// <summary>Disponibles o en negociación del usuario, CON tracking (para cancelarlas).</summary>
    Task<IReadOnlyList<Publicacion>> ListarActivasParaActualizarAsync(Guid propietarioId);
    void Agregar(Publicacion publicacion);
}

public interface ISolicitudRepository
{
    /// <summary>Incluye Publicacion y Solicitante.</summary>
    Task<Solicitud?> ObtenerPorIdAsync(Guid id);
    Task<IReadOnlyList<Solicitud>> ListarPorSolicitanteAsync(Guid solicitanteId);
    Task<IReadOnlyList<Solicitud>> ListarRecibidasAsync(Guid propietarioId);
    Task<Solicitud?> ObtenerPendientePorPublicacionAsync(Guid publicacionId);
    /// <summary>La solicitud Pendiente o Aceptada de la publicación (con tracking), si existe.</summary>
    Task<Solicitud?> ObtenerEnCursoPorPublicacionAsync(Guid publicacionId);
    Task<int> ContarPendientesPorSolicitanteAsync(Guid solicitanteId);
    /// <summary>Aceptada o Completada: el solicitante puede ver las coordenadas exactas.</summary>
    Task<bool> ExisteAceptadaAsync(Guid publicacionId, Guid solicitanteId);
    /// <summary>
    /// Aceptadas que deben cerrarse solas: con UNA confirmación y aceptadas antes de <paramref name="limiteConConfirmacion"/>
    /// (se completan), o SIN confirmaciones y aceptadas antes de <paramref name="limiteSinConfirmacion"/> (no concretadas).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListarParaCierreAutomaticoAsync(DateTime limiteConConfirmacion, DateTime limiteSinConfirmacion, int maximo);
    /// <summary>En curso (Pendientes o Aceptadas) enviadas o recibidas por el usuario, CON tracking e incluyendo la Publicacion.</summary>
    Task<IReadOnlyList<Solicitud>> ListarPendientesDelUsuarioAsync(Guid usuarioId);
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
    Task<IReadOnlyList<Pago>> ListarPorUsuarioAsync(Guid usuarioId, int maximo);
    /// <summary>Para administración (p. ej. RequiereRevision). Más antiguos primero.</summary>
    Task<(IReadOnlyList<Pago> Items, int Total)> ListarPorEstadoAsync(EstadoPago estado, int pagina, int tamano);
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

public interface INotificacionRepository
{
    Task<Notificacion?> ObtenerAsync(Guid id, Guid usuarioId);
    Task<(IReadOnlyList<Notificacion> Items, int Total)> ListarAsync(Guid usuarioId, bool soloNoLeidas, int pagina, int tamano);
    Task<IReadOnlyList<Notificacion>> ListarTodasAsync(Guid usuarioId, int maximo);
    Task<int> ContarNoLeidasAsync(Guid usuarioId);
    /// <summary>Marca (sin guardar) hasta 1000 notificaciones no leídas.</summary>
    Task MarcarTodasLeidasAsync(Guid usuarioId, DateTime ahoraUtc);
    Task<int> PurgarLeidasAsync(DateTime leidasAntesDeUtc, int maximo);
    void Agregar(Notificacion notificacion);
}

public interface IConversacionRepository
{
    /// <summary>Incluye Solicitud (con Publicacion), Duenio y Solicitante.</summary>
    Task<Conversacion?> ObtenerAsync(Guid id);
    Task<IReadOnlyDictionary<Guid, Guid>> IdsPorSolicitudAsync(IReadOnlyCollection<Guid> solicitudIds);
    /// <summary>Del usuario (como dueño o solicitante), más recientes primero. Incluye Solicitud/Publicacion y participantes.</summary>
    Task<IReadOnlyList<Conversacion>> ListarDeUsuarioAsync(Guid usuarioId, int maximo);
    /// <summary>Mensajes NO leídos que otros enviaron al usuario, por conversación.</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarNoLeidosAsync(Guid usuarioId, IReadOnlyCollection<Guid> conversacionIds);
    Task<IReadOnlyDictionary<Guid, Mensaje>> UltimosMensajesAsync(IReadOnlyCollection<Guid> conversacionIds);
    /// <summary>Más recientes primero, anteriores a <paramref name="antesDeUtc"/> si se indica.</summary>
    Task<IReadOnlyList<Mensaje>> ListarMensajesAsync(Guid conversacionId, DateTime? antesDeUtc, int tamano);
    /// <summary>Marca (sin guardar) como leídos los mensajes que recibió <paramref name="lectorId"/>.</summary>
    Task MarcarLeidosAsync(Guid conversacionId, Guid lectorId, DateTime ahoraUtc);
    /// <summary>Incluye la Conversacion.</summary>
    Task<Mensaje?> ObtenerMensajeAsync(Guid id);
    Task<int> ContarMensajesDelAutorDesdeAsync(Guid autorId, DateTime desdeUtc);
    Task<IReadOnlyList<Mensaje>> ListarMensajesDelAutorAsync(Guid autorId, int maximo);
    void Agregar(Conversacion conversacion);
    void AgregarMensaje(Mensaje mensaje);
}

public interface IDenunciaRepository
{
    Task<Denuncia?> ObtenerAsync(Guid id);
    Task<bool> ExisteAsync(Guid denuncianteId, TipoObjetoDenuncia tipo, Guid objetivoId);
    Task<int> ContarDelDenuncianteDesdeAsync(Guid denuncianteId, DateTime desdeUtc);
    /// <summary>Más antiguas primero (cola de moderación).</summary>
    Task<IReadOnlyList<Denuncia>> ListarPorEstadoAsync(EstadoDenuncia estado, int maximo);
    /// <summary>Pendientes sobre el mismo objetivo (con tracking): se resuelven juntas.</summary>
    Task<IReadOnlyList<Denuncia>> PendientesDelObjetivoAsync(TipoObjetoDenuncia tipo, Guid objetivoId);
    Task<IReadOnlyList<Denuncia>> ListarDelDenuncianteAsync(Guid denuncianteId, int maximo);
    void Agregar(Denuncia denuncia);
}

public interface IFavoritoRepository
{
    Task<Favorito?> ObtenerAsync(Guid usuarioId, Guid publicacionId);
    /// <summary>De la lista dada, cuáles son favoritas del usuario (para marcar el catálogo).</summary>
    Task<IReadOnlySet<Guid>> FiltrarFavoritasAsync(Guid usuarioId, IReadOnlyCollection<Guid> publicacionIds);
    /// <summary>Ids de las favoritas, más recientes primero.</summary>
    Task<(IReadOnlyList<Guid> Ids, int Total)> ListarAsync(Guid usuarioId, int pagina, int tamano);
    Task<int> ContarAsync(Guid usuarioId);
    void Agregar(Favorito favorito);
    void Quitar(Favorito favorito);
}

public interface ICalificacionRepository
{
    Task<Calificacion?> ObtenerAsync(Guid id);
    Task<bool> ExisteAsync(Guid solicitudId, Guid autorId);
    /// <summary>De las solicitudes dadas, cuáles ya calificó el autor.</summary>
    Task<IReadOnlySet<Guid>> SolicitudesCalificadasPorAsync(Guid autorId, IReadOnlyCollection<Guid> solicitudIds);
    /// <summary>Recibidas por el usuario, más recientes primero; incluye Autor.</summary>
    Task<(IReadOnlyList<Calificacion> Items, int Total)> ListarRecibidasAsync(Guid calificadoId, int pagina, int tamano);
    Task<IReadOnlyList<Calificacion>> ListarDelAutorAsync(Guid autorId, int maximo);
    void Agregar(Calificacion calificacion);
}

public interface IComentarioRepository
{
    Task<Comentario?> ObtenerPorIdAsync(Guid id);
    /// <summary>
    /// Incluye Autor. Más recientes primero. Si <paramref name="incluirOcultos"/> es false, no se devuelven los ocultos
    /// ni los de autores suspendidos.
    /// </summary>
    Task<(IReadOnlyList<Comentario> Items, int Total)> ListarPorPublicacionAsync(Guid publicacionId, bool incluirOcultos, int pagina, int tamano, DateTime ahoraUtc);
    Task<int> ContarDelAutorDesdeAsync(Guid autorId, DateTime desdeUtc);
    /// <summary>CON tracking (exportación y ocultamiento al eliminar la cuenta).</summary>
    Task<IReadOnlyList<Comentario>> ListarDelAutorAsync(Guid autorId, int maximo);
    void Agregar(Comentario comentario);
}
