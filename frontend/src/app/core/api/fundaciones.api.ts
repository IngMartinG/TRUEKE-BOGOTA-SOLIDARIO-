import { inject, Injectable } from '@angular/core';
import { shareReplay } from 'rxjs';
import type {
  EditarFundacionRequest,
  EstadoFundacionDto,
  FundacionAdminDto,
  FundacionPublicaDto,
  FundacionResumenDto,
  GuardarNecesidadRequest,
  MiFundacionDto,
  NecesidadDto,
  SolicitarFundacionRequest,
} from '../../api/tipos';
import { HttpApi, seg } from './http-api';

/** Fundaciones verificadas: listado y página públicos, perfil propio con "Lo que necesitamos" y revisión del SuperUsuario. */
@Injectable({ providedIn: 'root' })
export class FundacionesApi {
  private readonly api = inject(HttpApi);

  listar = () => this.api.get<FundacionResumenDto[]>('/fundaciones');
  /** El mismo listado, una vez por sesión (para sugerir fundaciones al donar). */
  readonly listado$ = this.api.get<FundacionResumenDto[]>('/fundaciones', { silencioso: true }).pipe(shareReplay(1));
  obtener = (id: string) => this.api.get<FundacionPublicaDto>(`/fundaciones/${seg(id)}`, { silencioso: true });

  /** null (204) si la cuenta nunca pidió ser fundación. */
  mia = () => this.api.get<MiFundacionDto | null>('/fundaciones/mia');
  solicitar = (r: SolicitarFundacionRequest) => this.api.post<MiFundacionDto>('/fundaciones/mia', r, { silencioso: true });
  editar = (r: EditarFundacionRequest) => this.api.put<MiFundacionDto>('/fundaciones/mia', r, { silencioso: true });
  eliminar = () => this.api.delete<void>('/fundaciones/mia');
  crearNecesidad = (r: GuardarNecesidadRequest) => this.api.post<NecesidadDto>('/fundaciones/mia/necesidades', r, { silencioso: true });
  actualizarNecesidad = (id: string, r: GuardarNecesidadRequest) =>
    this.api.put<NecesidadDto>(`/fundaciones/mia/necesidades/${seg(id)}`, r, { silencioso: true });
  eliminarNecesidad = (id: string) => this.api.delete<void>(`/fundaciones/mia/necesidades/${seg(id)}`);

  // Solo SuperUsuario (la API lo vuelve a exigir, con verificación en dos pasos)
  revision = (estado: EstadoFundacionDto) => this.api.get<FundacionAdminDto[]>('/admin/fundaciones', { params: { estado } });
  aprobar = (id: string) => this.api.post<void>(`/admin/fundaciones/${seg(id)}/aprobar`);
  rechazar = (id: string, motivo: string) => this.api.post<void>(`/admin/fundaciones/${seg(id)}/rechazar`, { motivo });
  retirar = (id: string, motivo: string) => this.api.post<void>(`/admin/fundaciones/${seg(id)}/retirar`, { motivo });
  regimen = (id: string, calificada: boolean, nota: string) => this.api.post<void>(`/admin/fundaciones/${seg(id)}/regimen`, { calificada, nota });
}
