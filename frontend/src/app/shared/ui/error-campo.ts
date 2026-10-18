import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { AbstractControl } from '@angular/forms';

/**
 * Mensaje de error de un control reactivo. Se muestra cuando la persona sale del campo (touched) o al enviar el
 * formulario (markAllAsTouched), no mientras escribe, e incluye los errores que devuelve la API (`servidor`).
 * La región existe siempre y se anuncia de forma amable (polite); `idError` debe ser `<id del campo>-error`
 * para que `CampoAccesible` la asocie al campo.
 */
@Component({
  selector: 'app-error-campo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p class="error-campo empty:hidden" [id]="idError()" aria-live="polite">{{ mensaje() }}</p>`,
})
export class ErrorCampo {
  readonly control = input.required<AbstractControl>();
  readonly etiqueta = input('Este campo');
  readonly idError = input<string | null>(null);
  private readonly cambio = signal(0);

  constructor() {
    effect((alLimpiar) => {
      const sub = this.control().events.subscribe(() => this.cambio.update((v) => v + 1));
      alLimpiar(() => sub.unsubscribe());
    });
  }

  protected readonly mensaje = computed(() => {
    this.cambio();
    const c = this.control();
    if (!c.errors || !c.touched) return null;
    const e = c.errors;
    if (e['servidor']) return e['servidor'] as string;
    if (e['required']) return `${this.etiqueta()} es obligatorio.`;
    if (e['email']) return 'Escribe un correo válido, por ejemplo nombre@correo.com.';
    if (e['minlength']) return `Debe tener al menos ${e['minlength'].requiredLength} caracteres.`;
    if (e['maxlength']) return `Máximo ${e['maxlength'].requiredLength} caracteres.`;
    if (e['min']) return `El valor mínimo es ${e['min'].min}.`;
    if (e['max']) return `El valor máximo es ${e['max'].max}.`;
    if (e['pattern']) return `${this.etiqueta()} no tiene el formato esperado.`;
    if (e['requiredTrue']) return 'Debes aceptar para continuar.';
    if (e['claveDebil']) return 'Usa al menos 8 caracteres con mayúscula, minúscula y número.';
    if (e['noCoincide']) return 'Las contraseñas no coinciden.';
    if (e['menorDeEdad']) return 'Trueke Bogotá Solidario es solo para personas mayores de 18 años.';
    if (e['fechaInvalida']) return 'Revisa la fecha: no parece correcta.';
    return `${this.etiqueta()} no es válido.`;
  });
}

/** Aplica a los controles los errores por campo que devolvió la API. */
export function aplicarErroresServidor(
  controles: Record<string, AbstractControl>,
  errores: Record<string, string>,
): boolean {
  let alguno = false;
  for (const [campo, mensaje] of Object.entries(errores)) {
    const c = controles[campo];
    if (c) {
      c.setErrors({ ...(c.errors ?? {}), servidor: mensaje });
      c.markAsTouched();
      alguno = true;
    }
  }
  return alguno;
}
