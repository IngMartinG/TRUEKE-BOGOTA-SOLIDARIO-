import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { fechaMaximaNacimiento } from '../validadores';
import { ErrorCampo } from './error-campo';

/**
 * Fecha de nacimiento con el selector nativo del navegador (el mejor en celular). Solo sirve para comprobar la mayoría de
 * edad: lo dice la ayuda, y nunca se muestra en el perfil público. Usar con el validador `mayorDeEdad`.
 */
@Component({
  selector: 'app-campo-fecha-nacimiento',
  imports: [ReactiveFormsModule, ErrorCampo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="campo">
      <label class="etiqueta" [for]="idCampo()">Fecha de nacimiento</label>
      <input [id]="idCampo()" type="date" class="entrada" [formControl]="control()" autocomplete="bday"
        min="1906-01-01" [max]="maxima" [attr.aria-describedby]="idCampo() + '-ayuda ' + idCampo() + '-error'"
        [attr.aria-invalid]="(control().invalid && control().touched) || null" />
      <p [id]="idCampo() + '-ayuda'" class="ayuda">Trueke es solo para mayores de 18 años. Tu fecha no se muestra a nadie.</p>
      <app-error-campo [control]="control()" etiqueta="La fecha de nacimiento" [idError]="idCampo() + '-error'" />
    </div>
  `,
})
export class CampoFechaNacimiento {
  readonly control = input.required<FormControl<string>>();
  readonly idCampo = input('fecha-nacimiento');
  protected readonly maxima = fechaMaximaNacimiento();
}
