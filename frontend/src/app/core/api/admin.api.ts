import { inject, Injectable } from '@angular/core';
import type {
  AccionDenunciaDto,
  ApelacionAdminDto,
  DenunciaAgrupadaDto,
  EstadoApelacionDto,
  EstadoDenunciaDto,
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
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly api = inject(HttpApi);

  usuarios = (texto: string, soloSuspendidos: boolean, pagina = 1, tamano = 20) =>
    this.api.get<UsuarioAdminDtoPaginaDto>('/admin/usuarios', {
      params: { texto, soloSuspendidos: soloSuspendidos || undefined, pagina, tamano },
    });
  suspender = (id: string, motivo: string, dias: number | null) =>
    this.api.post<UsuarioAdminDto>(`/admin/usuarios/${seg(id)}/suspender`, { motivo, dias });
  reactivar = (id: string) => this.api.post<UsuarioAdminDto>(`/admin/usuarios/${seg(id)}/reactivar`);
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
  facturasCsv = (estado: EstadoFacturaDto) => this.api.archivo('/admin/facturas.csv', { params: { estado } });

  pqr = (estado: EstadoPqrDto, pagina = 1, tamano = 20) =>
    this.api.get<PqrAdminDtoPaginaDto>('/admin/pqr', { params: { estado, pagina, tamano } });
  responderPqr = (id: string, respuesta: string) =>
    this.api.post<PqrAdminDto>(`/admin/pqr/${seg(id)}/responder`, { respuesta }, { silencioso: true });

  ingresos = (desde?: string | null, hasta?: string | null) =>
    this.api.get<IngresosDto>('/admin/ingresos', { params: { desde, hasta } });
  ingresosCsv = (desde?: string | null, hasta?: string | null) =>
    this.api.archivo('/admin/ingresos.csv', { params: { desde, hasta } });
}
