import { Injectable, signal } from '@angular/core';

export type TipoAviso = 'exito' | 'error' | 'info' | 'puntos';

export interface Aviso {
  id: number;
  tipo: TipoAviso;
  titulo: string;
  detalle?: string;
}

interface Temporizador {
  restante: number;
  inicio: number;
  id?: ReturnType<typeof setTimeout>;
}

/**
 * Notificaciones emergentes (toasts). Se anuncian a lectores de pantalla con aria-live.
 * El tiempo se detiene mientras el aviso tiene el puntero o el foco encima (WCAG 2.2.1).
 */
@Injectable({ providedIn: 'root' })
export class AvisosService {
  private siguienteId = 1;
  private readonly _avisos = signal<Aviso[]>([]);
  private readonly temporizadores = new Map<number, Temporizador>();
  readonly avisos = this._avisos.asReadonly();

  exito(titulo: string, detalle?: string): void {
    this.mostrar('exito', titulo, detalle);
  }
  error(titulo: string, detalle?: string): void {
    this.mostrar('error', titulo, detalle, 10000);
  }
  info(titulo: string, detalle?: string): void {
    this.mostrar('info', titulo, detalle);
  }
  puntos(titulo: string, detalle?: string): void {
    this.mostrar('puntos', titulo, detalle, 6000);
  }

  cerrar(id: number): void {
    const t = this.temporizadores.get(id);
    if (t?.id) clearTimeout(t.id);
    this.temporizadores.delete(id);
    this._avisos.update((l) => l.filter((a) => a.id !== id));
  }

  pausar(id: number): void {
    const t = this.temporizadores.get(id);
    if (!t?.id) return;
    clearTimeout(t.id);
    t.id = undefined;
    t.restante -= Date.now() - t.inicio;
  }

  reanudar(id: number): void {
    const t = this.temporizadores.get(id);
    if (!t || t.id) return;
    t.inicio = Date.now();
    // Un respiro mínimo al soltar, para que no desaparezca justo al quitar el mouse
    t.id = setTimeout(() => this.cerrar(id), Math.max(t.restante, 2000));
  }

  private mostrar(tipo: TipoAviso, titulo: string, detalle?: string, duracion = 5000): void {
    const id = this.siguienteId++;
    // Evita apilar el mismo mensaje varias veces (p. ej. varias peticiones fallando a la vez).
    if (this._avisos().some((a) => a.titulo === titulo && a.detalle === detalle)) return;
    const lista = this._avisos();
    if (lista.length > 3) this.cerrar(lista[0]!.id);
    this._avisos.update((l) => [...l, { id, tipo, titulo, detalle }]);
    this.temporizadores.set(id, { restante: duracion, inicio: Date.now(), id: setTimeout(() => this.cerrar(id), duracion) });
  }
}
