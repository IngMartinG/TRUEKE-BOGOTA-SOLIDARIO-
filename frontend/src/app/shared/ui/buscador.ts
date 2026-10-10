import { ChangeDetectionStrategy, Component, computed, inject, input, model, output, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { Icono } from './icono';

let siguienteId = 0;

/**
 * Caja de búsqueda del catálogo con sugerencias mientras se escribe (combobox accesible: flechas, Enter y Escape).
 * Las sugerencias son títulos de publicaciones visibles (`GET /publicaciones/sugerencias`, sin importar tildes).
 */
@Component({
  selector: 'app-buscador',
  imports: [FormsModule, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // sin clase de display propia: quien lo usa decide (p. ej. "hidden md:block" en el encabezado)
  template: `
    <form class="relative" role="search" (ngSubmit)="enviar(texto())">
      <app-icono nombre="buscar" [tamano]="grande() ? 20 : 18" class="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-tenue" />
      <input
        type="search"
        name="texto"
        [ngModel]="texto()"
        (ngModelChange)="escribir($event)"
        maxlength="100"
        autocomplete="off"
        role="combobox"
        aria-autocomplete="list"
        [attr.aria-expanded]="abierto()"
        [attr.aria-controls]="idLista"
        [attr.aria-activedescendant]="activa() >= 0 ? idLista + '-' + activa() : null"
        [attr.aria-label]="etiqueta()"
        [placeholder]="placeholder()"
        [class]="claseEntrada()"
        (keydown)="tecla($event)"
        (focus)="enfocado.set(true)"
        (blur)="enfocado.set(false)"
      />
      @if (abierto()) {
        <ul
          [id]="idLista"
          role="listbox"
          [attr.aria-label]="'Sugerencias para ' + texto()"
          class="absolute inset-x-0 top-full z-40 mt-2 overflow-hidden rounded-2xl border border-borde bg-superficie py-1 shadow-lg"
        >
          @for (s of sugerencias(); track s; let i = $index) {
            <li
              [id]="idLista + '-' + i"
              role="option"
              [attr.aria-selected]="i === activa()"
              class="flex cursor-pointer items-center gap-3 px-4 py-2.5 text-sm"
              [class.bg-superficie-2]="i === activa()"
              (mousedown)="$event.preventDefault(); enviar(s)"
              (mouseenter)="activa.set(i)"
            >
              <app-icono nombre="buscar" [tamano]="14" class="shrink-0 text-tenue" />
              <span class="truncate">{{ s }}</span>
            </li>
          }
        </ul>
      }
    </form>
  `,
})
export class Buscador {
  private readonly api = inject(CatalogoApi);

  readonly texto = model('');
  readonly placeholder = input('¿Qué estás buscando?');
  readonly etiqueta = input('Buscar publicaciones');
  readonly claseEntrada = input('entrada rounded-full pl-11');
  readonly grande = input(false);
  readonly buscar = output<string>();

  protected readonly idLista = `sugerencias-${++siguienteId}`;
  protected readonly enfocado = signal(false);
  protected readonly activa = signal(-1);
  /** Se apaga al elegir o enviar, para que la lista no reaparezca con el mismo texto. */
  private readonly mostrar = signal(false);

  protected readonly sugerencias = toSignal(
    toObservable(this.texto).pipe(
      map((t) => t.trim()),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((t) => (t.length < 2 ? of([]) : this.api.sugerencias(t).pipe(catchError(() => of([]))))),
    ),
    { initialValue: [] as string[] },
  );

  protected readonly abierto = computed(() => this.enfocado() && this.mostrar() && this.sugerencias().length > 0);

  protected escribir(valor: string): void {
    this.texto.set(valor);
    this.mostrar.set(true);
    this.activa.set(-1);
  }

  protected tecla(e: KeyboardEvent): void {
    const n = this.sugerencias().length;
    if (e.key === 'Escape' && this.abierto()) {
      e.preventDefault();
      this.mostrar.set(false);
    } else if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && n > 0) {
      e.preventDefault();
      this.mostrar.set(true);
      // recorre -1 (lo escrito), 0 … n-1 y vuelve a empezar
      const siguiente = this.activa() + (e.key === 'ArrowDown' ? 1 : -1);
      this.activa.set(siguiente >= n ? -1 : siguiente < -1 ? n - 1 : siguiente);
    } else if (e.key === 'Enter' && this.abierto() && this.activa() >= 0) {
      e.preventDefault();
      this.enviar(this.sugerencias()[this.activa()]);
    }
  }

  protected enviar(valor: string): void {
    this.texto.set(valor);
    this.mostrar.set(false);
    this.activa.set(-1);
    this.buscar.emit(valor.trim());
  }
}
