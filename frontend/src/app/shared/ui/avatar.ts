import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';
import { miniatura, urlPublica } from '../imagenes';
import { iniciales } from '../pipes';
import { Icono } from './icono';

const FONDOS = [
  'bg-bosque-100 text-bosque-800',
  'bg-agua-100 text-agua-700',
  'bg-sol-100 text-sol-600',
  'bg-tierra-100 text-tierra-700',
  'bg-cielo-100 text-cielo-700',
];

/** Foto de perfil redonda; sin foto (o si no carga) muestra las iniciales con un color estable según el nombre. */
@Component({
  selector: 'app-avatar',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative inline-flex shrink-0' },
  template: `
    @if (fotoVisible(); as src) {
      <img
        [src]="src"
        [alt]="'Foto de ' + (nombre() || 'perfil')"
        class="rounded-full bg-superficie-2 object-cover"
        [style.width.px]="tamano()"
        [style.height.px]="tamano()"
        loading="lazy"
        decoding="async"
        referrerpolicy="no-referrer"
        (error)="intento.set(intento() + 1)"
      />
    } @else {
      <span
        class="grid place-items-center rounded-full font-display font-bold"
        [class]="fondo()"
        [style.width.px]="tamano()"
        [style.height.px]="tamano()"
        [style.fontSize.px]="tamano() * 0.38"
        >{{ letras() }}</span
      >
    }
    @if (verificado()) {
      <span class="absolute -right-0.5 -bottom-0.5 rounded-full bg-superficie text-agua-600" title="Cuenta verificada">
        <app-icono nombre="verificado" [tamano]="tamano() > 40 ? 18 : 14" etiqueta="Cuenta verificada" />
      </span>
    }
  `,
})
export class Avatar {
  readonly nombre = input<string | null | undefined>('');
  readonly foto = input<string | null | undefined>(null);
  readonly tamano = input(40);
  readonly verificado = input(false);
  /** Primero la miniatura (un avatar nunca necesita la foto grande); si no carga, la original; luego las iniciales. */
  private readonly candidatas = computed(() => {
    const foto = this.foto();
    if (!foto) return [];
    const mini = miniatura(foto);
    return (mini === foto ? [foto] : [mini, foto]).map((u) => urlPublica(u));
  });
  /** Se reinicia cada vez que cambia la foto. */
  protected readonly intento = linkedSignal({ source: this.candidatas, computation: () => 0 });
  protected readonly fotoVisible = computed(() => this.candidatas()[this.intento()] ?? null);
  protected readonly letras = computed(() => iniciales(this.nombre()));
  protected readonly fondo = computed(() => {
    const n = this.nombre() ?? '';
    let h = 0;
    for (const c of n) h = (h * 31 + c.charCodeAt(0)) >>> 0;
    return FONDOS[h % FONDOS.length];
  });
}
