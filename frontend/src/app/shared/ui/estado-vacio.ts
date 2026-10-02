import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icono } from './icono';

/** Estado vacío ilustrado: explica qué pasa y qué puede hacer la persona (contenido proyectado). */
@Component({
  selector: 'app-estado-vacio',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <div class="mx-auto flex max-w-md flex-col items-center px-4 py-14 text-center">
      <div class="relative mb-6">
        <div class="absolute inset-0 scale-150 rounded-full bg-bosque-200/40 blur-2xl dark:bg-bosque-700/20"></div>
        <div
          class="relative grid size-20 place-items-center rounded-[1.75rem] bg-gradient-to-br from-bosque-100 to-agua-100 text-bosque-700 shadow-suave dark:from-bosque-900 dark:to-agua-700/40 dark:text-bosque-200"
        >
          <app-icono [nombre]="icono()" [tamano]="36" [grosor]="1.6" />
        </div>
      </div>
      <h3 class="text-lg font-bold">{{ titulo() }}</h3>
      @if (descripcion()) {
        <p class="mt-2 text-sm text-tenue">{{ descripcion() }}</p>
      }
      <div class="mt-6 flex flex-wrap justify-center gap-3"><ng-content /></div>
    </div>
  `,
})
export class EstadoVacio {
  readonly icono = input('hoja');
  readonly titulo = input.required<string>();
  readonly descripcion = input<string>('');
}
