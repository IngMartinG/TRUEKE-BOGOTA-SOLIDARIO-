import { inject, Injectable } from '@angular/core';
import type {
  AccionDenunciaDto,
  ApelacionAdminDto,
  DenunciaAgrupadaDto,
  EstadoApelacionDto,
  EstadoDenunciaDto,
  CorregirCompradorFacturaRequest,
  EstadoFacturaDto,
  EstadoPagoDto,
  EstadoPqrDto,
  FacturaAdminDto,
  FacturaAdminDtoPaginaDto,
  IngresosDto,
  PqrAdminDto,
  PqrAdminDtoPaginaDto,
  PagoAdminDto,
  PagoAdminDtoPaginaDto,
  RolDto,
  UsuarioAdminDto,
  UsuarioAdminDtoPaginaDto,
  UsuarioDto,
  VerificacionPendienteDto,
  FotosEnRevisionDto,
  DatosOferenteDto,
  VendedorDePqrDto,
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly api = inject(HttpApi);

  usuarios = (texto: string, soloSuspendidos: boolean, pagina = 1, tamano = 20, soloMenorDetectado = false) =>
    this.api.get<UsuarioAdminDtoPaginaDto>('/admin/usuarios', {
      params: { texto, soloSuspendidos: soloSuspendidos || undefined, soloMenorDetectado: soloMenorDetectado || undefined, pagina, tamano },
    });
  descartarMenor = (id: string, motivo: string) =>
    this.api.post<UsuarioAdminDto>(`/admin/usuarios/${seg(id)}/descartar-menor`, { motivo });
  suspender = (id: string, motivo: string, dias: number | null) =>
    this.api.post<UsuarioAdminDto>(`/admin/usuarios/${seg(id)}/suspender`, { motivo, dias });
  reactivar = (id: string) => this.api.post<UsuarioAdminDto>(`/admin/usuarios/${seg(id)}/reactivar`);
  /** Solo SuperUsuario: elimina (anonimiza) la cuenta de otra persona; el motivo queda en la auditoría. */
  eliminarCuenta = (id: string, motivo: string) => this.api.post<void>(`/admin/usuarios/${seg(id)}/eliminar`, { motivo });
  cambiarRol = (id: string, rol: RolDto) => this.api.patch<UsuarioDto>(`/admin/usuarios/${seg(id)}/rol`, { rol });

  denuncias = (estado: EstadoDenunciaDto) =>
    this.api.get<DenunciaAgrupadaDto[]>('/admin/denuncias', { params: { estado } });
  resolverDenuncia = (id: string, accion: AccionDenunciaDto, nota: string | null) =>
    this.api.post<void>(`/admin/denuncias/${seg(id)}/resolver`, { accion, nota });
  apelaciones = (estado: EstadoApelacionDto) =>
    this.api.get<ApelacionAdminDto[]>('/admin/apelaciones', { params: { estado } });
  resolverApelacion = (id: string, aceptar: boolean, nota: string) =>
    this.api.post<void>(`/admin/apelaciones/${seg(id)}/resolver`, { aceptar, nota });

  ocultarPublicacion = (id: string, motivo: string) =>
    this.api.post<void>(`/admin/publicaciones/${seg(id)}/ocultar`, { motivo });
  mostrarPublicacion = (id: string) => this.api.post<void>(`/admin/publicaciones/${seg(id)}/mostrar`);
  ocultarComentario = (id: string, motivo: string) =>
    this.api.post<void>(`/admin/comentarios/${seg(id)}/ocultar`, { motivo });
  mostrarComentario = (id: string) => this.api.post<void>(`/admin/comentarios/${seg(id)}/mostrar`);

  verificaciones = () => this.api.get<VerificacionPendienteDto[]>('/admin/verificaciones');
  fotosEnRevision = () => this.api.get<FotosEnRevisionDto>('/admin/fotos');
  aprobarFoto = (id: string) => this.api.post<void>(`/admin/fotos/${seg(id)}/aprobar`);
  rechazarFoto = (id: string, motivo: string) => this.api.post<void>(`/admin/fotos/${seg(id)}/rechazar`, { motivo });
  aprobarVerificacion = (id: string) => this.api.post<void>(`/admin/verificaciones/${seg(id)}/aprobar`);
  rechazarVerificacion = (id: string, motivo: string) =>
    this.api.post<void>(`/admin/verificaciones/${seg(id)}/rechazar`, { motivo });

  pagos = (estado: EstadoPagoDto | null, pagina = 1, tamano = 20) =>
    this.api.get<PagoAdminDtoPaginaDto>('/admin/pagos', { params: { estado, pagina, tamano } });
  marcarReembolsado = (ref: string, nota: string) =>
    this.api.post<PagoAdminDto>(`/admin/pagos/${seg(ref)}/reembolsado`, { nota });

  facturas = (estado: EstadoFacturaDto, pagina = 1, tamano = 20) =>
    this.api.get<FacturaAdminDtoPaginaDto>('/admin/facturas', { params: { estado, pagina, tamano } });
  marcarFacturaEmitida = (id: string, numeroDian: string, cufe: string) =>
    this.api.post<FacturaAdminDto>(`/admin/facturas/${seg(id)}/emitida`, { numeroDian, cufe }, { silencioso: true });
  /** Solo SuperUsuario: corrige el comprador (pendiente) o reemplaza la factura (emitida). Devuelve la vigente. */
  corregirCompradorFactura = (id: string, r: CorregirCompradorFacturaRequest) =>
    this.api.put<FacturaAdminDto>(`/admin/facturas/${seg(id)}/comprador`, r, { silencioso: true });
  facturasCsv = (estado: EstadoFacturaDto) => this.api.archivo('/admin/facturas.csv', { params: { estado } });

  pqr = (estado: EstadoPqrDto, pagina = 1, tamano = 20) =>
    this.api.get<PqrAdminDtoPaginaDto>('/admin/pqr', { params: { estado, pagina, tamano } });
  /** Quienes le vendieron al autor de la PQRS (sin sus datos) y, aparte, el registro de uno (queda en la auditoría). */
  vendedoresDePqr = (id: string) => this.api.get<VendedorDePqrDto[]>(`/admin/pqr/${seg(id)}/vendedores`);
  registroVendedorDePqr = (id: string, usuarioId: string) =>
    this.api.get<DatosOferenteDto>(`/admin/pqr/${seg(id)}/vendedores/${seg(usuarioId)}/oferente`);
  pqrEnTramite = (id: string) => this.api.post<PqrAdminDto>(`/admin/pqr/${seg(id)}/en-tramite`, {});
  responderPqr = (id: string, respuesta: string) =>
    this.api.post<PqrAdminDto>(`/admin/pqr/${seg(id)}/responder`, { respuesta }, { silencioso: true });

  ingresos = (desde?: string | null, hasta?: string | null) =>
    this.api.get<IngresosDto>('/admin/ingresos', { params: { desde, hasta } });
  ingresosCsv = (desde?: string | null, hasta?: string | null) =>
    this.api.archivo('/admin/ingresos.csv', { params: { desde, hasta } });
}
