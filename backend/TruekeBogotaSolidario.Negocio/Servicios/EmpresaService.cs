using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Empresas;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>El PDF de un informe para descargar.</summary>
public sealed record ArchivoInforme(byte[] Contenido, string Nombre);
/// <summary>El CSV de un informe (texto; Presentacion le agrega el BOM para Excel).</summary>
public sealed record CsvInforme(string Contenido, string Nombre);

/// <summary>
/// Empresas: una cuenta registra la empresa con su NIT y el certificado de la Cámara de Comercio, un SuperUsuario la verifica y,
/// aprobada, sus Admin invitan miembros y crean campañas. Los miembros ligan publicaciones a las campañas activas y un
/// Administrador de la plataforma emite los informes de impacto. Los datos de la empresa solo los ven sus miembros (y la
/// administración). No hay cobro en la plataforma: el servicio se acuerda por cotización.
/// </summary>
public interface IEmpresaService
{
    /// <summary>null si la cuenta no pertenece a ninguna empresa.</summary>
    Task<MiEmpresaDto?> MiaAsync(Guid actorId);
    Task<MiEmpresaDto> SolicitarAsync(Guid actorId, SolicitarEmpresaRequest r, CancellationToken ct = default);
    Task<InvitacionCreadaDto> InvitarAsync(Guid actorId, CrearInvitacionEmpresaRequest r);
    Task RevocarInvitacionAsync(Guid actorId, Guid invitacionId);
    Task<MiEmpresaDto> UnirseAsync(Guid actorId, UnirseEmpresaRequest r);
    Task<MiEmpresaDto> CambiarRolAsync(Guid actorId, Guid miembroId, RolMiembroEmpresaDto rol);
    Task QuitarMiembroAsync(Guid actorId, Guid miembroId);
    Task SalirAsync(Guid actorId);
    Task<CampanaDto> CrearCampanaAsync(Guid actorId, GuardarCampanaRequest r);
    Task<CampanaDto> EditarCampanaAsync(Guid actorId, Guid campanaId, GuardarCampanaRequest r);
    /// <summary>Para el selector al publicar: las campañas activas hoy de la empresa de la cuenta (vacío si no tiene).</summary>
    Task<IReadOnlyList<CampanaDto>> CampanasActivasAsync(Guid actorId);
    /// <summary>Exige que la campaña esté activa y sea de una empresa verificada a la que pertenece la cuenta.</summary>
    Task ExigirCampanaParaPublicarAsync(Guid actorId, Guid campanaId);
    /// <summary>Solo miembros de la empresa del informe.</summary>
    Task<ArchivoInforme> InformePdfAsync(Guid actorId, Guid informeId);
    Task<CsvInforme> InformeCsvAsync(Guid actorId, Guid informeId);
    /// <summary>Al suprimir la cuenta (Ley 1581): quita su membresía sin guardar (lo guarda quien llama).</summary>
    Task QuitarDeCuentaAsync(Guid usuarioId);

    // Administración (Administrador; verificar, rechazar y retirar solo SuperUsuario)
    Task<IReadOnlyList<EmpresaAdminDto>> ListarAdminAsync(Guid actorId, EstadoEmpresaDto? estado);
    Task<EmpresaAdminDetalleDto> DetalleAdminAsync(Guid actorId, Guid empresaId);
    Task AprobarAsync(Guid actorId, Guid empresaId);
    Task RechazarAsync(Guid actorId, Guid empresaId, string motivo);
    Task RetirarAsync(Guid actorId, Guid empresaId, string motivo);
    Task<InformeImpactoDto> EmitirInformeAsync(Guid actorId, Guid empresaId, EmitirInformeRequest r);
    Task<ArchivoInforme> InformePdfAdminAsync(Guid actorId, Guid informeId);
    Task<CsvInforme> InformeCsvAdminAsync(Guid actorId, Guid informeId);
}

