import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { Resource } from '@angular/core';
import { Icono } from './icono';

/**
 * Aviso de que una sección no cargó, con su botón para reintentar. Va junto a un `datos` que solo lee `value()` si
 * `hasValue()`: un recurso con error lanza al leer su valor y la vista se quedaba vacía sin explicar nada.
 */
@Component({
  selector: 'app-error-carga',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block empty:hidden' },
  template: `
    @if (recurso().error() && !recurso().isLoading()) {
      <div role="alert" class="mb-6 flex flex-wrap items-center gap-3 rounded-2xl border border-tierra-100 bg-tierra-50 px-4 py-3 text-sm text-tierra-700 dark:border-tierra-700/50 dark:bg-tierra-700/20 dark:text-tierra-100">
        <app-icono nombre="alerta" [tamano]="18" />
        <p class="min-w-0 flex-1">{{ mensaje() }}</p>
        <button type="button" class="btn btn-secundario btn-sm" (click)="recurso().reload()">Reintentar</button>
      </div>
    }
  `,
})
export class ErrorCarga {
  readonly recurso = input.required<Resource<unknown> & { reload(): boolean }>();
  readonly mensaje = input('No pudimos cargar esta sección. Si el servidor estaba despertando, reintenta en unos segundos.');
}
