import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { apiBase } from '../entorno';
import { SesionService } from '../sesion.service';

/** Rutas de autenticación que nunca deben disparar un refresco automático ante un 401. */
const SIN_REFRESCO = ['/auth/login', '/auth/registrar', '/auth/google', '/auth/refrescar', '/auth/salir'];

/**
 * - Añade el JWT (solo a la API propia, nunca a terceros como Blob Storage).
 * - Las rutas /auth/* viajan con credenciales (la cookie de refresco tiene Path=/api/v1/auth)
 *   y con la cabecera anti-CSRF que exige el backend.
 * - Ante un 401, refresca UNA vez y reintenta la petición original.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const base = apiBase();
  if (!req.url.startsWith(base)) return next(req);

  const sesion = inject(SesionService);
  const ruta = req.url.slice(base.length);
  const esAuth = ruta.startsWith('/auth/');

  const preparar = (r: HttpRequest<unknown>, token: string | null) => {
    let headers = r.headers;
    if (token) headers = headers.set('Authorization', `Bearer ${token}`);
    if (esAuth) headers = headers.set('X-Trueke-Csrf', '1');
    return r.clone({ headers, withCredentials: esAuth || r.withCredentials });
  };

  // Token ya vencido (pestaña dormida, equipo suspendido): se renueva antes de enviar, sin esperar el 401.
  if (!esAuth && sesion.token() !== null && sesion.venceEn() <= 5_000) {
    return sesion.refrescar().pipe(switchMap((nuevo) => enviar(nuevo ?? sesion.token())));
  }
  return enviar(sesion.token());

  function enviar(tokenUsado: string | null) {
    return next(preparar(req, tokenUsado)).pipe(
    catchError((error: unknown) => {
      const reintentable =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        tokenUsado !== null &&
        !SIN_REFRESCO.some((r) => ruta.startsWith(r)) &&
        !(error.error && typeof error.error === 'object' && 'codigo' in error.error);
      if (!reintentable) return throwError(() => error);

      return sesion.refrescar().pipe(
        switchMap((nuevo) => (nuevo ? next(preparar(req, nuevo)) : throwError(() => error))),
      );
    }),
    );
  }
};
