using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Datos de facturación del usuario y sus facturas. La emisión ante la DIAN la registra el equipo (ver AdministracionService).</summary>
public interface IFacturacionService
{
    Task<DatosFacturacionDto> ObtenerDatosAsync(Guid actorId);
    Task<DatosFacturacionDto> ActualizarDatosAsync(Guid actorId, DatosFacturacionRequest r);
    /// <summary>Vuelve a facturar como "consumidor final".</summary>
    Task BorrarDatosAsync(Guid actorId);
    Task<IReadOnlyList<FacturaDto>> ListarMiasAsync(Guid actorId);
    /// <summary>Solo con Facturacion:Modo=Simulado (desarrollo): marca como emitidas las pendientes con datos ficticios.</summary>
    Task<int> EmitirSimuladasAsync(int maximo);
}

public sealed class FacturacionService : IFacturacionService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IFacturaRepository _facturas;
    private readonly IUnidadDeTrabajo _uow;
    private readonly FacturacionOpciones _opciones;
    private readonly TimeProvider _reloj;
    private readonly ILogger<FacturacionService> _log;

    public FacturacionService(IUsuarioRepository usuarios, IFacturaRepository facturas, IUnidadDeTrabajo uow, IOptions<FacturacionOpciones> opciones,
        TimeProvider reloj, ILogger<FacturacionService> log)
    {
        _usuarios = usuarios; _facturas = facturas; _uow = uow; _opciones = opciones.Value; _reloj = reloj; _log = log;
    }

    private async Task<Usuario> CargarAsync(Guid id) => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    public async Task<DatosFacturacionDto> ObtenerDatosAsync(Guid actorId) => Mapeos.ADatosFacturacion(await CargarAsync(actorId));

    public async Task<DatosFacturacionDto> ActualizarDatosAsync(Guid actorId, DatosFacturacionRequest r)
    {
        var u = await CargarAsync(actorId);
        u.ActualizarDatosFacturacion((TipoDocumentoFiscal)(int)r.TipoDocumento, r.Documento, r.Nombre, r.Correo, r.Direccion, r.MunicipioCodigo);
        await _uow.GuardarCambiosAsync();
        return Mapeos.ADatosFacturacion(u);
    }

    public async Task BorrarDatosAsync(Guid actorId)
    {
        var u = await CargarAsync(actorId);
        u.BorrarDatosFacturacion();
        await _uow.GuardarCambiosAsync();
    }

    public async Task<IReadOnlyList<FacturaDto>> ListarMiasAsync(Guid actorId)
        => (await _facturas.ListarPorUsuarioAsync(actorId, 200)).Select(Mapeos.AFacturaDto).ToList();

    public async Task<int> EmitirSimuladasAsync(int maximo)
    {
        if (!_opciones.EsSimulado) return 0;
        var (pendientes, _) = await _facturas.ListarPorEstadoAsync(EstadoFactura.Pendiente, 1, maximo);
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var emitidas = 0;
        foreach (var dto in pendientes)
        {
            var f = await _facturas.ObtenerAsync(dto.Id);
            if (f is null || f.Estado != EstadoFactura.Pendiente) continue;
            f.MarcarEmitida($"SIM-{ahora:yyyyMMddHHmmss}-{emitidas + 1}", "SIMULADO" + f.Id.ToString("N"), ahora);
            emitidas++;
        }
        if (emitidas > 0)
        {
            await _uow.GuardarCambiosAsync();
            _log.LogInformation("Facturación simulada: {Total} facturas marcadas como emitidas", emitidas);
        }
        return emitidas;
    }
}

/// <summary>CSV con separador ";" (Excel en español) y protección contra inyección de fórmulas.</summary>
internal static class Csv
{
    public static string Celda(object? valor)
    {
        var texto = valor switch
        {
            null => "",
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? ""
        };
        // Una celda que empieza con = + - @ se ejecutaría como fórmula al abrir el archivo en Excel.
        if (texto.Length > 0 && texto[0] is '=' or '+' or '-' or '@' or '\t' or '\r') texto = "'" + texto;
        return texto.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : texto;
    }

    public static void Fila(StringBuilder sb, params object?[] valores) => sb.AppendLine(string.Join(';', valores.Select(Celda)));
}
