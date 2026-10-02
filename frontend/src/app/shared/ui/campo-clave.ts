import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { requisitosClave } from '../validadores';
import { ErrorCampo } from './error-campo';
import { Icono } from './icono';

/** Campo de contraseña con botón mostrar/ocultar y, opcionalmente, requisitos en vivo. */
@Component({
  selector: 'app-campo-clave',
  imports: [ReactiveFormsModule, Icono, ErrorCampo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="campo">
      <div class="flex items-center justify-between">
        <label class="etiqueta" [for]="idCampo()">{{ etiqueta() }}</label>
        <ng-content select="[accion]" />
      </div>
      <div class="relative">
        <input
          [id]="idCampo()"
          [type]="visible() ? 'text' : 'password'"
          class="entrada pr-12"
          [formControl]="control()"
          [attr.autocomplete]="autocompletar()"
          [attr.aria-invalid]="control().invalid && control().touched"
          [attr.aria-describedby]="idCampo() + '-error'"
          maxlength="128"
        />
        <button
          type="button"
          class="absolute top-1/2 right-2 grid size-9 -translate-y-1/2 place-items-center rounded-full text-tenue hover:bg-superficie-2 hover:text-tinta"
          (click)="visible.set(!visible())"
          [attr.aria-label]="visible() ? 'Ocultar contraseña' : 'Mostrar contraseña'"
          [attr.aria-pressed]="visible()"
        >
          <app-icono [nombre]="visible() ? 'ojoNo' : 'ojo'" [tamano]="18" />
        </button>
      </div>
      @if (mostrarRequisitos()) {
        <div class="mt-1 flex gap-1" aria-hidden="true">
          @for (r of requisitos(); track r.texto) {
            <span class="h-1.5 flex-1 rounded-full transition" [class]="r.ok ? 'bg-bosque-500' : 'bg-borde'"></span>
          }
        </div>
        <ul class="mt-1 grid grid-cols-2 gap-x-3 gap-y-1">
          @for (r of requisitos(); track r.texto) {
            <li class="flex items-center gap-1.5 text-xs" [class]="r.ok ? 'text-bosque-700 dark:text-bosque-300' : 'text-tenue'">
              <app-icono [nombre]="r.ok ? 'checkCirculo' : 'info'" [tamano]="13" />{{ r.texto }}
            </li>
          }
        </ul>
      } @else {
        <app-error-campo [control]="control()" [etiqueta]="etiqueta()" [idError]="idCampo() + '-error'" />
      }
    </div>
  `,
})
export class CampoClave {
  readonly control = input.required<FormControl<string>>();
  readonly etiqueta = input('Contraseña');
  readonly idCampo = input('clave');
  readonly autocompletar = input<'current-password' | 'new-password'>('current-password');
  readonly mostrarRequisitos = input(false);
  protected readonly visible = signal(false);
  private readonly valor = signal('');
  protected readonly requisitos = computed(() => requisitosClave(this.valor()));

  constructor() {
    effect((alLimpiar) => {
      const c = this.control();
      this.valor.set(c.value ?? '');
      const sub = c.valueChanges.subscribe((v) => this.valor.set(v ?? ''));
      alLimpiar(() => sub.unsubscribe());
    });
  }
}
