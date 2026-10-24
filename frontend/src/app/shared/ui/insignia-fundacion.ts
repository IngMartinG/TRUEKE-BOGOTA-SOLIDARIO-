import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icono } from './icono';

/**
 * Insignia "Fundación verificada": un SuperUsuario revisó su NIT y su certificado de la Cámara de Comercio. Usa el color de las
 * donaciones (tierra) para distinguirse de "Identidad verificada" (agua). `compacta`: solo el corazón, con texto para lectores.
 */
@Component({
  selector: 'app-insignia-fundacion',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (compacta()) {
      <app-icono nombre="heart" [tamano]="12" class="shrink-0 text-tierra-600 dark:text-tierra-400" etiqueta="Fundación verificada" />
    } @else {
      <span class="insignia-donacion"><app-icono nombre="heart" [tamano]="12" [grosor]="2.5" />Fundación verificada</span>
    }
  `,
})
export class InsigniaFundacion {
  readonly compacta = input(false);
}
