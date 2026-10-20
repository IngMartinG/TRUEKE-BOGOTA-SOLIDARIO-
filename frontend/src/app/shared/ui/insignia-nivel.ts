import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { infoNivel } from '../../api/tipos';

/** Nivel de Eco-Puntos de una persona. Por defecto no se muestra en Semilla (el nivel de partida), para no llenar todo de insignias. */
@Component({
  selector: 'app-insignia-nivel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <span [class]="info().clase" [attr.title]="'Nivel ' + info().etiqueta + ' en Eco-Puntos'">
        <span aria-hidden="true">{{ info().emoji }}</span>{{ info().etiqueta }}<span class="sr-only"> (nivel en Eco-Puntos)</span>
      </span>
    }
  `,
})
export class InsigniaNivel {
  readonly nivel = input<string | null | undefined>();
  /** true = también se muestra en Semilla (perfil propio o público completo). */
  readonly siempre = input(false);
  protected readonly info = computed(() => infoNivel(this.nivel()));
  protected readonly visible = computed(() => !!this.nivel() && (this.siempre() || this.nivel() !== 'Semilla'));
}
