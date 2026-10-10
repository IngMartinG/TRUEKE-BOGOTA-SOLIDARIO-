import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Proporción ancho/alto del nombre recortado del diseño original (626 × 133 px). */
const PROPORCION_NOMBRE = 626 / 133;
const NOMBRE = 'logo-nombre-160.png 160w, logo-nombre-320.png 320w, logo-nombre-480.png 480w';
const NOMBRE_CLARO = 'logo-nombre-claro-160.png 160w, logo-nombre-claro-320.png 320w, logo-nombre-claro-480.png 480w';

/**
 * Logotipo oficial: el emblema circular y el nombre "Trueke Bogotá Solidario" (con la hoja de la O), ambos recortados del
 * diseño original (docs/diseno/logo-original-1024.png) sin modificar el dibujo. Se sirven ya reducidos en alta calidad
 * (srcset) para que se vean nítidos en cualquier pantalla. En fondos oscuros el gris del nombre pasa a blanco.
 */
@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex items-center gap-2.5' },
  template: `
    <img
      src="logo-emblema-96.png"
      srcset="logo-emblema-48.png 48w, logo-emblema-96.png 96w, logo-emblema-144.png 144w, logo-emblema.png 386w"
      [attr.sizes]="tamano() + 'px'"
      alt=""
      [width]="tamano()"
      [height]="tamano()"
      class="shrink-0 select-none"
      draggable="false"
      decoding="async"
    />
    @if (conTexto()) {
      <span class="leading-none" [class]="claseTexto()">
        @if (!claro()) {
          <img src="logo-nombre-320.png" [attr.srcset]="nombre" [attr.sizes]="anchoNombre() + 'px'" alt="Trueke Bogotá Solidario"
            [width]="anchoNombre()" [height]="altoNombre()" class="block select-none dark:hidden" draggable="false" decoding="async" />
        }
        <img src="logo-nombre-claro-320.png" [attr.srcset]="nombreClaro" [attr.sizes]="anchoNombre() + 'px'" alt="Trueke Bogotá Solidario"
          [width]="anchoNombre()" [height]="altoNombre()" class="select-none" [class]="claro() ? 'block' : 'hidden dark:block'"
          draggable="false" decoding="async" />
      </span>
    }
  `,
})
export class Logo {
  readonly tamano = input(36);
  readonly conTexto = input(true);
  /** Sobre fondos oscuros (pie de página, panel de ingreso): el nombre en blanco. */
  readonly claro = input(false);
  /** Clases del texto, p. ej. para ocultarlo en ciertos anchos de pantalla. */
  readonly claseTexto = input('');

  protected readonly nombre = NOMBRE;
  protected readonly nombreClaro = NOMBRE_CLARO;
  protected readonly altoNombre = computed(() => Math.round(this.tamano() * 0.8));
  protected readonly anchoNombre = computed(() => Math.round(this.altoNombre() * PROPORCION_NOMBRE));
}
