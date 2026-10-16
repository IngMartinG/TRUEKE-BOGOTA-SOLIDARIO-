import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icono } from './icono';

/** Tipo de lugar recomendado para entregar. Nunca lleva el nombre de una sede concreta. */
export interface PuntoEncuentro {
  readonly id: string;
  readonly icono: string;
  readonly nombre: string;
  readonly consejo: string;
  /** Frase que se propone en el chat; la persona la edita antes de enviarla. */
  readonly propuesta: string;
}

export const PUNTOS_ENCUENTRO: readonly PuntoEncuentro[] = [
  {
    id: 'transporte',
    icono: 'bus',
    nombre: 'Estación de TransMilenio',
    consejo: 'O de transporte público. Junto a la taquilla o al personal de seguridad, fuera de la hora pico.',
    propuesta: '¿Nos vemos en una estación de TransMilenio, junto a la taquilla? Propón día y hora.',
  },
  {
    id: 'centro-comercial',
    icono: 'tienda',
    nombre: 'Centro comercial',
    consejo: 'En la entrada principal o la zona de comidas: hay cámaras y vigilancia.',
    propuesta: '¿Nos vemos en un centro comercial, en la entrada principal? Propón día y hora.',
  },
  {
    id: 'biblioteca',
    icono: 'libro',
    nombre: 'Biblioteca pública',
    consejo: 'En el vestíbulo, dentro del horario de atención.',
    propuesta: '¿Nos vemos en una biblioteca pública, en el vestíbulo? Propón día y hora.',
  },
  {
    id: 'cai',
    icono: 'escudo',
    nombre: 'CAI de la Policía',
    consejo: 'Frente al CAI. El mejor lugar para compras y objetos de valor.',
    propuesta: '¿Nos vemos frente a un CAI de la Policía? Propón día y hora.',
  },
];

/**
 * Puntos seguros de encuentro por tipo de lugar.
 * `detalle`: lista con el porqué de cada lugar. `chat`: botones que proponen el lugar en la conversación.
 */
@Component({
  selector: 'app-puntos-encuentro',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (variante() === 'detalle') {
      <ul class="space-y-3">
        @for (p of puntos; track p.id) {
          <li class="flex items-start gap-3">
            <span class="grid size-8 shrink-0 place-items-center rounded-lg bg-white text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200" aria-hidden="true">
              <app-icono [nombre]="p.icono" [tamano]="16" />
            </span>
            <span class="min-w-0">
              <span class="block font-semibold text-bosque-900 dark:text-bosque-100">{{ p.nombre }}</span>
              <span class="block text-bosque-900/80 dark:text-bosque-100/80">{{ p.consejo }}</span>
            </span>
          </li>
        }
      </ul>
    } @else {
      <ul class="flex flex-wrap gap-2" aria-label="Lugares seguros para proponer">
        @for (p of puntos; track p.id) {
          <li>
            <button type="button" class="btn btn-secundario btn-sm" [attr.aria-label]="'Proponer ' + p.nombre" (click)="elegir.emit(p.propuesta)">
              <app-icono [nombre]="p.icono" [tamano]="16" class="text-bosque-600 dark:text-bosque-300" />{{ p.nombre }}
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class PuntosEncuentro {
  readonly variante = input<'detalle' | 'chat'>('detalle');
  /** Solo en `chat`: la frase del lugar elegido. */
  readonly elegir = output<string>();
  protected readonly puntos = PUNTOS_ENCUENTRO;
}
