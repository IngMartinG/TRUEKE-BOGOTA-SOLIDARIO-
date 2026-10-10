import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SesionService } from './sesion.service';

/** Exige sesión (aunque el correo no esté verificado); si no hay, lleva a ingresar y vuelve luego a la página pedida. */
export const exigirSesion: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionService);
  return sesion.autenticado()
    ? true
    : inject(Router).createUrlTree(['/ingresar'], { queryParams: { volver: estado.url } });
};

/** Páginas solo para visitantes (ingresar, registrarse): con sesión se va al catálogo. */
export const soloInvitado: CanActivateFn = () =>
  inject(SesionService).autenticado() ? inject(Router).createUrlTree(['/explorar']) : true;

/**
 * Todo lo de un usuario registrado (publicar, chatear, su cuenta, pagos...) exige correo verificado. Sin verificarlo,
 * la persona solo puede mirar el catálogo como un visitante y confirmar su correo.
 */
export const exigirCorreoVerificado: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionService);
  const router = inject(Router);
  if (!sesion.autenticado()) return router.createUrlTree(['/ingresar'], { queryParams: { volver: estado.url } });
  return sesion.correoVerificado() ? true : router.createUrlTree(['/verifica-tu-correo']);
};

/** Panel de moderación. La API además exige sesión con 2FA (responde 403 "2fa_requerido_admin"). */
export const exigirModerador: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionService);
  const router = inject(Router);
  if (!sesion.autenticado()) return router.createUrlTree(['/ingresar'], { queryParams: { volver: estado.url } });
  if (!sesion.correoVerificado()) return router.createUrlTree(['/verifica-tu-correo']);
  return sesion.esModerador() ? true : router.createUrlTree(['/explorar']);
};
