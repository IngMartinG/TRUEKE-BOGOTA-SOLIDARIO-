import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { Icono } from './icono';

/**
 * Botón "Volver": regresa a la pantalla anterior de la app (como el botón atrás del celular).
 * Si se llegó directo (enlace compartido, pestaña nueva), va a la ruta de respaldo en vez de salir del sitio.
 */
@Component({
  selector: 'app-volver',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex' },
  template: `
    <button type="button" class="-ml-2 inline-flex items-center gap-1.5 rounded-full px-2.5 py-1.5 text-sm font-semibold text-tenue transition hover:bg-superficie-2 hover:text-tinta"
      (click)="volver()">
      <app-icono nombre="izquierda" [tamano]="18" />{{ texto() }}
    </button>
  `,
})
export class Volver {
  /** A dónde ir si no hay una pantalla anterior dentro de la app. */
  readonly respaldo = input('/');
  readonly texto = input('Volver');
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  protected volver(): void {
    // El router guarda en history.state un navigationId incremental: > 1 significa que hubo una pantalla anterior en la app.
    const estado = this.location.getState() as { navigationId?: number } | null;
    if ((estado?.navigationId ?? 1) > 1) this.location.back();
    else void this.router.navigateByUrl(this.respaldo());
  }
}
