/**
 * Configuración de despliegue leída en tiempo de ejecución desde `config.json` (relativo al `<base href>`).
 * Así la misma imagen del front sirve para cualquier entorno: en Docker, nginx genera
 * el archivo a partir de variables de entorno (ver docker/config.json.template).
 * Nunca contiene secretos: todo lo que llega al navegador es público.
 */
export interface Entorno {
  /** URL base de la API sin barra final. Vacío = mismo origen (proxy de desarrollo o nginx). */
  apiUrl: string;
  /** Plantilla de teselas del mapa ({s}, {z}, {x}, {y}). Por defecto, OpenStreetMap. */
  mapaTeselas: string;
  /** Atribución que exige el proveedor de teselas (HTML fijo de configuración, no de usuarios). */
  mapaAtribucion: string;
}

const POR_DEFECTO: Entorno = {
  apiUrl: '',
  mapaTeselas: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  mapaAtribucion: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
};

let entorno: Entorno = { ...POR_DEFECTO };

export async function cargarEntorno(): Promise<void> {
  try {
    const r = await fetch('config.json', { cache: 'no-store' });
    if (r.ok) {
      const datos = (await r.json()) as Partial<Entorno>;
      entorno = {
        apiUrl: (datos.apiUrl ?? '').replace(/\/+$/, ''),
        mapaTeselas: datos.mapaTeselas || POR_DEFECTO.mapaTeselas,
        mapaAtribucion: datos.mapaAtribucion || POR_DEFECTO.mapaAtribucion,
      };
    }
  } catch {
    // Sin config.json se usan los valores por defecto (API en el mismo origen).
  }
}

export const apiBase = (): string => `${entorno.apiUrl}/api/v1`;

/**
 * Enlace para compartir una publicación: la página de la API con la vista previa (título, foto) que leen WhatsApp
 * y Facebook, que redirige a la publicación. Sin API en otro dominio (desarrollo con proxy) no hay vista previa: null.
 */
export const urlCompartirPublicacion = (id: string): string | null =>
  entorno.apiUrl ? `${entorno.apiUrl}/compartir/publicaciones/${encodeURIComponent(id)}` : null;
export const hubUrl = (): string => `${entorno.apiUrl}/hubs/notificaciones`;
export const mapaTeselas = (): { url: string; atribucion: string } => ({
  url: entorno.mapaTeselas,
  atribucion: entorno.mapaAtribucion,
});
