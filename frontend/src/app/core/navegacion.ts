import { ErrorHandler, inject } from '@angular/core';
import type { NavigationError } from '@angular/router';
import { AvisosService } from './avisos.service';

/** Última recarga hecha por un fragmento que no cargó: evita recargar en bucle si la red sigue caída. */
const CLAVE_RECARGA = 'trueke.recarga';
const VENTANA_RECARGA_MS = 15_000;

/**
 * El fragmento JS de una página no se pudo descargar: red inestable (celular), servidor ocupado o un despliegue que
 * reemplazó los archivos de la versión que la pestaña tenía abierta. Chrome, Firefox y Safari lo dicen distinto.
 */
export function esFalloDeCarga(error: unknown): boolean {
  const texto = error instanceof Error ? `${error.name} ${error.message}` : String(error);
  return /dynamically imported module|Importing a module script failed|ChunkLoadError|Loading chunk/i.test(texto);
}

/**
 * Manejo global de errores de navegación (con `resolveNavigationPromiseOnError`, `navigateByUrl` ya no rechaza): una
 * acción que salió bien en la API (ingresar, registrarse, guardar) nunca se muestra como fallida porque no cargó la
 * página siguiente. Si faltó un fragmento, se carga la página destino completa una vez (la sesión vuelve sola con la
 * cookie de refresco); si vuelve a fallar enseguida, se avisa.
 */
export function manejarErrorNavegacion(e: NavigationError): void {
  if (!esFalloDeCarga(e.error)) {
    inject(ErrorHandler).handleError(e.error); // consola y, si está activo, monitoreo de errores
    return;
  }
  const ahora = Date.now();
  let anterior: { url: string; t: number } | null = null;
  try {
    anterior = JSON.parse(sessionStorage.getItem(CLAVE_RECARGA) ?? 'null') as { url: string; t: number } | null;
  } catch {
    // Sin almacenamiento: se recarga igual (como mucho una vez por navegación).
  }
  if (anterior?.url === e.url && ahora - anterior.t < VENTANA_RECARGA_MS) {
    inject(AvisosService).error('No se pudo cargar la página', 'Revisa tu conexión e inténtalo de nuevo.');
    return;
  }
  try {
    sessionStorage.setItem(CLAVE_RECARGA, JSON.stringify({ url: e.url, t: ahora }));
  } catch {
    // Igual que arriba.
  }
  location.assign(e.url);
}
