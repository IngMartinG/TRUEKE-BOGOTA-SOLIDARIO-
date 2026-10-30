using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Lo que mantiene vivo el catálogo y trae de vuelta a la gente (Bloque 5): "¿Sigue disponible?" para las publicaciones
/// inactivas y el resumen semanal opcional. Lo llama la tarea de mantenimiento; cada paso marca antes de avisar, así que
/// nunca se avisa dos veces aunque falle algo después.
/// </summary>
public interface IRetencionService
{
    /// <summary>A los 30 días sin actividad le pregunta al dueño si la publicación sigue disponible.</summary>
    Task<int> PreguntarDisponibilidadAsync(int maximo);
    /// <summary>Pausa las que nadie confirmó en los 7 días siguientes a la pregunta.</summary>
    Task<int> PausarSinRespuestaAsync(int maximo);
    /// <summary>Resumen semanal por correo a quien lo activó: novedades en su localidad y coincidencias con "Lo que busco".</summary>
    Task<int> EnviarResumenesSemanalesAsync(int maximo);
}

public sealed class RetencionService : IRetencionService
{
    public static readonly TimeSpan IntervaloResumen = TimeSpan.FromDays(7);
    private const int MaxTitulos = 5;
    private const int MaxDeseosEnResumen = 5;

    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly IDeseoRepository _deseos;
    private readonly IBloqueoRepository _bloqueos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly ICorreoSaliente _correo;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly ILogger<RetencionService> _log;

    public RetencionService(IPublicacionRepository pubs, IUsuarioRepository usuarios, IDeseoRepository deseos, IBloqueoRepository bloqueos,
        IUnidadDeTrabajo uow, INotificador notificador, ICorreoSaliente correo, IOptions<UrlsOpciones> urls, TimeProvider reloj,
        ILogger<RetencionService> log)
    {
        _pubs = pubs; _usuarios = usuarios; _deseos = deseos; _bloqueos = bloqueos; _uow = uow; _notificador = notificador;
        _correo = correo; _urls = urls.Value; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;
    private string Frontend => _urls.Frontend.TrimEnd('/');

    public async Task<int> PreguntarDisponibilidadAsync(int maximo)
    {
        var ahora = Ahora;
        var pubs = await _pubs.ListarSinActividadAsync(ahora.AddDays(-Publicacion.DiasSinActividadParaPreguntar), ahora, maximo);
        if (pubs.Count == 0) return 0;
        foreach (var p in pubs) p.PedirConfirmacionDisponibilidad(ahora);
        await _uow.GuardarCambiosAsync();

        foreach (var p in pubs)
            await _notificador.NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionSigueDisponible,
                $"¿«{p.Titulo}» sigue disponible? Confírmalo en Mis publicaciones; si no, la pausaremos en {Publicacion.DiasParaResponderDisponibilidad} días.", p.Id);
        EnviarCorreos(pubs, "¿Tus publicaciones siguen disponibles?",
            [$"Hace {Publicacion.DiasSinActividadParaPreguntar} días que no hay movimiento en estas publicaciones:"],
            [$"Si siguen disponibles, confírmalo con un clic. Si no respondes en {Publicacion.DiasParaResponderDisponibilidad} días, las pausaremos: saldrán del catálogo y podrás reactivarlas cuando quieras.",
             "Si ya no las tienes, cancélalas para que nadie te escriba por algo que ya no está."],
            "Confirmar en Mis publicaciones");
        _log.LogInformation("¿Sigue disponible?: {Total} publicaciones preguntadas", pubs.Count);
        return pubs.Count;
    }

