using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IPublicacionService
{
    Task<PublicacionDto> CrearAsync(Guid actorId, CrearPublicacionRequest r);
    /// <summary>Solo el dueño y solo si está Disponible. Reemplaza todos los campos (incluida la lista de fotos).</summary>
    Task<PublicacionDto> EditarAsync(Guid actorId, Guid id, CrearPublicacionRequest r);
    Task<PaginaDto<PublicacionDto>> ListarAsync(Guid? actorId, FiltroPublicacionesRequest f);
    /// <param name="visitante">Identificador anónimo de quien mira (usuario o hash de IP) para contar vistas; null = no contar.</param>
    Task<PublicacionDto> ObtenerAsync(Guid? actorId, Guid id, string? visitante = null);
    Task<IReadOnlyList<PublicacionCercanaDto>> ListarCercanasAsync(Guid? actorId, CercanasRequest r);
    /// <summary>Vitrina: destacadas vigentes en orden aleatorio (se muestran sobre el catálogo con cualquier orden o filtro).</summary>
    Task<IReadOnlyList<PublicacionDto>> ListarDestacadasAsync(Guid? actorId, DestacadasRequest r);
    Task<IReadOnlyList<PublicacionDto>> ListarMiasAsync(Guid actorId);
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync();
    Task CancelarAsync(Guid actorId, Guid id, string? motivo);
    /// <summary>Sube la publicación al primer lugar de "Más recientes" a cambio de Eco-Puntos (una vez cada 24 h).</summary>
    Task<PublicacionDto> ImpulsarAsync(Guid actorId, Guid id);
    /// <summary>Rendimiento de la publicación (solo el dueño). La serie diaria es un beneficio de los planes pagos.</summary>
    Task<EstadisticasPublicacionDto> ObtenerEstadisticasAsync(Guid actorId, Guid id);
    /// <summary>Perfil público; usuarios eliminados o suspendidos "no existen" (404).</summary>
    Task<PerfilUsuarioDto> ObtenerPerfilAsync(Guid usuarioId);
    Task<PaginaDto<PublicacionDto>> ListarDePerfilAsync(Guid? actorId, Guid usuarioId, int pagina, int tamano);
    Task MarcarFavoritaAsync(Guid actorId, Guid publicacionId);
    Task QuitarFavoritaAsync(Guid actorId, Guid publicacionId);
    Task<PaginaDto<PublicacionDto>> ListarFavoritasAsync(Guid actorId, int pagina, int tamano);
}

public sealed class PublicacionService : IPublicacionService
{
    public const int MaxFavoritos = 500;
    private const int MaxCandidatosCercanas = 500;
    private const int DiasSerieEstadisticas = 30;

    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICategoriaRepository _categorias;
    private readonly ISolicitudRepository _solicitudes;
    private readonly IFavoritoRepository _favoritos;
    private readonly IEstadisticaRepository _estadisticas;
    private readonly IUnidadDeTrabajo _uow;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly IAlmacenArchivos _almacen;
    private readonly IRegistroVistas _vistas;

    public PublicacionService(IPublicacionRepository pubs, IUsuarioRepository usuarios, ICategoriaRepository categorias,
        ISolicitudRepository solicitudes, IFavoritoRepository favoritos, IEstadisticaRepository estadisticas, IUnidadDeTrabajo uow,
        IOptions<UrlsOpciones> urls, TimeProvider reloj, INotificador notificador, IAlmacenArchivos almacen, IRegistroVistas vistas,
        ICalificacionRepository calificaciones, IPresencia presencia)
    {
        _pubs = pubs; _usuarios = usuarios; _categorias = categorias; _solicitudes = solicitudes; _favoritos = favoritos;
        _estadisticas = estadisticas; _uow = uow; _urls = urls.Value; _reloj = reloj; _notificador = notificador; _almacen = almacen;
        _vistas = vistas; _calificaciones = calificaciones; _presencia = presencia;
    }

