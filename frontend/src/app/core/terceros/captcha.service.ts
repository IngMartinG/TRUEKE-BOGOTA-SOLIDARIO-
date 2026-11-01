import { inject, Injectable } from '@angular/core';
import { ConfigService } from '../config.service';
import { cargarScript } from './cargar-script';

interface OpcionesTurnstile {
  sitekey: string;
  action: string;
  appearance: 'interaction-only';
  language: string;
  callback: (token: string) => void;
  'error-callback': () => boolean;
  'expired-callback': () => void;
  'timeout-callback': () => void;
  'unsupported-callback': () => void;
}
interface Turnstile {
  render(contenedor: HTMLElement, opciones: OpcionesTurnstile): string | undefined;
  remove(id: string): void;
}
declare global {
  interface Window {
    turnstile?: Turnstile;
  }
}

export type AccionCaptcha = 'registro' | 'login' | 'olvide_clave' | 'pqr';

const NO_VERIFICADO = 'No pudimos verificar que eres una persona. Inténtalo de nuevo.';

/**
 * Cloudflare Turnstile: casi siempre invisible y sin cookies de rastreo. Solo se carga si la API publica una clave de sitio.
 * Si Cloudflare duda, muestra una casilla en un recuadro flotante (.captcha-turnstile) que desaparece al resolverla.
 */
@Injectable({ providedIn: 'root' })
export class CaptchaService {
  private readonly config = inject(ConfigService);

  /** Devuelve el token (de un solo uso) o null si el captcha está deshabilitado en este entorno. */
  async token(accion: AccionCaptcha): Promise<string | null> {
    const clave = this.config.captchaClave();
    if (!clave) return null;
    await cargarScript('https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit');
    const t = window.turnstile;
    if (!t) throw new Error('No se pudo cargar la verificación anti-robots.');

    const contenedor = document.createElement('div');
    contenedor.className = 'captcha-turnstile';
    document.body.appendChild(contenedor);
    let id: string | undefined;
    try {
      return await new Promise<string>((ok, mal) => {
        const fallo = () => mal(new Error(NO_VERIFICADO));
        id = t.render(contenedor, {
          sitekey: clave,
          action: accion,
          appearance: 'interaction-only',
          language: 'es',
          callback: ok,
          // true = error atendido aquí (Turnstile no lo relanza a la consola).
          'error-callback': () => (fallo(), true),
          'expired-callback': fallo,
          'timeout-callback': fallo,
          'unsupported-callback': fallo,
        });
      });
    } finally {
      if (id) t.remove(id);
      contenedor.remove();
    }
  }
}