    public async Task<int> PausarSinRespuestaAsync(int maximo)
    {
        var ahora = Ahora;
        var pubs = await _pubs.ListarParaPausarAsync(ahora.AddDays(-Publicacion.DiasParaResponderDisponibilidad), maximo);
        if (pubs.Count == 0) return 0;
        foreach (var p in pubs) p.Pausar();
        await _uow.GuardarCambiosAsync();

        foreach (var p in pubs)
            await _notificador.NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionPausada,
                $"Pausamos «{p.Titulo}» porque no confirmaste que siguiera disponible. Reactívala en Mis publicaciones cuando quieras.", p.Id);
        EnviarCorreos(pubs, "Pausamos algunas de tus publicaciones",
            ["No confirmaste que estas publicaciones siguieran disponibles, así que las pausamos y salieron del catálogo:"],
            ["No se borró nada: puedes reactivarlas desde Mis publicaciones con un clic."],
            "Ir a Mis publicaciones");
        _log.LogInformation("¿Sigue disponible?: {Total} publicaciones pausadas", pubs.Count);
        return pubs.Count;
    }

    /// <summary>Un solo correo por dueño, con los títulos de todas sus publicaciones (solo si tiene activos los avisos de intercambios).</summary>
    private void EnviarCorreos(IReadOnlyList<Publicacion> pubs, string asunto, string[] antes, string[] despues, string boton)
    {
        foreach (var grupo in pubs.GroupBy(p => p.PropietarioId))
        {
            var u = grupo.First().Propietario;
            if (u is null || !u.CorreoVerificado || !u.AvisosCorreoIntercambios) continue; // apagado en Cuenta → Avisos
            var titulos = grupo.Select(p => $"• {p.Titulo}").ToList();
            _correo.Encolar(PlantillaCorreo.Crear(u.Correo, asunto, $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                [.. antes, string.Join("\n", titulos), .. despues], (boton, $"{Frontend}/mis-publicaciones")));
        }
    }

    public async Task<int> EnviarResumenesSemanalesAsync(int maximo)
    {
        var ahora = Ahora;
        var desde = ahora - IntervaloResumen;
        var usuarios = await _usuarios.ListarParaResumenSemanalAsync(desde, ahora, maximo);
        if (usuarios.Count == 0) return 0;
        foreach (var u in usuarios) u.MarcarResumenSemanal(ahora);
        await _uow.GuardarCambiosAsync(); // primero se marca: nunca llega dos veces la misma semana

        var enviados = 0;
        foreach (var u in usuarios)
        {
            try
            {
                var parrafos = await ContenidoResumenAsync(u, desde, ahora);
                if (parrafos.Count == 0) continue; // sin novedades no se escribe
                _correo.Encolar(PlantillaCorreo.Crear(u.Correo, "Tu semana en Trueke", $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                    parrafos, ("Explorar el catálogo", $"{Frontend}/explorar"),
                    "Recibes este resumen porque lo activaste. Puedes apagarlo en Cuenta → Avisos y notificaciones."));
                enviados++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo armar el resumen semanal de {UsuarioId}", u.Id);
            }
        }
        if (enviados > 0) _log.LogInformation("Resúmenes semanales enviados: {Total}", enviados);
        return enviados;
    }

    private async Task<List<string>> ContenidoResumenAsync(Usuario u, DateTime desde, DateTime ahora)
    {
        var bloqueados = await _bloqueos.RelacionadosAsync(u.Id);
        var parrafos = new List<string>();
        if (!u.LocalidadPendiente)
        {
            var (items, total) = await _pubs.ListarVisiblesAsync(new FiltroPublicaciones
            {
                MunicipioCodigo = u.MunicipioCodigo, Localidad = u.Localidad, ExcluirPropietarioId = u.Id,
                PublicadasDesde = desde, PublicadasHasta = ahora, Tamano = MaxTitulos
            }, ahora);
            var titulos = items.Where(p => !bloqueados.Contains(p.PropietarioId)).Select(p => $"«{p.Titulo}»").ToList();
            total -= items.Count - titulos.Count; // las de personas bloqueadas no cuentan
            if (titulos.Count > 0)
                parrafos.Add(total == 1
                    ? $"Esta semana se publicó 1 objeto nuevo en {u.Localidad}: {titulos[0]}."
                    : $"Esta semana se publicaron {total} objetos nuevos en {u.Localidad}, entre ellos {string.Join(", ", titulos)}.");
        }

        var coincidencias = new List<string>();
        foreach (var d in (await _deseos.DeUsuarioAsync(u.Id)).Take(MaxDeseosEnResumen))
        {
            var (items, total) = await _pubs.ListarVisiblesAsync(new FiltroPublicaciones
            {
                Texto = d.Texto, CategoriaId = d.CategoriaId, ExcluirPropietarioId = u.Id,
                PublicadasDesde = desde, PublicadasHasta = ahora, Tamano = 10
            }, ahora);
            var visibles = items.Count(p => !bloqueados.Contains(p.PropietarioId));
            var nuevas = total - (items.Count - visibles);
            if (visibles > 0) coincidencias.Add($"«{d.Texto}»: {nuevas} {(nuevas == 1 ? "nueva" : "nuevas")}");
        }
        if (coincidencias.Count > 0)
            parrafos.Add($"Coincidencias con lo que buscas: {string.Join("; ", coincidencias)}. Míralas en {Frontend}/lo-que-busco.");
        return parrafos;
    }
}
