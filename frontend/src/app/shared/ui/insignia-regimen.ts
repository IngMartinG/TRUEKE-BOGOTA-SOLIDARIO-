import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Icono } from './icono';

/**
 * Marca prudente del Régimen Tributario Especial: dice a qué régimen pertenece la fundación, nunca que una donación sea
 * deducible (eso depende de cada caso y de lo que certifique la fundación).
 */
@Component({
  selector: 'app-insignia-regimen',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="insignia bg-agua-100 text-agua-700 dark:bg-agua-700/30 dark:text-agua-100"
      title="Lo revisamos con la información pública de la DIAN. Si necesitas un certificado de donación, pídeselo a la fundación.">
      <app-icono nombre="escudo" [tamano]="12" />Régimen Tributario Especial
    </span>
  `,
})
export class InsigniaRegimen {}
