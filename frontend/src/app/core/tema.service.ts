import { effect, Injectable, signal } from '@angular/core';

export type Tema = 'claro' | 'oscuro' | 'sistema';
const CLAVE = 'trueke.tema';

/** Modo claro/oscuro. La preferencia es solo visual, por eso es aceptable en localStorage. */
@Injectable({ providedIn: 'root' })
export class TemaService {
  readonly tema = signal<Tema>(this.leer());
  private readonly _oscuro = signal(false);
  private readonly consulta = window.matchMedia('(prefers-color-scheme: dark)');

  constructor() {
    this.aplicar(this.tema()); // síncrono: evita el destello de tema incorrecto al cargar
    this.consulta.addEventListener('change', () => this.aplicar(this.tema()));
    effect(() => {
      const t = this.tema();
      this.aplicar(t);
      try {
        localStorage.setItem(CLAVE, t);
      } catch {
        // Almacenamiento bloqueado: se mantiene solo en memoria.
      }
    });
  }

  alternar(): void {
    this.tema.set(this.oscuroActivo() ? 'claro' : 'oscuro');
  }

  oscuroActivo(): boolean {
    return this._oscuro();
  }

  private aplicar(t: Tema): void {
    const oscuro = t === 'oscuro' || (t === 'sistema' && this.consulta.matches);
    this._oscuro.set(oscuro);
    document.documentElement.classList.toggle('dark', oscuro);
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', oscuro ? '#0b1611' : '#123d27');
  }

  private leer(): Tema {
    try {
      const v = localStorage.getItem(CLAVE);
      return v === 'claro' || v === 'oscuro' ? v : 'sistema';
    } catch {
      return 'sistema';
    }
  }
}
