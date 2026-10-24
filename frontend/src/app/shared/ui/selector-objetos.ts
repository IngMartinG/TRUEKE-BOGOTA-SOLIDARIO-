import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { infoCondicion, type PublicacionDto } from '../../api/tipos';
import { Icono } from './icono';
import { ImagenPublicacion } from './imagen-publicacion';

/** Objetos que se pueden ofrecer a cambio de uno en un trueke (la API rechaza más). */
export const MAX_OBJETOS_OFRECIDOS = 3;

/**
 * Elegir de 0 a 3 publicaciones para un trueke (al proponer o al contraofertar). `objetos` null = cargando.
 * Al llegar al máximo, las demás casillas se desactivan en lugar de quitar la primera elegida.
 */
@Component({
  selector: 'app-selector-objetos',
  imports: [ImagenPublicacion, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (objetos() === null) {
      <div class="esqueleto h-16"></div>
    } @else {
      <div class="grid gap-2">
        @for (o of objetos(); track o.id) {
          @let elegido = seleccion().includes(o.id!);
          <label class="flex items-center gap-3 rounded-2xl border-2 p-2 text-sm transition has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-bosque-500"
            [class]="elegido ? 'cursor-pointer border-bosque-500 bg-bosque-50 dark:bg-bosque-900/50' : lleno() ? 'cursor-not-allowed border-borde opacity-60' : 'cursor-pointer border-borde hover:border-bosque-200'">
            <input type="checkbox" class="sr-only" [checked]="elegido" [disabled]="!elegido && lleno()" (change)="alternar(o.id!)"
              [attr.aria-label]="prefijo() + ' ' + o.titulo" />
            <app-imagen-publicacion class="size-12 shrink-0 rounded-xl" [src]="o.imagenes?.[0]" [miniatura]="true" [modo]="o.modo" [categoriaId]="o.categoria?.id" />
            <span class="min-w-0 flex-1">
              <span class="block truncate font-semibold">{{ o.titulo }}</span>
              <span class="block text-xs text-tenue">{{ condicion(o.condicion) }}</span>
            </span>
            <span class="grid size-6 shrink-0 place-items-center rounded-md border-2"
              [class]="elegido ? 'border-bosque-600 bg-bosque-600 text-white' : 'border-borde'" aria-hidden="true">
              @if (elegido) {
                <app-icono nombre="check" [tamano]="14" [grosor]="3" />
              }
            </span>
          </label>
        } @empty {
          <ng-content />
        }
      </div>
      @if (objetos()?.length) {
        <p class="ayuda mt-2" aria-live="polite">{{ seleccion().length }} de {{ max }} elegidos{{ lleno() ? ': quita uno para cambiarlo.' : '.' }}</p>
      }
    }
  `,
})
export class SelectorObjetos {
  readonly objetos = input<PublicacionDto[] | null>(null);
  /** Ids elegidos, en el orden en que se eligieron (el primero es el principal en la tarjeta). */
  readonly seleccion = model<string[]>([]);
  /** Inicio de la etiqueta accesible de cada casilla: "Ofrecer Bicicleta". */
  readonly prefijo = input('Ofrecer');
  protected readonly max = MAX_OBJETOS_OFRECIDOS;
  protected readonly lleno = computed(() => this.seleccion().length >= MAX_OBJETOS_OFRECIDOS);
  protected readonly condicion = (c: string | null | undefined) => infoCondicion(c).etiqueta;

  protected alternar(id: string): void {
    this.seleccion.update((s) => (s.includes(id) ? s.filter((x) => x !== id) : s.length < MAX_OBJETOS_OFRECIDOS ? [...s, id] : s));
  }
}
