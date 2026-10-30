import { inject, Injectable } from '@angular/core';
import type { EstadisticasCuentaDto, NegocioPorVerificarDto, NegocioPublicoDto, NegocioResumenDto } from '../../api/tipos';
import { HttpApi, seg } from './http-api';

/** Plan Negocio: directorio y vitrina públicos, estadísticas de la cuenta y la revisión del NIT por los moderadores. */
@Injectable({ providedIn: 'root' })
export class NegociosApi {
  private readonly api = inject(HttpApi);

  listar = () => this.api.get<NegocioResumenDto[]>('/negocios');
  obtener = (id: string) => this.api.get<NegocioPublicoDto>(`/negocios/${seg(id)}`, { silencioso: true });
  estadisticas = () => this.api.get<EstadisticasCuentaDto>('/negocios/mio/estadisticas', { silencioso: true });

  // Moderadores (la API lo vuelve a exigir, con verificación en dos pasos)
  porVerificar = () => this.api.get<NegocioPorVerificarDto[]>('/admin/negocios');
  verificar = (id: string) => this.api.post<void>(`/admin/negocios/${seg(id)}/verificar`);
  rechazar = (id: string, motivo: string) => this.api.post<void>(`/admin/negocios/${seg(id)}/rechazar`, { motivo });
}
