import { inject, Injectable } from '@angular/core';
import { ConfigService } from '../config.service';
import { cargarScript } from './cargar-script';

interface GRecaptcha {
  ready(cb: () => void): void;
  execute(clave: string, opciones: { action: string }): Promise<string>;
}
declare global {
  interface Window {
    grecaptcha?: GRecaptcha;
  }
}

export type AccionCaptcha = 'registro' | 'login' | 'olvide_clave';

/** reCAPTCHA v3 (invisible). Solo se carga si la API publica una clave de sitio. */
@Injectable({ providedIn: 'root' })
export class CaptchaService {
  private readonly config = inject(ConfigService);

  /** Devuelve el token o null si el captcha está deshabilitado en este entorno. */
  async token(accion: AccionCaptcha): Promise<string | null> {
    const clave = this.config.captchaClave();
    if (!clave) return null;
    await cargarScript(`https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(clave)}`);
    const g = window.grecaptcha;
    if (!g) throw new Error('No se pudo cargar la verificación anti-robots.');
    await new Promise<void>((ok) => g.ready(ok));
    return g.execute(clave, { action: accion });
  }
}
