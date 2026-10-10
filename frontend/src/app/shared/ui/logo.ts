import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Logotipo oficial: el emblema circular (manos con corazón sobre la ciudad) y el nombre "Trueke Bogotá Solidario".
 * El emblema es un PNG de 512 px (public/logo-emblema.png): transparente fuera del círculo y con el centro blanco para que
 * las manos se vean también en modo oscuro; el nombre es texto para que se lea nítido
 * en cualquier tamaño y se adapte al modo oscuro.
 */
@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex items-center gap-2.5' },
  template: `
    <img src="logo-emblema.png" alt="" [width]="tamano()" [height]="tamano()" class="shrink-0 select-none" draggable="false" decoding="async" />
    @if (conTexto()) {
      <span class="leading-none" [class]="claseTexto()">
        <span class="block font-display text-[0.95rem] font-extrabold tracking-wide uppercase" [class]="claro() ? 'text-white' : 'text-[#303840] dark:text-white'">
          Trueke Bogotá
        </span>
        <span class="mt-0.5 block bg-gradient-to-r from-[#1aa7b0] to-[#8cc63f] bg-clip-text text-[0.72rem] font-extrabold tracking-[0.22em] text-transparent uppercase">
          Solidario
        </span>
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
}