public sealed class EmpresaService : IEmpresaService
{
    private const int MaxRevision = 200;
    public const int MaxInvitacionesPendientes = 20;
    public const int MaxDiasInforme = 366;

    private readonly IEmpresaRepository _empresas;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICategoriaRepository _categorias;
    private readonly IImpactoRepository _impacto;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAlmacenArchivos _almacen;
    private readonly INotificador _notificador;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly ILogger<EmpresaService> _log;

    public EmpresaService(IEmpresaRepository empresas, IUsuarioRepository usuarios, ICategoriaRepository categorias, IImpactoRepository impacto,
        IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, IAlmacenArchivos almacen, INotificador notificador, IOptions<UrlsOpciones> urls,
        TimeProvider reloj, ILogger<EmpresaService> log)
    {
        _empresas = empresas; _usuarios = usuarios; _categorias = categorias; _impacto = impacto; _auditoria = auditoria; _uow = uow;
        _almacen = almacen; _notificador = notificador; _urls = urls.Value; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;
    private DateOnly Hoy => Usuario.HoyEnColombia(Ahora);

    // ---------------- La propia empresa ----------------
    public async Task<MiEmpresaDto?> MiaAsync(Guid actorId)
        => await _empresas.MembresiaAsync(actorId) is var (m, e) ? await AMiaAsync(e, m, actorId) : null;

    public async Task<MiEmpresaDto> SolicitarAsync(Guid actorId, SolicitarEmpresaRequest r, CancellationToken ct = default)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(u, Ahora);
        if (!r.DeclaroRepresentacion)
            throw new ReglaDeNegocioException("Confirma que representas legalmente a la empresa o que su representante te autorizó.");
        var nit = Empresa.NormalizarNitJuridica(r.Nit);

        var membresia = await _empresas.MembresiaAsync(actorId);
        if (membresia is var (miembro, actual))
        {
            if (!miembro.EsAdmin) throw new ReglaDeNegocioException("Ya perteneces a una empresa: solo sus Admin pueden actualizar el registro.");
            if (actual.Estado == EstadoEmpresa.Pendiente) throw new ReglaDeNegocioException("Ya hay una solicitud en revisión.");
            if (actual.Estado == EstadoEmpresa.Aprobada) throw new ReglaDeNegocioException("La empresa ya está verificada.");
        }
        var empresa = membresia?.Empresa;
        if (await _empresas.NitEnUsoAsync(nit, empresa?.Id ?? Guid.Empty))
            throw new ReglaDeNegocioException("Otra empresa ya registró ese NIT. Si es un error, escríbenos por PQRS.");

        var certificado = r.CertificadoUrl;
        if (_almacen.Habilitado) certificado = await _almacen.ValidarArchivoPropioAsync(r.CertificadoUrl, actorId, TipoArchivoDto.Documento, ct);
        else ValidadorUrls.ExigirHostPermitido(r.CertificadoUrl, _urls.HostsPermitidosDocumentos, "El certificado");

        if (empresa is null)
        {
            empresa = new Empresa(actorId, Ahora);
            _empresas.Agregar(empresa);
            _empresas.AgregarMiembro(MiembroEmpresa.Fundador(empresa.Id, actorId, Ahora));
        }
        empresa.Solicitar(r.RazonSocial, nit, certificado, Ahora);
        Auditar(actorId, "EMPRESA_SOLICITADA", empresa.Id, empresa.Nit);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Solicitud de empresa {EmpresaId} de {UsuarioId}", empresa.Id, actorId);

        foreach (var super in await _usuarios.ListarIdsSuperUsuariosAsync())
            if (super != actorId)
                await _notificador.NotificarAsync(super, TiposNotificacion.EmpresaPorRevisar, $"La empresa \"{empresa.RazonSocial}\" pidió su verificación.", empresa.Id);
        return (await MiaAsync(actorId))!;
    }