    private readonly ICalificacionRepository _calificaciones;
    private readonly IPresencia _presencia;

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Cada foto NUEVA debe ser un archivo propio subido con SAS (o, sin almacenamiento, de un host permitido).
    /// Devuelve la lista final de URLs: las fotos nuevas se reemplazan por su copia limpia (sin GPS ni metadatos).
    /// </summary>
    private async Task<List<string>> ValidarImagenesAsync(Guid actorId, IReadOnlyList<string> imagenes, IEnumerable<string> yaAceptadas)
    {
        if (imagenes.Count > Publicacion.MaxImagenes) throw new ReglaDeNegocioException($"Puedes subir como máximo {Publicacion.MaxImagenes} fotos.");
        if (imagenes.Distinct(StringComparer.Ordinal).Count() != imagenes.Count) throw new ReglaDeNegocioException("Hay fotos repetidas.");
        var existentes = yaAceptadas.ToHashSet(StringComparer.Ordinal);
        var finales = new List<string>(imagenes.Count);
        foreach (var url in imagenes)
        {
            if (existentes.Contains(url)) finales.Add(url);
            else if (_almacen.Habilitado) finales.Add(await _almacen.ValidarArchivoPropioAsync(url, actorId, TipoArchivoDto.Imagen));
            else
            {
                ValidadorUrls.ExigirHostPermitido(url, _urls.HostsPermitidosImagenes, "La imagen");
                finales.Add(url);
            }
        }
        return finales;
    }

    private static bool SuspendidoOEliminado(Usuario u, DateTime ahora) => u.EstaEliminado || u.SuspensionVigente(ahora);

    private async Task<IReadOnlySet<Guid>> FavoritasAsync(Guid? actorId, IEnumerable<Publicacion> pubs)
        => actorId.HasValue
            ? await _favoritos.FiltrarFavoritasAsync(actorId.Value, pubs.Select(p => p.Id).ToList())
            : new HashSet<Guid>();

    private static DatosPublicacion Datos(CrearPublicacionRequest r, Categoria categoria, Usuario actor, IReadOnlyList<string> imagenes)
    {
        if (r.Condicion is null) throw new ReglaDeNegocioException("Indica el estado del producto (nuevo, usado, reparado…).");
        return new DatosPublicacion(r.Titulo, r.Descripcion, categoria, (ModoTransaccion)(int)r.Modo, (CondicionProducto)(int)r.Condicion.Value,
            r.DetalleCondicion, r.MunicipioCodigo ?? actor.MunicipioCodigo, r.Localidad, r.PrecioReferenciaCop, r.Latitud, r.Longitud, imagenes);
    }

    /// <summary>
    /// Estatuto del Consumidor (Ley 1480, art. 53): quien vende de forma habitual por un portal de contacto debe estar
    /// identificado. Más de N ventas activas exige identidad verificada o plan Empresa (que registra NIT).
    /// </summary>
    private async Task ExigirVendedorIdentificadoAsync(Usuario actor, ModoDto modo, Guid? excepto, DateTime ahora)
    {
        if (modo != ModoDto.Compra || actor.EsVerificado || actor.EmpresaVigente(ahora)) return;
        if (await _pubs.ContarVentasActivasAsync(actor.Id, excepto) >= PoliticaEcoPuntos.MaxVentasActivasSinIdentificar)
            throw new ReglaDeNegocioException(
                $"Para tener más de {PoliticaEcoPuntos.MaxVentasActivasSinIdentificar} artículos a la venta al mismo tiempo debes verificar tu identidad " +
                "o tener el plan Empresa (Estatuto del Consumidor, art. 53). Puedes hacerlo desde Eco-Puntos.");
    }

