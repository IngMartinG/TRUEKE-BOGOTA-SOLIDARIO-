import { HttpClient, HttpErrorResponse, HttpEventType } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, filter, firstValueFrom, map, Observable, switchMap, throwError } from 'rxjs';
import type { TipoArchivoDto } from '../api/tipos';
import { urlPublica } from '../shared/imagenes';
import { CuentaApi } from './api/cuenta.api';
import { ConfigService } from './config.service';

export const TIPOS_IMAGEN = ['image/jpeg', 'image/png', 'image/webp'];
export const TIPOS_DOCUMENTO = ['application/pdf', ...TIPOS_IMAGEN];

export interface ProgresoSubida {
  progreso: number;
  url?: string;
}

/**
 * Subida directa a Supabase Storage con URL firmada:
 * 1) la API entrega una URL firmada para UN archivo nuevo (no puede sobrescribir); 2) el navegador hace PUT del archivo;
 * 3) se usa `urlArchivo` en la publicación. La API valida dueño, tamaño y tipo real al usarla.
 */
@Injectable({ providedIn: 'root' })
export class SubidasService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(CuentaApi);
  private readonly config = inject(ConfigService);

  validar(archivo: File, tipo: TipoArchivoDto = 'Imagen'): string | null {
    const permitidos = tipo === 'Imagen' ? TIPOS_IMAGEN : TIPOS_DOCUMENTO;
    if (!permitidos.includes(archivo.type)) {
      return tipo === 'Imagen' ? 'Usa imágenes JPG, PNG o WebP.' : 'Usa un PDF, JPG o PNG.';
    }
    const max = this.config.tamanoMaximo();
    if (archivo.size > max) return `El archivo supera ${Math.round(max / 1024 / 1024)} MB.`;
    return null;
  }

  subir(archivo: File, tipo: TipoArchivoDto = 'Imagen'): Observable<ProgresoSubida> {
    return this.api.solicitarSubida({ tipo, contentType: archivo.type, tamanoBytes: archivo.size }).pipe(
      switchMap((s) => {
        let headers: Record<string, string> = { ...(s.cabeceras ?? {}) };
        if (!Object.keys(headers).some((h) => h.toLowerCase() === 'content-type')) {
          headers = { ...headers, 'Content-Type': archivo.type };
        }
        const url = urlPublica(s.urlSubida ?? '');
        const enviar = (destino: string) =>
          this.http.request(s.metodo ?? 'PUT', destino, { body: archivo, headers, reportProgress: true, observe: 'events' });
        // ?ngsw-bypass: la subida directa al almacenamiento no pasa por el service worker (PWA); si pasara, el service
        // worker reenvía el PUT y puede fallar (504). Si el almacenamiento rechazara ese parámetro extra (400/403), se
        // reintenta una vez sin él: nunca queda peor que sin la PWA.
        return enviar(conBypass(url))
          .pipe(
            catchError((e: unknown) =>
              e instanceof HttpErrorResponse && (e.status === 400 || e.status === 403) ? enviar(url) : throwError(() => e),
            ),
            filter((e) => e.type === HttpEventType.UploadProgress || e.type === HttpEventType.Response),
            map((e) =>
              e.type === HttpEventType.UploadProgress
                ? { progreso: e.total ? Math.round((100 * e.loaded) / e.total) : 50 }
                : { progreso: 100, url: s.urlArchivo },
            ),
          );
      }),
    );
  }

  async subirTodo(archivo: File, tipo: TipoArchivoDto = 'Imagen'): Promise<string> {
    const final = await firstValueFrom(this.subir(archivo, tipo).pipe(filter((p) => !!p.url)));
    return final.url!;
  }
}

/** Agrega ?ngsw-bypass=true sin tocar el token de la URL firmada. */
export function conBypass(url: string): string {
  return url ? `${url}${url.includes('?') ? '&' : '?'}ngsw-bypass=true` : url;
}
