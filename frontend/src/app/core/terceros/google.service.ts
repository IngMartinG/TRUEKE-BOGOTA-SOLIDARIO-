import { inject, Injectable } from '@angular/core';
import { ConfigService } from '../config.service';
import { cargarScript } from './cargar-script';

interface GoogleId {
  initialize(opciones: {
    client_id: string;
    callback: (r: { credential: string }) => void;
    ux_mode?: 'popup';
    use_fedcm_for_prompt?: boolean;
  }): void;
  renderButton(elemento: HTMLElement, opciones: Record<string, unknown>): void;
}
declare global {
  interface Window {
    google?: { accounts: { id: GoogleId } };
  }
}

/** Google Identity Services: dibuja el botón oficial y entrega el `credential` (ID token). */
@Injectable({ providedIn: 'root' })
export class GoogleService {
  private readonly config = inject(ConfigService);

  get habilitado(): boolean {
    return !!this.config.googleClientId();
  }

  async renderizarBoton(
    contenedor: HTMLElement,
    alObtenerToken: (idToken: string) => void,
    texto: 'signin_with' | 'signup_with' | 'continue_with' = 'continue_with',
  ): Promise<void> {
    const clientId = this.config.googleClientId();
    if (!clientId) return;
    await cargarScript('https://accounts.google.com/gsi/client');
    const id = window.google?.accounts.id;
    if (!id) return;
    id.initialize({ client_id: clientId, callback: (r) => alObtenerToken(r.credential), ux_mode: 'popup' });
    id.renderButton(contenedor, {
      type: 'standard',
      theme: document.documentElement.classList.contains('dark') ? 'filled_black' : 'outline',
      size: 'large',
      shape: 'pill',
      text: texto,
      locale: 'es',
      width: Math.min(contenedor.clientWidth || 360, 400),
    });
  }
}
