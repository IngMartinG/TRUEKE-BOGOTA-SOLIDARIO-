import { HttpClient, HttpContext, HttpContextToken, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBase } from '../entorno';

/** Marca peticiones que no deben mostrar el toast de error genérico (el componente lo maneja). */
export const ERROR_SILENCIOSO = new HttpContextToken<boolean>(() => false);

export type Parametros = Record<string, string | number | boolean | null | undefined>;

export interface OpcionesApi {
  params?: Parametros;
  silencioso?: boolean;
}

/** Envoltura mínima de HttpClient: antepone la base de la API y limpia parámetros vacíos. */
@Injectable({ providedIn: 'root' })
export class HttpApi {
  private readonly http = inject(HttpClient);

  get<T>(ruta: string, op: OpcionesApi = {}): Observable<T> {
    return this.http.get<T>(apiBase() + ruta, this.opciones(op));
  }

  post<T>(ruta: string, cuerpo: unknown = {}, op: OpcionesApi = {}): Observable<T> {
    return this.http.post<T>(apiBase() + ruta, cuerpo, this.opciones(op));
  }

  put<T>(ruta: string, cuerpo: unknown, op: OpcionesApi = {}): Observable<T> {
    return this.http.put<T>(apiBase() + ruta, cuerpo, this.opciones(op));
  }

  patch<T>(ruta: string, cuerpo: unknown, op: OpcionesApi = {}): Observable<T> {
    return this.http.patch<T>(apiBase() + ruta, cuerpo, this.opciones(op));
  }

  delete<T>(ruta: string, op: OpcionesApi = {}): Observable<T> {
    return this.http.delete<T>(apiBase() + ruta, this.opciones(op));
  }

  private opciones(op: OpcionesApi) {
    let params = new HttpParams();
    for (const [clave, valor] of Object.entries(op.params ?? {})) {
      if (valor !== null && valor !== undefined && valor !== '') params = params.set(clave, String(valor));
    }
    return { params, context: new HttpContext().set(ERROR_SILENCIOSO, op.silencioso ?? false) };
  }
}

/** Codifica un segmento de ruta (ids y referencias) para evitar inyección de rutas. */
export const seg = (valor: string | number): string => encodeURIComponent(String(valor));
