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
    if (e['nombreCompleto']) return 'Escribe tu nombre y apellido, solo con letras (se permiten espacios, apóstrofo, guion y punto).';
    if (e['nombrePersona']) return 'El nombre solo puede tener letras, espacios, apóstrofo, guion y punto.';
    if (e['nombreComercial']) return "El nombre comercial solo puede tener letras, números, espacios y & . , - ' ( ).";
    if (e['razonSocial']) return "La razón social solo puede tener letras, números, espacios y & . , - ' ( ).";
    if (e['localidad']) return 'La localidad o barrio solo puede tener letras, números, espacios y # - . °.';
    if (e['telefono']) return 'Escribe un celular (300 123 4567) o un fijo con indicativo (601 234 5678) de Colombia.';
    if (e['direccion']) return 'La dirección solo puede tener letras, números, espacios y # - . , ° /.';
    if (e['nit']) return 'El NIT debe tener entre 6 y 15 dígitos (puntos opcionales) y, si lo incluyes, el dígito de verificación tras un guion.';
    if (e['nitDv']) return 'El dígito de verificación del NIT no es correcto.';
    if (e['cedula']) return 'La cédula debe tener entre 5 y 10 dígitos, sin letras.';
    if (e['textoInvisible']) return 'El texto tiene caracteres invisibles o de control; bórralos y vuelve a escribirlo.';
    if (e['documentoAlfanumerico']) return 'El documento debe tener entre 3 y 20 letras o números, sin guiones ni símbolos.';
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
