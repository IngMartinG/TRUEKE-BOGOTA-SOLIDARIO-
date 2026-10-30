import { inject, Injectable } from '@angular/core';
import type {
  CampanaDto,
  EmitirInformeRequest,
  EmpresaAdminDetalleDto,
  EmpresaAdminDto,
  EstadoEmpresaDto,
  GuardarCampanaRequest,
  InformeImpactoDto,
  InvitacionCreadaDto,
  MiEmpresaDto,
  RolMiembroEmpresaDto,
  SolicitarEmpresaRequest,
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

/**
 * Empresas: la propia (registro, equipo, campañas e informes, que solo ven sus miembros) y su administración (verificar es del
 * SuperUsuario; los informes los emite cualquier Administrador).
 */
@Injectable({ providedIn: 'root' })
export class EmpresasApi {
  private readonly api = inject(HttpApi);

  /** null (204) si la cuenta no pertenece a ninguna empresa. */
  mia = () => this.api.get<MiEmpresaDto | null>('/empresas/mia');
  solicitar = (r: SolicitarEmpresaRequest) => this.api.post<MiEmpresaDto>('/empresas/mia', r, { silencioso: true });
  invitar = (rol: RolMiembroEmpresaDto) => this.api.post<InvitacionCreadaDto>('/empresas/mia/invitaciones', { rol });
  revocarInvitacion = (id: string) => this.api.delete<void>(`/empresas/mia/invitaciones/${seg(id)}`);
  unirse = (codigo: string) => this.api.post<MiEmpresaDto>('/empresas/unirse', { codigo }, { silencioso: true });
  cambiarRol = (id: string, rol: RolMiembroEmpresaDto) => this.api.put<MiEmpresaDto>(`/empresas/mia/miembros/${seg(id)}/rol`, { rol });
  quitarMiembro = (id: string) => this.api.delete<void>(`/empresas/mia/miembros/${seg(id)}`);
  salir = () => this.api.delete<void>('/empresas/mia/membresia');
  crearCampana = (r: GuardarCampanaRequest) => this.api.post<CampanaDto>('/empresas/mia/campanas', r, { silencioso: true });
  editarCampana = (id: string, r: GuardarCampanaRequest) => this.api.put<CampanaDto>(`/empresas/mia/campanas/${seg(id)}`, r, { silencioso: true });
  /** Para el selector al publicar (vacío si la cuenta no pertenece a una empresa verificada). */
  campanasActivas = () => this.api.get<CampanaDto[]>('/empresas/mia/campanas-activas', { silencioso: true });
  informePdf = (id: string) => this.api.archivo(`/empresas/mia/informes/${seg(id)}/pdf`);
  informeCsv = (id: string) => this.api.archivo(`/empresas/mia/informes/${seg(id)}/csv`);

  // Administración (la API vuelve a exigir el rol, con verificación en dos pasos)
  listarAdmin = (estado: EstadoEmpresaDto | null) => this.api.get<EmpresaAdminDto[]>('/admin/empresas', { params: { estado } });
  detalleAdmin = (id: string) => this.api.get<EmpresaAdminDetalleDto>(`/admin/empresas/${seg(id)}`);
  aprobar = (id: string) => this.api.post<void>(`/admin/empresas/${seg(id)}/aprobar`);
  rechazar = (id: string, motivo: string) => this.api.post<void>(`/admin/empresas/${seg(id)}/rechazar`, { motivo });
  retirar = (id: string, motivo: string) => this.api.post<void>(`/admin/empresas/${seg(id)}/retirar`, { motivo });
  emitirInforme = (id: string, r: EmitirInformeRequest) =>
    this.api.post<InformeImpactoDto>(`/admin/empresas/${seg(id)}/informes`, r, { silencioso: true });
  informePdfAdmin = (id: string) => this.api.archivo(`/admin/empresas/informes/${seg(id)}/pdf`);
  informeCsvAdmin = (id: string) => this.api.archivo(`/admin/empresas/informes/${seg(id)}/csv`);
}
