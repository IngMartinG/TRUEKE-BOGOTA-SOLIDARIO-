using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Asistente;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IAsistenteService
{
    bool Habilitado { get; }
    Task<RespuestaAsistenteDto> ResponderAsync(Guid actorId, PreguntaAsistenteRequest r, CancellationToken ct = default);
}

/// <summary>
/// Asistente de ayuda con IA. Solo orienta: sus herramientas leen el catálogo público y los datos de quien pregunta (el
/// actor sale del JWT, nunca del modelo) y la respuesta solo lleva enlaces a rutas conocidas de la app. El historial no se
/// guarda: lo envía el front en cada pregunta.
/// </summary>
public sealed partial class AsistenteService : IAsistenteService
{
    /// <summary>Rondas de herramientas por pregunta: acota los tokens del cupo gratis y evita ciclos.</summary>
    private const int MaxRondas = 3;
    private const int MaxEnlaces = 3;
    private const int MaxResultadosBusqueda = 5;
    private const int MaxElementosLista = 10;

    internal static readonly HashSet<string> RutasPermitidas = new(StringComparer.Ordinal)
    {
        "/", "/explorar", "/como-funciona", "/impacto", "/apoya", "/ayuda", "/pqrs", "/publicar", "/mis-publicaciones",
        "/favoritos", "/lo-que-busco", "/intercambios", "/mensajes", "/notificaciones", "/eco-puntos", "/planes",
        "/cuenta/perfil", "/cuenta/seguridad", "/cuenta/facturacion", "/cuenta/vendedor", "/cuenta/soporte", "/cuenta/reportes",
        "/cuenta/avisos", "/cuenta/busquedas", "/cuenta/privacidad", "/privacidad", "/terminos",
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IModeloLenguaje _modelo;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionService _publicaciones;
    private readonly ISolicitudService _solicitudes;
    private readonly IEcoPuntosService _ecoPuntos;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _reloj;
    private readonly AsistenteOpciones _o;
    private readonly PagosOpciones _pagos;
    private readonly ILogger<AsistenteService> _log;

    public AsistenteService(IModeloLenguaje modelo, IUsuarioRepository usuarios, IPublicacionService publicaciones, ISolicitudService solicitudes,
        IEcoPuntosService ecoPuntos, IMemoryCache cache, TimeProvider reloj, IOptions<AsistenteOpciones> o, IOptions<PagosOpciones> pagos,
        ILogger<AsistenteService> log)
    {
        _modelo = modelo; _usuarios = usuarios; _publicaciones = publicaciones; _solicitudes = solicitudes; _ecoPuntos = ecoPuntos;
        _cache = cache; _reloj = reloj; _o = o.Value; _pagos = pagos.Value; _log = log;
    }

    public bool Habilitado => _modelo.Habilitado;

    public async Task<RespuestaAsistenteDto> ResponderAsync(Guid actorId, PreguntaAsistenteRequest r, CancellationToken ct = default)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        if (!_modelo.Habilitado) throw new AsistenteNoDisponibleException();
        ConsumirCupo(actorId);

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var mensajes = new List<MensajeModelo>
        {
            new(RolMensaje.Sistema, InstruccionesAsistente.Construir(_ecoPuntos.ObtenerPolitica(), _pagos.VentasHabilitadas, ahora)),
        };
        foreach (var t in r.Historial.TakeLast(_o.MaxTurnos))
            mensajes.Add(new(t.Rol == RolTurnoAsistenteDto.Asistente ? RolMensaje.Asistente : RolMensaje.Usuario, t.Texto));
        mensajes.Add(new(RolMensaje.Usuario, r.Pregunta.Trim()));

        try
        {
            for (var ronda = 0; ; ronda++)
            {
                // En la última ronda ya no se ofrecen herramientas: el modelo debe contestar con lo que tiene.
                var respuesta = await _modelo.CompletarAsync(mensajes, ronda < MaxRondas ? Herramientas : [], ct);
                // Si aun sin herramientas insiste en llamarlas, se responde con lo que haya (nunca un ciclo sin fin).
                if (respuesta.Llamadas is not { Count: > 0 } || ronda >= MaxRondas)
                    return Armar(respuesta.Contenido);
                mensajes.Add(respuesta);
                foreach (var llamada in respuesta.Llamadas)
                    mensajes.Add(new(RolMensaje.Herramienta, await EjecutarAsync(actorId, llamada), IdLlamada: llamada.Id));
            }
        }
        catch (ModeloNoDisponibleException ex)
        {
            _log.LogWarning("Asistente no disponible: {Motivo}", ex.Message);
            throw new AsistenteNoDisponibleException(ex);
        }
    }

