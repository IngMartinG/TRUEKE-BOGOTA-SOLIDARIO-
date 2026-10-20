import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Indicador de carga con calidez: una mata 🌱 que "respira" (crece y se encoge desde la raíz) mientras se espera.
 * Animación CSS para que siga fluida aunque el hilo principal esté ocupado; con movimiento reducido queda quieta.
 */
@Component({
  selector: 'app-cargando',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col items-center', role: 'status' },
  styles: `
    .mata {
      transform-origin: 50% 90%;
      animation: respirar 900ms cubic-bezier(0.77, 0, 0.175, 1) infinite alternate;
    }
    @keyframes respirar {
      from {
        transform: scale(0.9);
      }
      to {
        transform: scale(1);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .mata {
        animation: none;
      }
    }
  `,
  template: `
    <span class="grid size-20 place-items-center rounded-full bg-bosque-100 shadow-suave dark:bg-bosque-900" aria-hidden="true">
      <span class="mata block text-4xl leading-none">🌱</span>
    </span>
    @if (texto()) {
      <p class="mt-4 text-sm text-tenue">{{ texto() }}</p>
    } @else {
      <span class="sr-only">Cargando…</span>
    }
  `,
})
export class Cargando {
  /** Mensaje visible bajo la mata; si se omite, los lectores de pantalla oyen "Cargando…". */
  readonly texto = input('');
}
