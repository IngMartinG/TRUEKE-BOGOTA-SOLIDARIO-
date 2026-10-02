import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  input,
  model,
  viewChild,
} from '@angular/core';
import { Icono } from './icono';

/**
 * Diálogo modal sobre <dialog> nativo: atrapa el foco, cierra con Esc y devuelve el foco
 * al elemento que lo abrió. En móvil se muestra como hoja inferior.
 */
@Component({
  selector: 'app-modal',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    dialog::backdrop {
      background: rgb(10 36 23 / 0.55);
      backdrop-filter: blur(3px);
    }
    dialog[open] {
      animation: subir 0.28s cubic-bezier(0.2, 0.7, 0.2, 1);
    }
    @keyframes subir {
      from {
        opacity: 0;
        transform: translateY(24px);
      }
    }
  `,
  template: `
    <dialog
      #dialogo
      class="m-0 mt-auto max-h-[92dvh] w-full max-w-none overflow-hidden rounded-t-3xl border border-borde bg-superficie p-0 text-tinta shadow-elevada sm:m-auto sm:rounded-3xl"
      [class]="ancho()"
      [attr.aria-labelledby]="'titulo-' + id"
      (close)="abierto.set(false)"
      (click)="clicFondo($event)"
    >
      <div class="flex max-h-[92dvh] flex-col">
        <header class="flex items-start justify-between gap-4 border-b border-borde px-6 py-4">
          <div>
            <h2 [id]="'titulo-' + id" class="text-lg font-bold">{{ titulo() }}</h2>
            @if (subtitulo()) {
              <p class="mt-0.5 text-sm text-tenue">{{ subtitulo() }}</p>
            }
          </div>
          <button type="button" class="btn-icono -mr-2" (click)="abierto.set(false)" aria-label="Cerrar">
            <app-icono nombre="x" />
          </button>
        </header>
        <div class="overflow-y-auto px-6 py-5"><ng-content /></div>
        <ng-content select="[pie]" />
      </div>
    </dialog>
  `,
})
export class Modal {
  private static contador = 0;
  protected readonly id = ++Modal.contador;
  readonly abierto = model(false);
  readonly titulo = input.required<string>();
  readonly subtitulo = input('');
  readonly ancho = input('sm:max-w-lg');
  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  constructor() {
    effect(() => {
      const d = this.dialogo().nativeElement;
      if (this.abierto() && !d.open) d.showModal();
      else if (!this.abierto() && d.open) d.close();
    });
  }

  protected clicFondo(e: MouseEvent): void {
    if (e.target === this.dialogo().nativeElement) this.abierto.set(false);
  }
}
