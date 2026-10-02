/**
 * Configuración de despliegue leída en tiempo de ejecución desde `/config.json`.
 * Así la misma imagen del front sirve para cualquier entorno: en Docker, nginx genera
 * el archivo a partir de variables de entorno (ver docker/config.json.template).
 * Nunca contiene secretos: todo lo que llega al navegador es público.
 */
export interface Entorno {
  /** URL base de la API sin barra final. Vacío = mismo origen (proxy de desarrollo o nginx). */
  apiUrl: string;
}

let entorno: Entorno = { apiUrl: '' };

export async function cargarEntorno(): Promise<void> {
  try {
    const r = await fetch('/config.json', { cache: 'no-store' });
    if (r.ok) {
      const datos = (await r.json()) as Partial<Entorno>;
      entorno = { apiUrl: (datos.apiUrl ?? '').replace(/\/+$/, '') };
    }
  } catch {
    // Sin config.json se usa el mismo origen.
  }
}

export const apiBase = (): string => `${entorno.apiUrl}/api/v1`;
export const hubUrl = (): string => `${entorno.apiUrl}/hubs/notificaciones`;