    public async Task<PublicacionDto> CrearAsync(Guid actorId, CrearPublicacionRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(actor);
        var ahora = Ahora;
        var plan = actor.PlanEfectivo(ahora);
        var maximo = PoliticaEcoPuntos.MaxPublicacionesActivas(plan);
        if (await _pubs.ContarActivasPorUsuarioAsync(actorId) >= maximo)
            throw new ReglaDeNegocioException(plan == TipoCuenta.Individual
                ? $"Alcanzaste el máximo de {maximo} publicaciones activas. Con el plan Premium puedes tener hasta {PoliticaEcoPuntos.MaxPublicacionesPremium}."
                : $"Alcanzaste el máximo de {maximo} publicaciones activas de tu plan.");
        await ExigirVendedorIdentificadoAsync(actor, r.Modo, null, ahora);

        var categoria = await _categorias.ObtenerPorIdAsync(r.CategoriaId) ?? throw new ReglaDeNegocioException("La categoría no existe.");
        var imagenes = await ValidarImagenesAsync(actorId, r.Imagenes ?? new List<string>(), Array.Empty<string>());

        var pub = new Publicacion(actorId, Datos(r, categoria, actor, imagenes));
        _pubs.Agregar(pub);
        await _uow.GuardarCambiosAsync();

        var creada = await _pubs.ObtenerPorIdAsync(pub.Id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        return Mapeos.APublicacionDto(creada, actorId, veExacto: true, veModeracion: false, ahora, vistas: 0);
    }

    public async Task<PublicacionDto> EditarAsync(Guid actorId, Guid id, CrearPublicacionRequest r)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(actor);
        var ahora = Ahora;
        // Con personas interesadas no se cambia lo que pidieron (precio, modo, fotos...): nadie debe recibir algo distinto.
        if ((await InteresadosAsync(new[] { p })).GetValueOrDefault(p.Id) > 0)
            throw new ReglaDeNegocioException("Ya hay personas interesadas en esta publicación: no se puede editar para que nadie reciba algo distinto de lo que pidió. Respóndeles o cancélala y publica de nuevo.");
        await ExigirVendedorIdentificadoAsync(actor, r.Modo, p.Id, ahora);

        var categoria = await _categorias.ObtenerPorIdAsync(r.CategoriaId) ?? throw new ReglaDeNegocioException("La categoría no existe.");
        // las fotos que ya tenía no se revalidan; las nuevas se limpian
        var imagenes = await ValidarImagenesAsync(actorId, r.Imagenes ?? new List<string>(), p.Imagenes.Select(i => i.Url));
        p.Editar(Datos(r, categoria, actor, imagenes), ahora);
        await _uow.GuardarCambiosAsync(); // RowVersion: si justo llegó una solicitud, 409
        var favorita = (await _favoritos.ObtenerAsync(actorId, p.Id)) is not null;
        var vistas = (await _estadisticas.TotalesAsync(new[] { p.Id })).GetValueOrDefault(p.Id);
        return Mapeos.APublicacionDto(p, actorId, veExacto: true, veModeracion: false, ahora, favorita, vistas);
    }

