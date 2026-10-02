import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  NgZone,
  output,
  viewChild,
} from '@angular/core';
import { GoogleService } from '../../core/terceros/google.service';

/** Botón oficial de Google Identity Services. Solo se muestra si la API publica un Client ID. */
@Component({
  selector: 'app-boton-google',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (google.habilitado) {
      <div class="relative my-6 flex items-center gap-3 text-xs text-tenue">
        <span class="h-px flex-1 bg-borde"></span>o<span class="h-px flex-1 bg-borde"></span>
      </div>
      <div class="flex justify-center" [class.pointer-events-none]="deshabilitado()" [class.opacity-50]="deshabilitado()">
        <div #contenedor class="flex min-h-11 w-full justify-center"></div>
      </div>
    }
  `,
})
export class BotonGoogle {
  protected readonly google = inject(GoogleService);
  private readonly zona = inject(NgZone);
  readonly texto = input<'signin_with' | 'signup_with' | 'continue_with'>('continue_with');
  readonly deshabilitado = input(false);
  readonly token = output<string>();
  private readonly contenedor = viewChild<ElementRef<HTMLElement>>('contenedor');

  constructor() {
    afterNextRender(() => {
      const el = this.contenedor()?.nativeElement;
      if (el) {
        void this.google.renderizarBoton(el, (t) => this.zona.run(() => this.token.emit(t)), this.texto());
      }
    });
  }
}
