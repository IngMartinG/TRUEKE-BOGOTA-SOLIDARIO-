import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ErrorCampo } from './error-campo';

/**
 * Las dos casillas legales, separadas y sin marcar de antemano (Ley 1581: la autorización de datos es previa, expresa e
 * informada, y no se mezcla con la aceptación de los términos). Se usan al registrarse, al crear la cuenta con Google y
 * cuando cambia una versión.
 */
@Component({
  selector: 'app-casillas-legales',
  imports: [ReactiveFormsModule, RouterLink, ErrorCampo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3">
      <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-borde p-3 transition hover:bg-superficie-2"
        [class.!border-tierra-500]="datos().invalid && datos().touched">
        <input type="checkbox" class="casilla mt-0.5" [formControl]="datos()" [attr.aria-describedby]="prefijo() + '-datos-error'" />
        <span class="text-sm">
          Autorizo el tratamiento de mis datos personales según la
          <a routerLink="/privacidad" target="_blank" class="enlace">política de tratamiento de datos<span class="sr-only"> (abre otra pestaña)</span></a>
          (Ley 1581 de 2012).
        </span>
      </label>
      <app-error-campo [control]="datos()" [idError]="prefijo() + '-datos-error'" />

      <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-borde p-3 transition hover:bg-superficie-2"
        [class.!border-tierra-500]="terminos().invalid && terminos().touched">
        <input type="checkbox" class="casilla mt-0.5" [formControl]="terminos()" [attr.aria-describedby]="prefijo() + '-terminos-error'" />
        <span class="text-sm">
          Acepto los
          <a routerLink="/terminos" target="_blank" class="enlace">términos de uso<span class="sr-only"> (abre otra pestaña)</span></a>
          y declaro que soy mayor de 18 años.
        </span>
      </label>
      <app-error-campo [control]="terminos()" [idError]="prefijo() + '-terminos-error'" />
    </div>
  `,
})
export class CasillasLegales {
  readonly datos = input.required<FormControl<boolean>>();
  readonly terminos = input.required<FormControl<boolean>>();
  /** Prefijo de los id de error (varios formularios en la misma página). */
  readonly prefijo = input('legal');
}
