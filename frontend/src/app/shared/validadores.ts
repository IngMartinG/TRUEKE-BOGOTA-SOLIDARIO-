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

/** Misma regla que el backend (Usuario.EdadMinima): la plataforma es solo para mayores de edad. */
export const EDAD_MINIMA = 18;
const EDAD_MAXIMA = 120;

/** Fecha de hoy en Colombia ("yyyy-MM-dd"): siempre UTC-5, no hay horario de verano. */
export function hoyEnColombia(ahora = Date.now()): string {
  return new Date(ahora - 5 * 3_600_000).toISOString().slice(0, 10);
}

/** Resta años a una fecha "yyyy-MM-dd" (un 29 de febrero pasa al 28 en años no bisiestos, como en el backend). */
export function restarAnios(fecha: string, anios: number): string {
  const [a, m, d] = fecha.split('-').map(Number) as [number, number, number];
  const destino = a - anios;
  const ultimoDia = new Date(Date.UTC(destino, m, 0)).getUTCDate();
  return `${destino}-${String(m).padStart(2, '0')}-${String(Math.min(d, ultimoDia)).padStart(2, '0')}`;
}

/** Fecha de nacimiento más reciente permitida: quien cumple 18 hoy ya puede. */
export const fechaMaximaNacimiento = (ahora = Date.now()) => restarAnios(hoyEnColombia(ahora), EDAD_MINIMA);

/** Valida una fecha de nacimiento "yyyy-MM-dd": real, no futura, de 18 años o más y no absurda. */
export const mayorDeEdad: ValidatorFn = (c: AbstractControl): ValidationErrors | null => {
  const v = String(c.value ?? '');
  if (!v) return null;
  const fecha = new Date(`${v}T00:00:00Z`);
  // Descarta formatos raros y fechas inexistentes (31 de febrero se convertiría en marzo).
  const valida = /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(fecha.getTime()) && fecha.toISOString().slice(0, 10) === v;
  if (!valida) return { fechaInvalida: true };
  const hoy = hoyEnColombia();
  if (v > hoy || v < restarAnios(hoy, EDAD_MAXIMA)) return { fechaInvalida: true };
  return v > fechaMaximaNacimiento() ? { menorDeEdad: true } : null;
};
