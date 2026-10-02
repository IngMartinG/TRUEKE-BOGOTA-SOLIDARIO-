import { HttpErrorResponse } from '@angular/common/http';
import type { Problema } from '../../api/tipos';

/** Extrae el ProblemDetails de un error HTTP (o uno equivalente si no hubo respuesta). */
export function problemaDe(error: unknown): Problema {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return { status: 0, title: 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.' };
    }
    const cuerpo = error.error;
    if (cuerpo && typeof cuerpo === 'object') return { status: error.status, ...(cuerpo as Problema) };
    return { status: error.status, title: tituloPorEstado(error.status) };
  }
  return { title: 'Ocurrió un error inesperado.' };
}

export function mensajeDe(error: unknown): string {
  return problemaDe(error).title ?? 'Ocurrió un error inesperado.';
}

/** Errores de validación por campo, con las claves en camelCase como los nombres del formulario. */
export function erroresDeCampos(error: unknown): Record<string, string> {
  const errores = problemaDe(error).errors ?? {};
  const salida: Record<string, string> = {};
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = clave.replace(/^\$\./, '').replace(/^./, (c) => c.toLowerCase());
    if (mensajes?.length) salida[campo] = mensajes[0];
  }
  return salida;
}

function tituloPorEstado(estado: number): string {
  switch (estado) {
    case 401:
      return 'Tu sesión expiró. Ingresa de nuevo.';
    case 403:
      return 'No tienes permiso para hacer esto.';
    case 404:
      return 'No encontramos lo que buscabas.';
    case 409:
      return 'Alguien más modificó esto al mismo tiempo. Recarga e inténtalo de nuevo.';
    case 413:
      return 'El contenido es demasiado grande.';
    case 429:
      return 'Vas muy rápido. Espera un momento e inténtalo de nuevo.';
    default:
      return estado >= 500 ? 'El servidor tuvo un problema. Inténtalo en unos minutos.' : 'No se pudo completar la acción.';
  }
}
