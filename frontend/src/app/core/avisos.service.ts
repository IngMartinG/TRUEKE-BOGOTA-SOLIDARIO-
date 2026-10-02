import { Injectable, signal } from '@angular/core';

export type TipoAviso = 'exito' | 'error' | 'info' | 'puntos';

export interface Aviso {
  id: number;
  tipo: TipoAviso;
  titulo: string;
  detalle?: string;
}

/** Notificaciones emergentes (toasts). Se anuncian a lectores de pantalla con aria-live. */
@Injectable({ providedIn: 'root' })
export class AvisosService {
  private siguienteId = 1;
  private readonly _avisos = signal<Aviso[]>([]);
  readonly avisos = this._avisos.asReadonly();

  exito(titulo: string, detalle?: string): void {
    this.mostrar('exito', titulo, detalle);
  }
  error(titulo: string, detalle?: string): void {
    this.mostrar('error', titulo, detalle, 7000);
  }
  info(titulo: string, detalle?: string): void {
    this.mostrar('info', titulo, detalle);
  }
  puntos(titulo: string, detalle?: string): void {
    this.mostrar('puntos', titulo, detalle, 6000);
  }

  cerrar(id: number): void {
    this._avisos.update((l) => l.filter((a) => a.id !== id));
  }

  private mostrar(tipo: TipoAviso, titulo: string, detalle?: string, duracion = 4500): void {
    const id = this.siguienteId++;
    // Evita apilar el mismo mensaje varias veces (p. ej. varias peticiones fallando a la vez).
    if (this._avisos().some((a) => a.titulo === titulo && a.detalle === detalle)) return;
    this._avisos.update((l) => [...l.slice(-3), { id, tipo, titulo, detalle }]);
    setTimeout(() => this.cerrar(id), duracion);
  }
}
