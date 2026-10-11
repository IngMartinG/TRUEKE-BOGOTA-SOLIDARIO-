import { Injectable, inject, signal } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';

/** Evento que Chrome, Edge y Android disparan cuando la app se puede instalar (no es estándar: no tiene tipo propio). */
interface EventoInstalacion extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

export const enPantallaCompleta = () =>
  matchMedia('(display-mode: standalone)').matches || (navigator as Navigator & { standalone?: boolean }).standalone === true;

/** iPhone o iPad (los iPad nuevos se presentan como Mac, pero con pantalla táctil). */
export const esIos = () => /iPad|iPhone|iPod/.test(navigator.userAgent) || (/Macintosh/.test(navigator.userAgent) && navigator.maxTouchPoints > 1);

/**
 * App instalable (PWA): guarda el evento de instalación para ofrecer el botón "Instalar la app" en el momento que la
 * persona elija, y mantiene la app al día. Una versión nueva se descarga en segundo plano y se usa la próxima vez que se
 * abra la app (nunca se recarga a mitad de lo que la persona está haciendo).
 */
@Injectable({ providedIn: 'root' })
export class PwaService {
  private readonly actualizaciones = inject(SwUpdate);
  private evento: EventoInstalacion | null = null;

  /** El navegador ofrece instalarla (Chrome, Edge, Android). En iPhone se instala desde "Compartir". */
  readonly puedeInstalar = signal(false);
  readonly instalada = signal(enPantallaCompleta());

  constructor() {
    window.addEventListener('beforeinstallprompt', (e) => {
      e.preventDefault(); // sin el aviso automático del navegador: lo ofrece la app
      this.evento = e as EventoInstalacion;
      this.puedeInstalar.set(true);
    });
    window.addEventListener('appinstalled', () => {
      this.evento = null;
      this.puedeInstalar.set(false);
      this.instalada.set(true);
    });

    if (!this.actualizaciones.isEnabled) return;
    // Si la versión guardada quedó inservible (p. ej. el navegador borró parte de la caché), se recarga desde el servidor.
    this.actualizaciones.unrecoverable.subscribe(() => location.reload());
    // Una app instalada puede quedar abierta días: al volver a ella se busca si hay versión nueva.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') void this.actualizaciones.checkForUpdate().catch(() => false);
    });
  }

  async instalar(): Promise<void> {
    const evento = this.evento;
    if (!evento) return;
    await evento.prompt();
    const { outcome } = await evento.userChoice;
    this.evento = null;
    this.puedeInstalar.set(false);
    if (outcome === 'accepted') this.instalada.set(true);
  }
}
