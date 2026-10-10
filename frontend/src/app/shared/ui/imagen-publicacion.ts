import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';
import { iconoCategoria } from '../../api/tipos';
import { miniatura, urlPublica } from '../imagenes';
import { Icono } from './icono';

/**
 * Foto de la publicación o, si no tiene (o falla), una ilustración según categoría y modo.
 * Con `miniatura`, primero intenta la versión liviana (tarjetas y listas) y, si no existe, la foto original.
 */
@Component({
  selector: 'app-imagen-publicacion',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block overflow-hidden' },
  template: `
    @if (url(); as u) {
      <img
        [src]="u"
        [alt]="alt()"
        loading="lazy"
        decoding="async"
        referrerpolicy="no-referrer"
        class="size-full object-cover transition duration-500"
        [class]="claseImagen()"
        (error)="intento.set(intento() + 1)"
      />
    } @else {
      <div class="relative grid size-full place-items-center overflow-hidden" [class]="fondo()">
        <svg class="absolute -right-6 -bottom-6 size-32 opacity-20" viewBox="0 0 100 100" aria-hidden="true">
          <path d="M50 5c25 8 38 27 35 52-19 2-35-6-44-23-3-8-4-18 9-29Z" fill="currentColor" />
        </svg>
        <app-icono [nombre]="icono()" [tamano]="44" [grosor]="1.4" class="relative opacity-80" />
      </div>
    }
  `,
})
export class ImagenPublicacion {
  readonly src = input<string | null | undefined>(null);
  readonly alt = input('');
  readonly modo = input<string | null | undefined>('Trueke');
  readonly categoriaId = input<number | null | undefined>(null);
  readonly claseImagen = input('');
  readonly miniatura = input(false);
  /** URLs a probar en orden; cada error de carga pasa a la siguiente y, al agotarlas, se muestra la ilustración. */
  private readonly candidatas = computed(() => {
    const src = this.src();
    if (!src) return [];
    const mini = this.miniatura() ? miniatura(src) : src;
    return (mini === src ? [src] : [mini, src]).map((u) => urlPublica(u));
  });
  protected readonly intento = linkedSignal({ source: this.candidatas, computation: () => 0 });
  protected readonly url = computed(() => this.candidatas()[this.intento()] ?? null);
  protected readonly icono = computed(() => iconoCategoria(this.categoriaId()));
  protected readonly fondo = computed(() => {
    switch (this.modo()) {
      case 'Compra':
        return 'bg-gradient-to-br from-cielo-50 to-cielo-100 text-cielo-700 dark:from-cielo-700/30 dark:to-cielo-700/10 dark:text-cielo-100';
      case 'Donacion':
        return 'bg-gradient-to-br from-tierra-50 to-tierra-100 text-tierra-700 dark:from-tierra-700/30 dark:to-tierra-700/10 dark:text-tierra-100';
      default:
        return 'bg-gradient-to-br from-bosque-50 to-bosque-100 text-bosque-700 dark:from-bosque-900 dark:to-bosque-950 dark:text-bosque-200';
    }
  });
}