    public async Task<InvitacionCreadaDto> InvitarAsync(Guid actorId, CrearInvitacionEmpresaRequest r)
    {
        var (_, empresa) = await AdminDeAprobadaAsync(actorId);
        var todos = await _empresas.MiembrosAsync(empresa.Id);
        if (todos.Count(m => m.EsActivo) >= MiembroEmpresa.MaxMiembros)
            throw new ReglaDeNegocioException($"La empresa ya tiene {MiembroEmpresa.MaxMiembros} miembros.");
        if (todos.Count(m => m.InvitacionVigente(Ahora)) >= MaxInvitacionesPendientes)
            throw new ReglaDeNegocioException($"Hay {MaxInvitacionesPendientes} invitaciones sin usar: revoca las que ya no sirvan.");
        foreach (var vencida in todos.Where(m => !m.EsActivo && !m.InvitacionVigente(Ahora))) _empresas.QuitarMiembro(vencida);

        var codigo = Base64Url(RandomNumberGenerator.GetBytes(32));
        var invitacion = MiembroEmpresa.Invitacion(empresa.Id, (RolMiembroEmpresa)(int)r.Rol, HashCodigo(codigo), actorId, Ahora);
        _empresas.AgregarMiembro(invitacion);
        Auditar(actorId, "EMPRESA_INVITACION", empresa.Id, r.Rol.ToString());
        await _uow.GuardarCambiosAsync();
        return new InvitacionCreadaDto(invitacion.Id, codigo, r.Rol, invitacion.ExpiraUtc!.Value);
    }

    public async Task RevocarInvitacionAsync(Guid actorId, Guid invitacionId)
    {
        var (_, empresa) = await AdminDeAprobadaAsync(actorId);
        var inv = (await _empresas.MiembrosAsync(empresa.Id)).FirstOrDefault(m => m.Id == invitacionId && !m.EsActivo)
            ?? throw new NoEncontradoException("Invitación no encontrada.");
        _empresas.QuitarMiembro(inv);
        await _uow.GuardarCambiosAsync();
    }

