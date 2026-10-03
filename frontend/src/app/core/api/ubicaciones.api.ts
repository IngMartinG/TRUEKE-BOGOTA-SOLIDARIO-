import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import type { DepartamentoDto, MunicipioDto } from '../../api/tipos';
import { HttpApi, seg } from './http-api';

/** Departamentos y municipios de Colombia (DANE - DIVIPOLA). No cambian: se piden una vez por sesión de navegador. */
@Injectable({ providedIn: 'root' })
export class UbicacionesApi {
  private readonly api = inject(HttpApi);
  private readonly municipiosPorDepartamento = new Map<string, Observable<MunicipioDto[]>>();
  private readonly municipiosPorCodigo = new Map<string, Observable<MunicipioDto>>();

  readonly departamentos$ = this.api.get<DepartamentoDto[]>('/ubicaciones/departamentos').pipe(shareReplay(1));

  municipios(departamento: string): Observable<MunicipioDto[]> {
    let r = this.municipiosPorDepartamento.get(departamento);
    if (!r) {
      r = this.api.get<MunicipioDto[]>(`/ubicaciones/departamentos/${seg(departamento)}/municipios`).pipe(shareReplay(1));
      this.municipiosPorDepartamento.set(departamento, r);
    }
    return r;
  }

  municipio(codigo: string): Observable<MunicipioDto> {
    let r = this.municipiosPorCodigo.get(codigo);
    if (!r) {
      r = this.api.get<MunicipioDto>(`/ubicaciones/municipios/${seg(codigo)}`, { silencioso: true }).pipe(shareReplay(1));
      this.municipiosPorCodigo.set(codigo, r);
    }
    return r;
  }

  buscar = (texto: string) => this.api.get<MunicipioDto[]>('/ubicaciones/municipios', { params: { texto, max: 10 }, silencioso: true });
}
