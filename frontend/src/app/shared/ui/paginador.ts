import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Icono } from './icono';

@Component({
  selector: 'app-paginador',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (paginas() > 1) {
      <nav class="mt-10 flex items-center justify-center gap-2" aria-label="Paginación">
        <button type="button" class="btn btn-secundario btn-sm" [disabled]="pagina() <= 1" (click)="cambiar.emit(pagina() - 1)">
          <app-icono nombre="izquierda" [tamano]="16" /> Anterior
        </button>
        <span class="px-3 text-sm text-tenue">Página <strong class="text-tinta">{{ pagina() }}</strong> de {{ paginas() }}</span>
        <button type="button" class="btn btn-secundario btn-sm" [disabled]="pagina() >= paginas()" (click)="cambiar.emit(pagina() + 1)">
          Siguiente <app-icono nombre="derecha" [tamano]="16" />
        </button>
      </nav>
    }
  `,
})
export class Paginador {
  readonly pagina = input(1);
  readonly total = input(0);
  readonly tamano = input(12);
  readonly cambiar = output<number>();
  protected readonly paginas = computed(() => Math.max(1, Math.ceil(this.total() / this.tamano())));
}
