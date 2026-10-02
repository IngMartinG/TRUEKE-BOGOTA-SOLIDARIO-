import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { iniciales } from '../pipes';
import { Icono } from './icono';

const FONDOS = [
  'bg-bosque-100 text-bosque-800',
  'bg-agua-100 text-agua-700',
  'bg-sol-100 text-sol-600',
  'bg-tierra-100 text-tierra-700',
  'bg-cielo-100 text-cielo-700',
];

/** Avatar con iniciales y color estable según el nombre. */
@Component({
  selector: 'app-avatar',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative inline-flex shrink-0' },
  template: `
    <span
      class="grid place-items-center rounded-full font-display font-bold"
      [class]="fondo()"
      [style.width.px]="tamano()"
      [style.height.px]="tamano()"
      [style.fontSize.px]="tamano() * 0.38"
      >{{ letras() }}</span
    >
    @if (verificado()) {
      <span class="absolute -right-0.5 -bottom-0.5 rounded-full bg-superficie text-agua-600" title="Cuenta verificada">
        <app-icono nombre="verificado" [tamano]="tamano() > 40 ? 18 : 14" etiqueta="Cuenta verificada" />
      </span>
    }
  `,
})
export class Avatar {
  readonly nombre = input<string | null | undefined>('');
  readonly tamano = input(40);
  readonly verificado = input(false);
  protected readonly letras = computed(() => iniciales(this.nombre()));
  protected readonly fondo = computed(() => {
    const n = this.nombre() ?? '';
    let h = 0;
    for (const c of n) h = (h * 31 + c.charCodeAt(0)) >>> 0;
    return FONDOS[h % FONDOS.length];
  });
}
