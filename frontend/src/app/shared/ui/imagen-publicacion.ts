import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { iconoCategoria } from '../../api/tipos';
import { urlPublica } from '../imagenes';
import { Icono } from './icono';

/** Foto de la publicación o, si no tiene (o falla), una ilustración según categoría y modo. */
@Component({
  selector: 'app-imagen-publicacion',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block overflow-hidden' },
  template: `
    @if (src() && !fallo()) {
      <img
        [src]="url()"
        [alt]="alt()"
        loading="lazy"
        decoding="async"
        referrerpolicy="no-referrer"
        class="size-full object-cover transition duration-500"
        [class]="claseImagen()"
        (error)="fallo.set(true)"
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
  protected readonly fallo = signal(false);
  protected readonly url = computed(() => urlPublica(this.src()));
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
