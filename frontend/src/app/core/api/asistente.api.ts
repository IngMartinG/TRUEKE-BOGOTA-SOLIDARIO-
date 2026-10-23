import { inject, Injectable } from '@angular/core';
import type { PreguntaAsistenteRequest, RespuestaAsistenteDto } from '../../api/tipos';
import { HttpApi } from './http-api';

/** Asistente de ayuda con IA. Silencioso: si falla, el asistente responde con la guía local sin mostrar errores. */
@Injectable({ providedIn: 'root' })
export class AsistenteApi {
  private readonly api = inject(HttpApi);

  preguntar(r: PreguntaAsistenteRequest) {
    return this.api.post<RespuestaAsistenteDto>('/asistente/preguntar', r, { silencioso: true });
  }
}
