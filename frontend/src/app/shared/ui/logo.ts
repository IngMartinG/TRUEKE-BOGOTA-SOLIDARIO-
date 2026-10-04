import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Logotipo: dos hojas que forman un ciclo (intercambio + economía circular). */
@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex items-center gap-2.5' },
  template: `
    <svg viewBox="0 0 40 40" [attr.width]="tamano()" [attr.height]="tamano()" aria-hidden="true">
      <circle cx="20" cy="20" r="20" [attr.fill]="claro() ? '#ffffff1f' : '#1f7a4d'" />
      <path d="M20 8c6 2 9 6.5 8.5 12.5-4.5.5-8.5-1.5-10.5-5.5-.8-1.8-.9-4.3 2-7Z" fill="#7bcfa3" />
      <path d="M20 32c-6-2-9-6.5-8.5-12.5 4.5-.5 8.5 1.5 10.5 5.5.8 1.8.9 4.3-2 7Z" fill="#f2b632" />
      <path d="M13.5 13.5a9.5 9.5 0 0 1 6.5-4.4M26.5 26.5a9.5 9.5 0 0 1-6.5 4.4" stroke="#fff" stroke-width="1.8" fill="none" stroke-linecap="round" />
    </svg>
    @if (conTexto()) {
      <span class="leading-none" [class]="claseTexto()" [class.text-white]="claro()">
        <span class="block font-display text-[1.05rem] font-extrabold tracking-tight">Trueke</span>
        <span class="block text-[0.68rem] font-semibold tracking-[0.14em] uppercase opacity-70">Bogotá Solidario</span>
      </span>
    }
  `,
})
export class Logo {
  readonly tamano = input(36);
  readonly conTexto = input(true);
  /** Clases del texto, p. ej. para ocultarlo en ciertos anchos de pantalla. */
  readonly claseTexto = input('');
  readonly claro = input(false);
}