    public async Task<MiEmpresaDto> UnirseAsync(Guid actorId, UnirseEmpresaRequest r)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(u);
        if (await _empresas.MembresiaAsync(actorId) is not null)
            throw new ReglaDeNegocioException("Ya perteneces a una empresa. Sal de ella antes de unirte a otra.");
        // Mismo mensaje si el código no existe o venció: no se revela qué códigos existen.
        var inv = await _empresas.InvitacionPorCodigoAsync(HashCodigo(r.Codigo));
        var empresa = inv is null ? null : await _empresas.ObtenerAsync(inv.EmpresaId);
        if (inv is null || empresa is null || !empresa.EstaAprobada || !inv.InvitacionVigente(Ahora))
            throw new ReglaDeNegocioException("La invitación venció o ya se usó. Pide una nueva.");
        if ((await _empresas.MiembrosAsync(empresa.Id)).Count(m => m.EsActivo) >= MiembroEmpresa.MaxMiembros)
            throw new ReglaDeNegocioException($"La empresa ya tiene {MiembroEmpresa.MaxMiembros} miembros.");
        inv.Aceptar(actorId, Ahora);
        Auditar(actorId, "EMPRESA_MIEMBRO_UNIDO", empresa.Id, inv.Rol.ToString());
        await _uow.GuardarCambiosAsync();
        await _notificador.NotificarAsync(inv.InvitadoPorId, TiposNotificacion.EmpresaMiembroNuevo,
            $"{Mapeos.NombrePublico(u.NombreCompleto)} se unió a {empresa.RazonSocial}.", empresa.Id);
        return (await MiaAsync(actorId))!;
    }

    public async Task<MiEmpresaDto> CambiarRolAsync(Guid actorId, Guid miembroId, RolMiembroEmpresaDto rol)
    {
        var (_, empresa) = await AdminDeAprobadaAsync(actorId);
        var miembros = await _empresas.MiembrosAsync(empresa.Id);
        var m = miembros.FirstOrDefault(x => x.Id == miembroId && x.EsActivo) ?? throw new NoEncontradoException("Miembro no encontrado.");
        var nuevo = (RolMiembroEmpresa)(int)rol;
        if (m.EsAdmin && nuevo != RolMiembroEmpresa.Admin && miembros.Count(x => x.EsAdmin) == 1)
            throw new ReglaDeNegocioException("La empresa necesita al menos un Admin.");
        m.CambiarRol(nuevo);
        Auditar(actorId, "EMPRESA_ROL", empresa.Id, $"{m.UsuarioId}: {nuevo}");
        await _uow.GuardarCambiosAsync();
        return (await MiaAsync(actorId))!;
    }

    public async Task QuitarMiembroAsync(Guid actorId, Guid miembroId)
    {
        var (yo, empresa) = await AdminDeAprobadaAsync(actorId);
        if (miembroId == yo.Id) throw new ReglaDeNegocioException("Para salir de la empresa usa \"Salir\".");
        var m = (await _empresas.MiembrosAsync(empresa.Id)).FirstOrDefault(x => x.Id == miembroId && x.EsActivo)
            ?? throw new NoEncontradoException("Miembro no encontrado.");
        _empresas.QuitarMiembro(m); // sus publicaciones ya ligadas siguen en la campaña (el impacto ya ocurrió con la empresa)
        Auditar(actorId, "EMPRESA_MIEMBRO_QUITADO", empresa.Id, m.UsuarioId?.ToString());
        await _uow.GuardarCambiosAsync();
    }

    public async Task SalirAsync(Guid actorId)
    {
        var (m, empresa) = await _empresas.MembresiaAsync(actorId) ?? throw new NoEncontradoException("No perteneces a ninguna empresa.");
        if (m.EsAdmin && (await _empresas.MiembrosAsync(empresa.Id)).Count(x => x.EsAdmin) == 1)
            throw new ReglaDeNegocioException("Eres el único Admin: nombra a otra persona como Admin antes de salir. Si la empresa ya no usará la plataforma, escríbenos por PQRS.");
        _empresas.QuitarMiembro(m);
        Auditar(actorId, "EMPRESA_SALIDA", empresa.Id, null);
        await _uow.GuardarCambiosAsync();
    }

    public async Task QuitarDeCuentaAsync(Guid usuarioId)
    {
        if (await _empresas.MembresiaAsync(usuarioId) is var (m, _)) _empresas.QuitarMiembro(m);
    }

    // ---------------- Campañas ----------------
    public async Task<CampanaDto> CrearCampanaAsync(Guid actorId, GuardarCampanaRequest r)
    {
        var (_, empresa) = await AdminDeAprobadaAsync(actorId);
        var c = new Campana(empresa.Id, r.Nombre, r.Inicio, r.Fin, actorId, Ahora);
        _empresas.AgregarCampana(c);
        Auditar(actorId, "CAMPANA_CREADA", empresa.Id, c.Nombre);
        await _uow.GuardarCambiosAsync();
        return ACampanaDto(c, null);
    }

    public async Task<CampanaDto> EditarCampanaAsync(Guid actorId, Guid campanaId, GuardarCampanaRequest r)
    {
        var (_, empresa) = await AdminDeAprobadaAsync(actorId);
        var c = await _empresas.CampanaAsync(campanaId);
        if (c is null || c.EmpresaId != empresa.Id) throw new NoEncontradoException("Campaña no encontrada.");
        c.Editar(r.Nombre, r.Inicio, r.Fin, Hoy);
        await _uow.GuardarCambiosAsync();
        return ACampanaDto(c, (await _empresas.AvanceCampanasAsync(empresa.Id)).GetValueOrDefault(c.Id));
    }

    public async Task<IReadOnlyList<CampanaDto>> CampanasActivasAsync(Guid actorId)
    {
        if (await _empresas.MembresiaAsync(actorId) is not var (_, empresa) || !empresa.EstaAprobada) return [];
        return (await _empresas.CampanasAsync(empresa.Id)).Where(c => c.EstaActiva(Hoy)).Select(c => ACampanaDto(c, null)).ToList();
    }

    public async Task ExigirCampanaParaPublicarAsync(Guid actorId, Guid campanaId)
    {
        var c = await _empresas.CampanaAsync(campanaId);
        if (c is null || await _empresas.MembresiaAsync(actorId) is not var (_, empresa) || !empresa.EstaAprobada || c.EmpresaId != empresa.Id
            || !c.EstaActiva(Hoy))
            throw new ReglaDeNegocioException("Esa campaña no está activa o no es de tu empresa.");
    }

    // ---------------- Informes (miembros) ----------------
    public async Task<ArchivoInforme> InformePdfAsync(Guid actorId, Guid informeId) => Pdf(await InformeDeMiEmpresaAsync(actorId, informeId));
    public async Task<CsvInforme> InformeCsvAsync(Guid actorId, Guid informeId) => Csv(await InformeDeMiEmpresaAsync(actorId, informeId));

    private async Task<InformeImpacto> InformeDeMiEmpresaAsync(Guid actorId, Guid informeId)
    {
        var informe = await _empresas.InformeAsync(informeId);
        // Mismo 404 si no existe o es de otra empresa: no se revela qué informes existen.
        if (informe is null || await _empresas.MembresiaAsync(actorId) is not var (_, empresa) || empresa.Id != informe.EmpresaId)
            throw new NoEncontradoException("Informe no encontrado.");
        return informe;
    }

    private static ArchivoInforme Pdf(InformeImpacto i)
        => new(InformePdf.Generar(ContenidoInformes.Leer(i.Contenido), i.Id, i.Huella), $"informe-impacto-{i.Desde:yyyyMMdd}-{i.Hasta:yyyyMMdd}.pdf");

    private static CsvInforme Csv(InformeImpacto i)
        => new(InformeCsv.Generar(ContenidoInformes.Leer(i.Contenido)), $"informe-impacto-{i.Desde:yyyyMMdd}-{i.Hasta:yyyyMMdd}.csv");

    // ---------------- Administración ----------------
    public async Task<IReadOnlyList<EmpresaAdminDto>> ListarAdminAsync(Guid actorId, EstadoEmpresaDto? estado)
    {
        var esSuper = await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var lista = await _empresas.PorEstadoAsync(estado is null ? null : (EstadoEmpresa)(int)estado, MaxRevision);
        var resultado = new List<EmpresaAdminDto>(lista.Count);
        foreach (var e in lista) resultado.Add(await AAdminDtoAsync(e, esSuper));
        return resultado;
    }

    public async Task<EmpresaAdminDetalleDto> DetalleAdminAsync(Guid actorId, Guid empresaId)
    {
        var esSuper = await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var e = await _empresas.ObtenerAsync(empresaId) ?? throw new NoEncontradoException("Empresa no encontrada.");
        var (campanas, informes) = await CampanasEInformesAsync(e.Id);
        return new EmpresaAdminDetalleDto(await AAdminDtoAsync(e, esSuper), campanas, informes);
    }

    public async Task AprobarAsync(Guid actorId, Guid empresaId)
    {
        var e = await ParaDecidirAsync(actorId, empresaId);
        var certificado = e.CertificadoUrl;
        if (await _empresas.NitEnUsoAsync(e.Nit, e.Id)) // dos solicitudes simultáneas con el mismo NIT
            throw new ReglaDeNegocioException("Otra empresa ya registró ese NIT.");
        e.Aprobar(Ahora);
        Auditar(actorId, "EMPRESA_APROBADA", e.Id, e.Nit);
        await _uow.GuardarCambiosAsync();
        if (certificado is not null) await _almacen.EliminarDocumentoAsync(certificado);
        await AvisarAdminsAsync(e, TiposNotificacion.EmpresaAprobada, $"{e.RazonSocial} fue verificada: ya puedes invitar a tu equipo y crear campañas.");
    }

    public async Task RechazarAsync(Guid actorId, Guid empresaId, string motivo)
    {
        var e = await ParaDecidirAsync(actorId, empresaId);
        var certificado = e.CertificadoUrl;
        e.Rechazar(motivo, Ahora);
        Auditar(actorId, "EMPRESA_RECHAZADA", e.Id, e.MotivoDecision);
        await _uow.GuardarCambiosAsync();
        if (certificado is not null) await _almacen.EliminarDocumentoAsync(certificado);
        await AvisarAdminsAsync(e, TiposNotificacion.EmpresaRechazada, "No pudimos verificar tu empresa. Revisa el motivo en tu cuenta y vuelve a intentarlo.");
    }

    public async Task RetirarAsync(Guid actorId, Guid empresaId, string motivo)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        var e = await _empresas.ObtenerAsync(empresaId) ?? throw new NoEncontradoException("Empresa no encontrada.");
        await ExigirNoPropiaAsync(actorId, e.Id);
        e.Retirar(motivo, Ahora);
        Auditar(actorId, "EMPRESA_RETIRADA", e.Id, e.MotivoDecision);
        await _uow.GuardarCambiosAsync();
        _log.LogWarning("Empresa {EmpresaId} retirada por {ActorId}", e.Id, actorId);
        await AvisarAdminsAsync(e, TiposNotificacion.EmpresaRetirada, "Se retiró la verificación de tu empresa. Revisa el motivo en tu cuenta.");
    }

    public async Task<InformeImpactoDto> EmitirInformeAsync(Guid actorId, Guid empresaId, EmitirInformeRequest r)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var empresa = await _empresas.ObtenerAsync(empresaId) ?? throw new NoEncontradoException("Empresa no encontrada.");
        if (!empresa.EstaAprobada) throw new ReglaDeNegocioException("Solo se emiten informes de empresas verificadas.");
        await ExigirNoPropiaAsync(actorId, empresa.Id);
        if (r.Hasta < r.Desde) throw new ReglaDeNegocioException("El periodo no puede terminar antes de empezar.");
        if (r.Hasta > Hoy) throw new ReglaDeNegocioException("El periodo no puede incluir días que aún no terminan de pasar.");
        if (r.Hasta.DayNumber - r.Desde.DayNumber + 1 > MaxDiasInforme)
            throw new ReglaDeNegocioException($"Un informe cubre como mucho {MaxDiasInforme} días.");
        var campanas = await _empresas.CampanasAsync(empresa.Id);
        Campana? campana = null;
        if (r.CampanaId is { } id)
            campana = campanas.FirstOrDefault(c => c.Id == id) ?? throw new ReglaDeNegocioException("La campaña no es de esta empresa.");

        var (desdeUtc, hastaUtc) = ContenidoInformes.RangoUtc(r.Desde, r.Hasta);
        var registros = await _empresas.RegistrosParaInformeAsync(empresa.Id, campana?.Id, desdeUtc, hastaUtc);
        if (registros.Count == 0) throw new ReglaDeNegocioException("No hay entregas confirmadas de sus campañas en ese periodo.");
        var categorias = (await _categorias.ListarAsync()).ToDictionary(c => c.Id, c => c.NombreCategoria);
        var contenido = ContenidoInformes.Construir(empresa, campana, campanas.ToDictionary(c => c.Id, c => c.Nombre), r.Desde, r.Hasta, Ahora,
            registros, categorias, await _impacto.ListarFactoresAsync());
        var json = ContenidoInformes.Serializar(contenido);
        var informe = new InformeImpacto(empresa.Id, campana?.Id, r.Desde, r.Hasta, actorId, Ahora, string.Join(", ", contenido.Versiones), json,
            ContenidoInformes.Huella(json));
        _empresas.AgregarInforme(informe);
        Auditar(actorId, "INFORME_IMPACTO_EMITIDO", empresa.Id, $"{informe.Id} {r.Desde:yyyy-MM-dd}..{r.Hasta:yyyy-MM-dd}: {registros.Count} registros");
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Informe de impacto {InformeId} de la empresa {EmpresaId} emitido por {ActorId}", informe.Id, empresa.Id, actorId);
        foreach (var m in (await _empresas.MiembrosAsync(empresa.Id)).Where(m => m.EsActivo))
            await _notificador.NotificarAsync(m.UsuarioId!.Value, TiposNotificacion.InformeImpactoEmitido,
                $"Ya está el informe de impacto de {empresa.RazonSocial} ({r.Desde:dd/MM/yyyy}–{r.Hasta:dd/MM/yyyy}). Descárgalo desde tu empresa.", informe.Id);
        return AInformeDto(new InformeResumen(informe.Id, informe.EmpresaId, informe.CampanaId, informe.Desde, informe.Hasta, informe.EmitidoUtc,
            informe.Versiones, informe.Huella), campana?.Nombre);
    }

    public async Task<ArchivoInforme> InformePdfAdminAsync(Guid actorId, Guid informeId) => Pdf(await InformeAdminAsync(actorId, informeId));
    public async Task<CsvInforme> InformeCsvAdminAsync(Guid actorId, Guid informeId) => Csv(await InformeAdminAsync(actorId, informeId));

    private async Task<InformeImpacto> InformeAdminAsync(Guid actorId, Guid informeId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        return await _empresas.InformeAsync(informeId) ?? throw new NoEncontradoException("Informe no encontrado.");
    }

    // ---------------- Apoyo ----------------
    /// <summary>SHA-256 del código: en la base nunca queda el código en claro.</summary>
    internal static string HashCodigo(string codigo)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("trueke-empresa:" + (codigo ?? "").Trim()))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<(MiembroEmpresa, Empresa)> AdminDeAprobadaAsync(Guid actorId)
    {
        var (m, e) = await _empresas.MembresiaAsync(actorId) ?? throw new NoEncontradoException("No perteneces a ninguna empresa.");
        if (!m.EsAdmin) throw new AccesoDenegadoException();
        if (!e.EstaAprobada) throw new ReglaDeNegocioException("Esto está disponible cuando la empresa está verificada.");
        return (m, e);
    }

    /// <summary>Defensa en profundidad contra la base de datos. Devuelve si el actor es SuperUsuario.</summary>
    private async Task<bool> ExigirRolAsync(Guid actorId, RolUsuarioEnum minimo)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < minimo) throw new AccesoDenegadoException();
        return actor.Rol >= RolUsuarioEnum.SuperUsuario;
    }

    /// <summary>Nadie resuelve ni emite informes de la empresa a la que pertenece.</summary>
    private async Task ExigirNoPropiaAsync(Guid actorId, Guid empresaId)
    {
        if (await _empresas.MembresiaAsync(actorId) is var (_, propia) && propia.Id == empresaId)
            throw new ReglaDeNegocioException("No puedes resolver asuntos de la empresa a la que perteneces.");
    }

    private async Task<Empresa> ParaDecidirAsync(Guid actorId, Guid empresaId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        var e = await _empresas.ObtenerAsync(empresaId) ?? throw new NoEncontradoException("Empresa no encontrada.");
        await ExigirNoPropiaAsync(actorId, e.Id);
        return e;
    }

    private async Task AvisarAdminsAsync(Empresa e, string tipo, string mensaje)
    {
        foreach (var m in (await _empresas.MiembrosAsync(e.Id)).Where(m => m.EsAdmin))
            await _notificador.NotificarAsync(m.UsuarioId!.Value, tipo, mensaje, e.Id);
    }

    private void Auditar(Guid actorId, string accion, Guid empresaId, string? detalle)
        => _auditoria.Agregar(new AuditoriaEvento(actorId, accion, "Empresa", empresaId, detalle is { Length: > 300 } ? detalle[..300] : detalle, Ahora));

    private CampanaDto ACampanaDto(Campana c, AvanceCampana? a)
        => new(c.Id, c.Nombre, c.Inicio, c.Fin, c.EstaActiva(Hoy), a?.Publicaciones ?? 0, a?.Objetos ?? 0, a?.Kg ?? 0, a?.KgCo2 ?? 0);

    private static InformeImpactoDto AInformeDto(InformeResumen i, string? campana)
        => new(i.Id, i.CampanaId, campana, i.Desde, i.Hasta, i.EmitidoUtc, i.Versiones, i.Huella);

    private async Task<(IReadOnlyList<CampanaDto>, IReadOnlyList<InformeImpactoDto>)> CampanasEInformesAsync(Guid empresaId)
    {
        var campanas = await _empresas.CampanasAsync(empresaId);
        var avance = await _empresas.AvanceCampanasAsync(empresaId);
        var nombres = campanas.ToDictionary(c => c.Id, c => c.Nombre);
        var informes = (await _empresas.InformesAsync(empresaId))
            .Select(i => AInformeDto(i, i.CampanaId is { } c ? nombres.GetValueOrDefault(c) : null)).ToList();
        return (campanas.Select(c => ACampanaDto(c, avance.GetValueOrDefault(c.Id))).ToList(), informes);
    }

    private async Task<MiEmpresaDto> AMiaAsync(Empresa e, MiembroEmpresa yo, Guid actorId)
    {
        var todos = await _empresas.MiembrosAsync(e.Id);
        var miembros = new List<MiembroEmpresaDto>();
        foreach (var m in todos.Where(m => m.EsActivo))
            if (await _usuarios.ObtenerPorIdAsync(m.UsuarioId!.Value) is { } u)
                miembros.Add(new MiembroEmpresaDto(m.Id, u.Id, Mapeos.NombrePublico(u.NombreCompleto), Mapeos.FotoVisible(u),
                    (RolMiembroEmpresaDto)(int)m.Rol, m.FechaIngresoUtc, u.Id == actorId));
        var invitaciones = yo.EsAdmin
            ? todos.Where(m => !m.EsActivo).Select(m => new InvitacionEmpresaDto(m.Id, (RolMiembroEmpresaDto)(int)m.Rol, m.FechaInvitacionUtc, m.ExpiraUtc,
                !m.InvitacionVigente(Ahora))).ToList()
            : [];
        var (campanas, informes) = await CampanasEInformesAsync(e.Id);
        return new MiEmpresaDto(e.Id, (EstadoEmpresaDto)(int)e.Estado, e.RazonSocial, e.Nit, e.MotivoDecision, e.FechaSolicitudUtc, e.FechaDecisionUtc,
            (RolMiembroEmpresaDto)(int)yo.Rol, miembros, invitaciones, campanas, informes);
    }

    private async Task<EmpresaAdminDto> AAdminDtoAsync(Empresa e, bool esSuper)
    {
        var solicitante = await _usuarios.ObtenerPorIdAsync(e.SolicitadaPorId);
        // El certificado vive en un bucket privado: el enlace caduca en 5 minutos
        var enlace = esSuper && e.CertificadoUrl is not null ? await _almacen.UrlLecturaTemporalAsync(e.CertificadoUrl) : null;
        var miembros = (await _empresas.MiembrosAsync(e.Id)).Count(m => m.EsActivo);
        return new EmpresaAdminDto(e.Id, e.RazonSocial, e.Nit, (EstadoEmpresaDto)(int)e.Estado, enlace, e.MotivoDecision, e.FechaSolicitudUtc,
            e.FechaDecisionUtc, solicitante?.NombreCompleto ?? "Cuenta eliminada", esSuper ? solicitante?.Correo : null, miembros);
    }
}
