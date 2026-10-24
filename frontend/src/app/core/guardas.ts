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

/**
 * Publicar y editar exigen además nombre y apellido reales, localidad y foto de perfil (reglas del backend): si falta algo
 * se lleva al perfil a completarlo y luego se vuelve.
 */
export const exigirCuentaCompleta: CanActivateFn = (ruta, estado) => {
  const verificado = exigirCorreoVerificado(ruta, estado);
  if (verificado !== true) return verificado;
  const sesion = inject(SesionService);
  if (sesion.nombreRealPendiente() || sesion.localidadPendiente())
    return inject(Router).createUrlTree(['/cuenta/perfil'], { fragment: 'datos', queryParams: { volver: estado.url } });
  return sesion.tieneFoto() ? true : inject(Router).createUrlTree(['/cuenta/perfil'], { fragment: 'foto', queryParams: { volver: estado.url } });
};

/** Páginas que una cuenta sin fecha de nacimiento sí puede abrir: registrarla, leer lo legal, pedir ayuda y sus datos. */
const LIBRES_SIN_FECHA = ['/completar-perfil', '/privacidad', '/terminos', '/accesibilidad', '/ayuda', '/pqrs', '/cuenta/privacidad', '/cuenta/soporte', '/verificar-correo', '/verifica-tu-correo'];

/**
 * Se aplica a TODAS las rutas: con sesión pero sin la mayoría de edad registrada (cuentas creadas antes de pedirla) o sin
 * aceptar las versiones vigentes de la política y los términos, solo se puede completar el perfil. Sin sesión no hace nada
 * (el catálogo es público).
 */
export const exigirMayoriaEdad: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionService);
  if (!sesion.autenticado() || !sesion.usuario() || (sesion.mayoriaEdadConfirmada() && !sesion.legalPendiente())) return true;
  const ruta = estado.url.split(/[?#]/)[0] ?? '';
  if (LIBRES_SIN_FECHA.some((r) => ruta === r || ruta.startsWith(`${r}/`))) return true;
  return inject(Router).createUrlTree(['/completar-perfil'], { queryParams: { volver: estado.url } });
};

/** Panel de moderación. La API además exige sesión con 2FA (responde 403 "2fa_requerido_admin"). */
export const exigirModerador: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionService);
  const router = inject(Router);
  if (!sesion.autenticado()) return router.createUrlTree(['/ingresar'], { queryParams: { volver: estado.url } });
  if (!sesion.correoVerificado()) return router.createUrlTree(['/verifica-tu-correo']);
  return sesion.esModerador() ? true : router.createUrlTree(['/explorar']);
};