    private void ConsumirCupo(Guid actorId)
    {
        var hoy = _reloj.GetUtcNow().UtcDateTime.Date;
        var contador = _cache.GetOrCreate($"asistente:{actorId:N}:{hoy:yyyyMMdd}", e =>
        {
            e.AbsoluteExpiration = hoy.AddDays(1);
            return new Contador();
        })!;
        if (Interlocked.Increment(ref contador.Usadas) > _o.MensajesPorDia) throw new LimiteAsistenteException();
    }

    private sealed class Contador
    {
        public int Usadas;
    }

    // ---------- Herramientas (solo lectura) ----------

    internal static readonly IReadOnlyList<HerramientaModelo> Herramientas =
    [
        new("buscar_publicaciones", "Busca publicaciones activas en el catálogo público.", JsonNode.Parse("""
            {"type":"object","properties":{
              "texto":{"type":"string","description":"Qué busca la persona, en pocas palabras"},
              "modo":{"type":"string","enum":["Trueke","Compra","Donacion"]},
              "localidad":{"type":"string","description":"Localidad o barrio, si la persona la dijo"}},
             "required":["texto"]}
            """)!.AsObject()),
        new("mi_resumen_eco_puntos", "Saldo de Eco-Puntos, reputación, nivel y plan de la persona que pregunta.", SinParametros()),
        new("mis_solicitudes", "Solicitudes que la persona envió y recibió, con su estado.", SinParametros()),
        new("mis_publicaciones", "Publicaciones de la persona que pregunta, con su estado.", SinParametros()),
    ];

    private static JsonObject SinParametros() => new() { ["type"] = "object", ["properties"] = new JsonObject() };

    internal async Task<string> EjecutarAsync(Guid actorId, LlamadaHerramienta llamada)
    {
        try
        {
            object resultado = llamada.Nombre switch
            {
                "buscar_publicaciones" => await BuscarAsync(actorId, llamada.ArgumentosJson),
                "mi_resumen_eco_puntos" => await ResumenAsync(actorId),
                "mis_solicitudes" => await SolicitudesAsync(actorId),
                "mis_publicaciones" => await PublicacionesAsync(actorId),
                _ => new { error = "Herramienta desconocida." },
            };
            return JsonSerializer.Serialize(resultado, Json);
        }
        catch (Exception ex) when (ex is JsonException or ReglaDeNegocioException or NoEncontradoException or AccesoDenegadoException)
        {
            return JsonSerializer.Serialize(new { error = "No se pudo consultar." }, Json);
        }
    }

    private async Task<object> BuscarAsync(Guid actorId, string argumentosJson)
    {
        var a = JsonSerializer.Deserialize<ArgumentosBusqueda>(string.IsNullOrWhiteSpace(argumentosJson) ? "{}" : argumentosJson, Json)
                ?? new ArgumentosBusqueda();
        var filtro = new FiltroPublicacionesRequest
        {
            Texto = Recortar(a.Texto, 100),
            Localidad = Recortar(a.Localidad, 60),
            Modo = Enum.TryParse<ModoDto>(a.Modo, true, out var modo) ? modo : null,
            Tamano = MaxResultadosBusqueda,
        };
        var pagina = await _publicaciones.ListarAsync(actorId, filtro);
        return new
        {
            total = pagina.Total,
            publicaciones = pagina.Items.Select(p => new
            {
                id = p.Id, titulo = p.Titulo, modo = p.Modo, categoria = p.Categoria.Nombre, condicion = p.Condicion,
                localidad = p.Localidad, municipio = p.Municipio, precioCop = p.PrecioReferenciaCop, esMia = p.EsMia,
            }),
        };
    }