    public async Task<PaginaDto<PublicacionDto>> ListarAsync(Guid? actorId, FiltroPublicacionesRequest f)
    {
        if (f.PrecioMin.HasValue && f.PrecioMax.HasValue && f.PrecioMin > f.PrecioMax)
            throw new ReglaDeNegocioException("El precio mínimo no puede ser mayor que el máximo.");
        var ahora = Ahora;
        var filtro = new FiltroPublicaciones
        {
            CategoriaId = f.CategoriaId,
            Modo = f.Modo.HasValue ? (ModoTransaccion)(int)f.Modo.Value : null,
            Condicion = f.Condicion.HasValue ? (CondicionProducto)(int)f.Condicion.Value : null,
            DepartamentoCodigo = f.DepartamentoCodigo,
            MunicipioCodigo = f.MunicipioCodigo,
            Localidad = f.Localidad,
            Texto = f.Texto,
            PrecioMin = f.PrecioMin,
            PrecioMax = f.PrecioMax,
            SoloVerificados = f.SoloVerificados,
            Orden = (OrdenPublicaciones)(int)f.Orden,
            Pagina = f.Pagina,
            Tamano = f.Tamano
        };
        var (items, total) = await _pubs.ListarVisiblesAsync(filtro, ahora);
        var favoritas = await FavoritasAsync(actorId, items);
        var interesados = await InteresadosAsync(items);
        var dtos = items.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: actorId.HasValue && p.PropietarioId == actorId,
            veModeracion: false, ahora, favoritas.Contains(p.Id), interesados: interesados.GetValueOrDefault(p.Id))).ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(f.Pagina, 1), Math.Clamp(f.Tamano, 1, 50));
    }

    public async Task<IReadOnlyList<PublicacionDto>> ListarDestacadasAsync(Guid? actorId, DestacadasRequest r)
    {
        var ahora = Ahora;
        var items = await _pubs.ListarDestacadasAsync(r.DepartamentoCodigo, r.MunicipioCodigo, r.CategoriaId, Math.Clamp(r.Max, 1, 12), ahora);
        var favoritas = await FavoritasAsync(actorId, items);
        var interesados = await InteresadosAsync(items);
        return items.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: actorId.HasValue && p.PropietarioId == actorId,
            veModeracion: false, ahora, favoritas.Contains(p.Id), interesados: interesados.GetValueOrDefault(p.Id))).ToList();
    }

    /// <summary>Solicitudes pendientes por publicación (una sola consulta para toda la página).</summary>
    private Task<IReadOnlyDictionary<Guid, int>> InteresadosAsync(IEnumerable<Publicacion> pubs)
        => _solicitudes.ContarPendientesPorPublicacionAsync(pubs.Select(p => p.Id).ToList());

    public async Task<PublicacionDto> ObtenerAsync(Guid? actorId, Guid id, string? visitante = null)
    {
        var p = await _pubs.ObtenerPorIdAsync(id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        var actor = actorId.HasValue ? await _usuarios.ObtenerPorIdAsync(actorId.Value) : null;
        var ahora = Ahora;

        var esDueno = actor is not null && p.PropietarioId == actor.Id;
        var esModerador = actor is not null && actor.Rol >= RolUsuarioEnum.Administrador;
        var aceptada = actor is not null && !esDueno && await _solicitudes.ExisteAceptadaAsync(p.Id, actor.Id);
        var visiblePublicamente = p.Estado == EstadoPublicacionEnum.Disponible && !p.EstaOculta && !SuspendidoOEliminado(p.Propietario!, ahora);

        // Lo que no puede ver, "no existe" para el usuario (no se filtra información por códigos distintos)
        if (!visiblePublicamente && !esDueno && !esModerador && !aceptada)
            throw new NoEncontradoException("Publicación no encontrada.");

        // Vista única por persona: ni el dueño ni los moderadores inflan las estadísticas
        if (visiblePublicamente && !esDueno && !esModerador && !string.IsNullOrWhiteSpace(visitante))
            _vistas.Registrar(p.Id, visitante);

        var favorita = actor is not null && (await _favoritos.ObtenerAsync(actor.Id, p.Id)) is not null;
        int? vistas = esDueno ? (await _estadisticas.TotalesAsync(new[] { p.Id })).GetValueOrDefault(p.Id) : null;
        var interesados = (await InteresadosAsync(new[] { p })).GetValueOrDefault(p.Id);
        var yaSolicite = actor is not null && !esDueno && await _solicitudes.ExisteEnCursoAsync(p.Id, actor.Id);
        return Mapeos.APublicacionDto(p, actor?.Id, veExacto: esDueno || esModerador || aceptada, veModeracion: esModerador, ahora, favorita, vistas,
            interesados, yaSolicite);
    }

    /// <summary>
    /// El filtro por radio y la distancia usan las coordenadas que el usuario PUEDE ver (aproximadas si no es el dueño).
    /// Si se usaran las exactas, variando el punto de consulta se podría triangular la ubicación real.
    /// </summary>
    public async Task<IReadOnlyList<PublicacionCercanaDto>> ListarCercanasAsync(Guid? actorId, CercanasRequest r)
    {
        if (r.Lat is null || r.Lon is null) throw new ReglaDeNegocioException("Indica latitud y longitud.");
        var lat = r.Lat.Value;
        var lon = r.Lon.Value;
        var radio = Math.Clamp(r.RadioKm, 0.1, 50);
        var ahora = Ahora;

        // margen = media celda de redondeo: incluye publicaciones cuya posición aproximada cae dentro del círculo
        var (minLat, maxLat, minLon, maxLon) = Geo.Caja(lat, lon, radio, margenGrados: 0.006);
        var candidatos = await _pubs.ListarVisiblesEnAreaAsync(minLat, maxLat, minLon, maxLon, r.CategoriaId,
            r.Modo.HasValue ? (ModoTransaccion)(int)r.Modo.Value : null, MaxCandidatosCercanas, ahora);
        if (r.Condicion.HasValue)
            candidatos = candidatos.Where(p => p.Condicion == (CondicionProducto)(int)r.Condicion.Value).ToList();

        var cercanas = candidatos
            .Select(p =>
            {
                var esMia = actorId.HasValue && p.PropietarioId == actorId.Value;
                var pLat = esMia ? p.Latitud!.Value : Geo.Aproximar(p.Latitud!.Value);
                var pLon = esMia ? p.Longitud!.Value : Geo.Aproximar(p.Longitud!.Value);
                return (p, esMia, d: Geo.DistanciaKm(lat, lon, pLat, pLon));
            })
            .Where(x => x.d <= radio)
            .OrderBy(x => x.d)
            .Take(Math.Clamp(r.Max, 1, 50))
            .ToList();
        var favoritas = await FavoritasAsync(actorId, cercanas.Select(x => x.p));
        var interesados = await InteresadosAsync(cercanas.Select(x => x.p));
        return cercanas.Select(x => new PublicacionCercanaDto(
                Mapeos.APublicacionDto(x.p, actorId, veExacto: x.esMia, veModeracion: false, ahora, favoritas.Contains(x.p.Id),
                    interesados: interesados.GetValueOrDefault(x.p.Id)),
                Math.Round(x.d, 1, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public async Task<IReadOnlyList<PublicacionDto>> ListarMiasAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _pubs.ListarPorPropietarioAsync(actorId);
        var favoritas = await FavoritasAsync(actorId, lista);
        var vistas = await _estadisticas.TotalesAsync(lista.Select(p => p.Id).ToList());
        var interesados = await InteresadosAsync(lista);
        return lista.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: true, veModeracion: false, ahora, favoritas.Contains(p.Id),
            vistas.GetValueOrDefault(p.Id), interesados.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync()
        => (await _categorias.ListarAsync()).Select(Mapeos.ACategoriaDto).ToList();

    public async Task CancelarAsync(Guid actorId, Guid id, string? motivo)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");

        var ahora = Ahora;
        // Todas las personas interesadas (la aceptada, si la hay, y la lista de espera) se cierran y reciben aviso.
        var enCurso = await _solicitudes.ListarEnCursoPorPublicacionAsync(p.Id, incluirAceptada: true);
        const string aviso = "La publicación fue cancelada por su propietario.";
        foreach (var s in enCurso)
        {
            if (s.Estado == EstadoSolicitud.Aceptada) s.MarcarNoConcretada(aviso, ahora);
            else s.Rechazar(aviso);
        }
        p.Cancelar(motivo ?? "");
        await _uow.GuardarCambiosAsync();
        foreach (var s in enCurso)
            await _notificador.NotificarAsync(s.SolicitanteId, TiposNotificacion.SolicitudRechazada,
                $"La publicación \"{p.Titulo}\" fue cancelada por su propietario.", s.Id);
    }

    // ---------------- Impulso y estadísticas ----------------
    public async Task<PublicacionDto> ImpulsarAsync(Guid actorId, Guid id)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(actor);
        var ahora = Ahora;
        p.ValidarPuedeImpulsarse(ahora); // validar ANTES de cobrar los puntos
        if (actor.SaldoEcoPuntos < PoliticaEcoPuntos.PuntosImpulsar)
            throw new ReglaDeNegocioException($"Necesitas {PoliticaEcoPuntos.PuntosImpulsar} Eco-Puntos para impulsar. Gánalos con intercambios o recárgalos.");
        actor.DebitarEcoPuntos(PoliticaEcoPuntos.PuntosImpulsar);
        p.Impulsar(ahora);
        await _uow.GuardarCambiosAsync(); // RowVersion en usuario y publicación: un doble clic no cobra dos veces
        var vistas = (await _estadisticas.TotalesAsync(new[] { p.Id })).GetValueOrDefault(p.Id);
        var favorita = (await _favoritos.ObtenerAsync(actorId, p.Id)) is not null;
        return Mapeos.APublicacionDto(p, actorId, veExacto: true, veModeracion: false, ahora, favorita, vistas);
    }

    public async Task<EstadisticasPublicacionDto> ObtenerEstadisticasAsync(Guid actorId, Guid id)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var ahora = Ahora;
        var desde = ahora.Date.AddDays(-(DiasSerieEstadisticas - 1));
        var serie = await _estadisticas.SerieAsync(p.Id, desde);
        var total = (await _estadisticas.TotalesAsync(new[] { p.Id })).GetValueOrDefault(p.Id);
        var conPlan = actor.PlanPagoVigente(ahora);

        var puntos = new List<PuntoSerieDto>();
        if (conPlan)
        {
            var porDia = serie.ToDictionary(e => e.Fecha.Date, e => e.Vistas);
            for (var d = desde; d <= ahora.Date; d = d.AddDays(1))
                puntos.Add(new PuntoSerieDto(DateTime.SpecifyKind(d, DateTimeKind.Utc), porDia.GetValueOrDefault(d)));
        }
        return new EstadisticasPublicacionDto(p.Id, total, serie.Sum(e => e.Vistas), await _favoritos.ContarPorPublicacionAsync(p.Id),
            await _solicitudes.ContarPorPublicacionAsync(p.Id), p.EstaDestacadaVigente(ahora),
            p.EstaDestacadaVigente(ahora) ? p.DestacadaHasta : null, conPlan, puntos);
    }

    // ---------------- Perfil público ----------------
    private async Task<Usuario> PerfilVisibleAsync(Guid usuarioId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (u is null || SuspendidoOEliminado(u, Ahora)) throw new NoEncontradoException("Usuario no encontrado.");
        return u;
    }

    public async Task<PerfilUsuarioDto> ObtenerPerfilAsync(Guid usuarioId)
    {
        var u = await PerfilVisibleAsync(usuarioId);
        var ahora = Ahora;
        var (_, total) = await _pubs.ListarVisiblesDePropietarioAsync(usuarioId, 1, 1, ahora);
        return Mapeos.APerfilUsuario(u, total, await SenalesConfianzaAsync(u, ahora), ahora);
    }

    /// <summary>Hechos verificables y agregados del último año que ayudan a decidir si intercambiar con alguien.</summary>
    private async Task<SenalesConfianzaDto> SenalesConfianzaAsync(Usuario u, DateTime ahora)
    {
        var (completados, noConcretados, horas) = await _solicitudes.ConfianzaAsync(u.Id, ahora.AddYears(-1));
        int? tasa = completados + noConcretados > 0
            ? (int)Math.Round(100.0 * completados / (completados + noConcretados), MidpointRounding.AwayFromZero) : null;
        double? respuesta = null;
        if (horas.Count > 0)
        {
            var orden = horas.Order().ToList();
            var mediana = orden.Count % 2 == 1 ? orden[orden.Count / 2] : (orden[orden.Count / 2 - 1] + orden[orden.Count / 2]) / 2;
            respuesta = Math.Round(mediana, 1, MidpointRounding.AwayFromZero);
        }
        var enLinea = (await _presencia.EnLineaAsync(new[] { u.Id })).Contains(u.Id);
        return new SenalesConfianzaDto(u.CorreoVerificado, u.EsVerificado, u.GoogleSub is not null, u.DosFactoresActivo, enLinea,
            u.TotalTruekesCompletados + u.TotalComprasRealizadas + u.TotalDonacionesRealizadas, tasa, respuesta,
            await _calificaciones.DistribucionAsync(u.Id));
    }

    public async Task<PaginaDto<PublicacionDto>> ListarDePerfilAsync(Guid? actorId, Guid usuarioId, int pagina, int tamano)
    {
        await PerfilVisibleAsync(usuarioId);
        var ahora = Ahora;
        var (items, total) = await _pubs.ListarVisiblesDePropietarioAsync(usuarioId, pagina, tamano, ahora);
        var favoritas = await FavoritasAsync(actorId, items);
        var dtos = items.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: actorId == usuarioId, veModeracion: false, ahora,
            favoritas.Contains(p.Id))).ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    // ---------------- Favoritos ----------------
    public async Task MarcarFavoritaAsync(Guid actorId, Guid publicacionId)
    {
        var p = await _pubs.ObtenerPorIdAsync(publicacionId);
        var ahora = Ahora;
        if (p is null || p.EstaOculta || p.Estado != EstadoPublicacionEnum.Disponible && p.Estado != EstadoPublicacionEnum.EnNegociacion
            || SuspendidoOEliminado(p.Propietario!, ahora))
            throw new NoEncontradoException("Publicación no encontrada.");
        if (await _favoritos.ObtenerAsync(actorId, publicacionId) is not null) return; // idempotente
        if (await _favoritos.ContarAsync(actorId) >= MaxFavoritos)
            throw new ReglaDeNegocioException($"Puedes guardar como máximo {MaxFavoritos} publicaciones.");
        _favoritos.Agregar(new Favorito(actorId, publicacionId, ahora));
        try { await _uow.GuardarCambiosAsync(); }
        catch (ConflictoDeConcurrenciaException) { /* doble clic simultáneo: ya quedó guardada */ }
    }

    public async Task QuitarFavoritaAsync(Guid actorId, Guid publicacionId)
    {
        var f = await _favoritos.ObtenerAsync(actorId, publicacionId);
        if (f is null) return; // idempotente
        _favoritos.Quitar(f);
        await _uow.GuardarCambiosAsync();
    }

    /// <summary>Las que ya no están visibles (canceladas, ocultas, intercambiadas) se devuelven con su estado, para que el usuario las quite.</summary>
    public async Task<PaginaDto<PublicacionDto>> ListarFavoritasAsync(Guid actorId, int pagina, int tamano)
    {
        var (ids, total) = await _favoritos.ListarAsync(actorId, pagina, tamano);
        var pubs = (await _pubs.ListarPorIdsAsync(ids)).ToDictionary(p => p.Id);
        var ahora = Ahora;
        var dtos = ids.Where(pubs.ContainsKey).Select(id => pubs[id])
            .Where(p => !p.EstaOculta && !SuspendidoOEliminado(p.Propietario!, ahora))
            .Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: p.PropietarioId == actorId, veModeracion: false, ahora, esFavorita: true))
            .ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }
}
