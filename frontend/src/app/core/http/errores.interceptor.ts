import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AvisosService } from '../avisos.service';
import { apiBase } from '../entorno';
import { SesionService } from '../sesion.service';
import { ERROR_SILENCIOSO } from '../api/http-api';
import { problemaDe } from './problema';

/**
 * Manejo central de errores de la API. Los componentes que muestran el error en su propio
 * formulario marcan la petición como silenciosa; el resto recibe un aviso con el `title`.
 */
export const erroresInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(apiBase())) return next(req);
  const avisos = inject(AvisosService);
  const router = inject(Router);
  const sesion = inject(SesionService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) return throwError(() => error);
      const problema = problemaDe(error);

      if (problema.codigo === '2fa_requerido_admin') {
        avisos.info('Activa la verificación en dos pasos', 'Es obligatoria para usar las funciones de moderación.');
        void router.navigate(['/cuenta/seguridad']);
        return throwError(() => error);
      }

      if (error.status === 401 && sesion.token() === null && !req.context.get(ERROR_SILENCIOSO)) {
        void router.navigate(['/ingresar'], { queryParams: { volver: router.url } });
        avisos.info('Ingresa para continuar', 'Tu sesión terminó o necesitas una cuenta para esta acción.');
        return throwError(() => error);
      }

      if (!req.context.get(ERROR_SILENCIOSO)) {
        if (error.status === 429) {
          const espera = Number(error.headers.get('Retry-After'));
          avisos.error(problema.title ?? 'Demasiados intentos', espera ? `Inténtalo de nuevo en ${espera} s.` : undefined);
        } else {
          avisos.error(problema.title ?? 'No se pudo completar la acción.');
        }
      }
      return throwError(() => error);
    }),
  );
};