    private async Task<object> ResumenAsync(Guid actorId)
    {
        var r = await _ecoPuntos.ResumenAsync(actorId);
        return new
        {
            saldo = r.Saldo, reputacion = r.Reputacion, nivel = r.Nivel?.Nombre, siguienteNivel = r.Nivel?.SiguienteNivel,
            puntosParaSiguienteNivel = r.Nivel?.PuntosSiguienteNivel is { } s ? s - r.Nivel.PuntosParticipacion : (int?)null,
            plan = r.TipoCuenta, planVigenteHasta = r.PlanVigenteHasta, destacadosGratisRestantes = r.DestacadosGratisRestantes,
            transaccionesConPuntosRestantesHoy = r.TransaccionesConPuntosRestantes, pruebaPremiumDisponible = r.PruebaPremiumDisponible,
        };
    }

    private async Task<object> SolicitudesAsync(Guid actorId)
    {
        var enviadas = await _solicitudes.ListarEnviadasAsync(actorId);
        var recibidas = await _solicitudes.ListarRecibidasAsync(actorId);
        // Sin nombres ni mensajes de la otra persona: el modelo solo necesita saber en qué va cada una.
        static object Resumen(SolicitudDto s) => new
        {
            publicacion = s.PublicacionTitulo, publicacionId = s.PublicacionId, modo = s.Modo, estado = s.Estado, enEspera = s.EnEspera,
            fecha = s.FechaSolicitud, yoConfirmeEntrega = s.SoyDuenio ? s.ConfirmadaPorDuenio : s.ConfirmadaPorSolicitante,
            laOtraParteConfirmo = s.SoyDuenio ? s.ConfirmadaPorSolicitante : s.ConfirmadaPorDuenio, cierreAutomatico = s.CierreAutomaticoUtc,
            puedoCalificar = s.PuedoCalificar,
        };
        return new
        {
            enviadas = enviadas.OrderByDescending(s => s.FechaSolicitud).Take(MaxElementosLista).Select(Resumen),
            recibidas = recibidas.OrderByDescending(s => s.FechaSolicitud).Take(MaxElementosLista).Select(Resumen),
        };
    }

    private async Task<object> PublicacionesAsync(Guid actorId)
    {
        var mias = await _publicaciones.ListarMiasAsync(actorId);
        return new
        {
            total = mias.Count,
            publicaciones = mias.OrderByDescending(p => p.FechaPublicacion).Take(MaxElementosLista).Select(p => new
            {
                id = p.Id, titulo = p.Titulo, modo = p.Modo, estado = p.Estado, destacada = p.Destacada, oculta = p.Oculta,
                interesados = p.Interesados, vistas = p.Vistas, proximoImpulso = p.ProximoImpulsoUtc,
            }),
        };
    }

    private static string? Recortar(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max];
    }

    private sealed class ArgumentosBusqueda
    {
        public string? Texto { get; init; }
        public string? Modo { get; init; }
        public string? Localidad { get; init; }
    }

    // ---------- Respuesta ----------

    [GeneratedRegex(@"\[\[\s*(?<ruta>[^\]|]+?)\s*\|\s*(?<texto>[^\]]+?)\s*\]\]")]
    private static partial Regex MarcaEnlace();

    [GeneratedRegex(@"^/publicacion/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex RutaPublicacion();

    /// <summary>Separa los botones [[ruta|Texto]] del texto y descarta las rutas que no son de la app.</summary>
    internal static RespuestaAsistenteDto Armar(string contenido)
    {
        var enlaces = new List<EnlaceAsistenteDto>();
        foreach (Match m in MarcaEnlace().Matches(contenido))
        {
            var ruta = m.Groups["ruta"].Value.Trim();
            var texto = m.Groups["texto"].Value.Trim();
            if (enlaces.Count < MaxEnlaces && texto.Length is > 0 and <= 40 && (RutasPermitidas.Contains(ruta) || RutaPublicacion().IsMatch(ruta))
                && enlaces.All(e => e.Ruta != ruta))
                enlaces.Add(new EnlaceAsistenteDto(texto, ruta));
        }
        var limpio = MarcaEnlace().Replace(contenido, "").Trim();
        if (limpio.Length == 0) limpio = "No encontré una buena respuesta. Si quieres, crea una PQRS y te respondemos por escrito.";
        return new RespuestaAsistenteDto(limpio, enlaces);
    }
}
