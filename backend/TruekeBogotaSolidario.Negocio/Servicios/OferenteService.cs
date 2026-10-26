using System.Text;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Registro de oferentes del Estatuto del Consumidor (Ley 1480, art. 53): quien vende por un portal de contacto queda
/// registrado con su nombre, documento, dirección de notificaciones y teléfono. Ese registro se entrega a quien le compró y
/// reclama, o a la autoridad; nunca a otros usuarios.
/// </summary>
public interface IOferenteService
{
    /// <summary>Registra o actualiza los datos. Documento, dirección y teléfono se guardan cifrados.</summary>
    Task<UsuarioDto> RegistrarAsync(Guid actorId, DatosOferenteRequest r);
    /// <summary>El registro del titular, o null si no lo tiene.</summary>
    Task<DatosOferenteDto?> ObtenerMioAsync(Guid actorId);
    /// <summary>El registro descifrado, o null (exportación de datos del titular).</summary>
    DatosOferenteDto? Descifrar(Usuario u);
    /// <summary>Quienes le vendieron al autor de la PQRS en compras completadas (sin datos del registro).</summary>
    Task<IReadOnlyList<VendedorDePqrDto>> ListarVendedoresDePqrAsync(Guid actorId, Guid pqrId);
    /// <summary>
    /// El registro de quien le vendió al autor de la PQRS. Exige una compra completada entre los dos y queda en la auditoría.
    /// Sin esa compra responde 404, igual que si no existiera.
    /// </summary>
    Task<DatosOferenteDto> ConsultarParaPqrAsync(Guid actorId, Guid pqrId, Guid vendedorId);
}

public sealed class OferenteService : IOferenteService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPqrRepository _pqrs;
    private readonly ISolicitudRepository _solicitudes;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly ICifradorSecretos _cifrador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<OferenteService> _log;

    public OferenteService(IUsuarioRepository usuarios, IPqrRepository pqrs, ISolicitudRepository solicitudes, IAuditoriaRepository auditoria,
        IUnidadDeTrabajo uow, ICifradorSecretos cifrador, TimeProvider reloj, ILogger<OferenteService> log)
    {
        _usuarios = usuarios; _pqrs = pqrs; _solicitudes = solicitudes; _auditoria = auditoria; _uow = uow; _cifrador = cifrador;
        _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<UsuarioDto> RegistrarAsync(Guid actorId, DatosOferenteRequest r)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(u);
        var tipo = (TipoDocumentoFiscal)(int)r.TipoDocumento;
        var documento = DocumentosFiscales.Normalizar(tipo, r.Documento);
        if (Divipola.ObtenerMunicipio(r.MunicipioCodigo) is null) throw new ReglaDeNegocioException("El municipio no es válido.");
        u.RegistrarOferente(tipo, r.Nombre, Cifrar(documento), Cifrar(FormatosTexto.Compactar(r.Direccion)), r.MunicipioCodigo,
            Cifrar(NormalizarTelefono(r.Telefono)), Ahora);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Registro de oferente guardado {UsuarioId}", u.Id);
        return Mapeos.AUsuarioDto(u, Ahora);
    }

    public async Task<DatosOferenteDto?> ObtenerMioAsync(Guid actorId)
        => Descifrar(await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida."));

    public DatosOferenteDto? Descifrar(Usuario u)
    {
        if (!u.RegistroOferenteCompleto) return null;
        return new DatosOferenteDto(u.OferenteTipoDocumento!.Value.ToString(), u.OferenteNombre ?? "", Descifrar(u.OferenteDocumentoCifrado!),
            Descifrar(u.OferenteDireccionCifrada!), u.OferenteMunicipioCodigo ?? "", Divipola.NombreCompleto(u.OferenteMunicipioCodigo),
            Descifrar(u.OferenteTelefonoCifrado!), u.OferenteRegistradoUtc!.Value);
    }

    public async Task<IReadOnlyList<VendedorDePqrDto>> ListarVendedoresDePqrAsync(Guid actorId, Guid pqrId)
    {
        var autor = await AutorDePqrAsync(actorId, pqrId);
        if (autor is null) return Array.Empty<VendedorDePqrDto>(); // PQRS pública: no hay compras vinculadas
        return (await ComprasCompletadasAsync(autor.Value))
            .GroupBy(s => s.Publicacion!.PropietarioId)
            .Select(g => g.OrderByDescending(s => s.FechaCierreUtc ?? s.FechaSolicitud).First())
            .Select(s => new VendedorDePqrDto(s.Publicacion!.PropietarioId, Mapeos.NombrePublico(s.Publicacion.Propietario?.NombreCompleto ?? ""),
                s.Publicacion.Titulo, s.FechaCierreUtc ?? s.FechaSolicitud, s.Publicacion.Propietario?.RegistroOferenteCompleto == true))
            .ToList();
    }

    public async Task<DatosOferenteDto> ConsultarParaPqrAsync(Guid actorId, Guid pqrId, Guid vendedorId)
    {
        var autor = await AutorDePqrAsync(actorId, pqrId);
        var compro = autor is not null && (await ComprasCompletadasAsync(autor.Value)).Any(s => s.Publicacion!.PropietarioId == vendedorId);
        var vendedor = compro ? await _usuarios.ObtenerPorIdAsync(vendedorId) : null;
        var datos = vendedor is null ? null : Descifrar(vendedor);
        if (datos is null) throw new NoEncontradoException("No hay un registro de vendedor vinculado a esta PQRS.");
        // Habeas data: cada consulta del registro queda con quién la hizo y para qué PQRS.
        _auditoria.Agregar(new AuditoriaEvento(actorId, "OFERENTE_CONSULTADO", "Usuario", vendedorId, $"PQRS {pqrId}", Ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogWarning("Registro de oferente {VendedorId} consultado por {ActorId} para la PQRS {PqrId}", vendedorId, actorId, pqrId);
        return datos;
    }

    /// <summary>Autor (con cuenta) de la PQRS; null si es pública. 404 si la PQRS no existe. Solo moderación.</summary>
    private async Task<Guid?> AutorDePqrAsync(Guid actorId, Guid pqrId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol is not (RolUsuarioEnum.Administrador or RolUsuarioEnum.SuperUsuario)) throw new AccesoDenegadoException();
        var pqr = await _pqrs.ObtenerAsync(pqrId) ?? throw new NoEncontradoException("PQRS no encontrada.");
        return pqr.UsuarioId;
    }

    /// <summary>Compras completadas (ajenas): una venta que se cerró como trueke no es una compra.</summary>
    private async Task<IEnumerable<Solicitud>> ComprasCompletadasAsync(Guid compradorId)
        => (await _solicitudes.ListarPorSolicitanteAsync(compradorId))
            .Where(s => s.Estado == EstadoSolicitud.Completada && s.Modo == ModoTransaccion.Compra && s.Publicacion is not null
                        && s.Publicacion.PropietarioId != compradorId);

    private string Cifrar(string texto) => _cifrador.Cifrar(Encoding.UTF8.GetBytes(texto));
    private string Descifrar(string cifrado) => Encoding.UTF8.GetString(_cifrador.Descifrar(cifrado));

    /// <summary>Solo los 10 dígitos nacionales, sin +57, espacios ni guiones.</summary>
    internal static string NormalizarTelefono(string telefono)
    {
        var digitos = new string(telefono.Where(char.IsAsciiDigit).ToArray());
        return digitos.Length == 12 && digitos.StartsWith("57", StringComparison.Ordinal) ? digitos[2..] : digitos;
    }
}
