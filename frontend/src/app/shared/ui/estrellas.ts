import { ChangeDetectionStrategy, Component, input, model, signal } from '@angular/core';
import { Icono } from './icono';

/** Estrellas de calificación. Con `editable` funciona como control (teclado y ratón). */
@Component({
  selector: 'app-estrellas',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex items-center gap-0.5' },
  template: `
    @if (editable()) {
      <div role="radiogroup" aria-label="Calificación" class="flex gap-1" (mouseleave)="hover.set(0)">
        @for (n of [1, 2, 3, 4, 5]; track n) {
          <button
            type="button"
            role="radio"
            [attr.aria-checked]="valor() === n"
            [attr.aria-label]="n + (n === 1 ? ' estrella' : ' estrellas')"
            class="rounded-md p-0.5 transition hover:scale-110"
            [class]="(hover() || valor()) >= n ? 'text-sol-400' : 'text-borde'"
            (mouseenter)="hover.set(n)"
            (click)="valor.set(n)"
          >
            <app-icono nombre="estrella" [tamano]="tamano()" [relleno]="(hover() || valor()) >= n" />
          </button>
        }
      </div>
    } @else {
      <span class="sr-only">{{ valor() }} de 5 estrellas</span>
      @for (n of [1, 2, 3, 4, 5]; track n) {
        <app-icono
          nombre="estrella"
          [tamano]="tamano()"
          [relleno]="valor() >= n - 0.25"
          [class]="valor() >= n - 0.25 ? 'text-sol-400' : 'text-tenue/30'"
        />
      }
    }
  `,
})
export class Estrellas {
  readonly valor = model(0);
  readonly editable = input(false);
  readonly tamano = input(16);
  protected readonly hover = signal(0);
}
