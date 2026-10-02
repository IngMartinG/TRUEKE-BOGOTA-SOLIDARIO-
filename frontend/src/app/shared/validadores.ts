import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Misma regla que el backend (Usuario.EstablecerClave): 8-128 caracteres con mayúscula, minúscula y número. */
export const claveSegura: ValidatorFn = (c: AbstractControl): ValidationErrors | null => {
  const v = String(c.value ?? '');
  if (!v) return null;
  const ok = v.length >= 8 && v.length <= 128 && /[A-ZÁÉÍÓÚÑ]/.test(v) && /[a-záéíóúñ]/.test(v) && /\d/.test(v);
  return ok ? null : { claveDebil: true };
};

/** Valida que dos controles del grupo coincidan (p. ej. clave y confirmación). */
export function coinciden(campo: string, confirmacion: string): ValidatorFn {
  return (g: AbstractControl): ValidationErrors | null => {
    const a = g.get(campo);
    const b = g.get(confirmacion);
    if (!a || !b) return null;
    const error = b.value && a.value !== b.value;
    const errores = { ...(b.errors ?? {}) };
    if (error) errores['noCoincide'] = true;
    else delete errores['noCoincide'];
    b.setErrors(Object.keys(errores).length ? errores : null);
    return null;
  };
}

/** Requisitos de la contraseña para mostrarlos en vivo. */
export function requisitosClave(v: string): { texto: string; ok: boolean }[] {
  return [
    { texto: 'Al menos 8 caracteres', ok: v.length >= 8 },
    { texto: 'Una mayúscula', ok: /[A-ZÁÉÍÓÚÑ]/.test(v) },
    { texto: 'Una minúscula', ok: /[a-záéíóúñ]/.test(v) },
    { texto: 'Un número', ok: /\d/.test(v) },
  ];
}
