import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { INFO_MODO, type ModoDto } from '../../api/tipos';
import { Icono } from './icono';

@Component({
  selector: 'app-insignia-modo',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [class]="info().clase"><app-icono [nombre]="info().icono" [tamano]="12" [grosor]="2.5" />{{ info().etiqueta }}</span>`,
})
export class InsigniaModo {
  readonly modo = input<string | null | undefined>('Trueke');
  protected readonly info = computed(() => INFO_MODO[(this.modo() as ModoDto) ?? 'Trueke'] ?? INFO_MODO.Trueke);
}
