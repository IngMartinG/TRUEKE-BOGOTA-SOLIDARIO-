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

// ---------------- Formatos de los datos (mismas reglas que el backend: Datos/Common/FormatosTexto.cs) ----------------
// Se toleran espacios al inicio, al final y repetidos: el backend recorta y deja uno solo.

/** Una palabra de un nombre: letras (con tildes, ñ, diéresis) con apóstrofo, guion o punto internos ("O'Neill", "J."). */
const PALABRA = String.raw`\p{L}\p{M}*(?:['’.\-]?\p{L}\p{M}*)*\.?`;
export const RE_NOMBRE_PERSONA = new RegExp(String.raw`^ *${PALABRA}(?: +${PALABRA})* *$`, 'u');
export const RE_NOMBRE_COMPLETO = new RegExp(String.raw`^ *${PALABRA}(?: +${PALABRA})+ *$`, 'u');
export const RE_NOMBRE_COMERCIAL = /^(?=.*[\p{L}0-9]) *[\p{L}\p{M}0-9&.,'’()-]+(?: +[\p{L}\p{M}0-9&.,'’()-]+)* *$/u;
export const RE_LOCALIDAD = /^(?=.*\p{L}) *[\p{L}\p{M}0-9#.°º-]+(?: +[\p{L}\p{M}0-9#.°º-]+)* *$/u;
export const RE_DIRECCION = /^(?=.*\p{L}) *[\p{L}\p{M}0-9#.,°º/-]+(?: +[\p{L}\p{M}0-9#.,°º/-]+)* *$/u;
const RE_NIT = /^ *[0-9][0-9. ]*(?:- *[0-9])? *$/;

function formato(re: RegExp, error: string): ValidatorFn {
  return (c: AbstractControl): ValidationErrors | null => {
    const v = String(c.value ?? '');
    return !v || re.test(v) ? null : { [error]: true };
  };
}

/** Nombre de persona: solo letras, espacio, apóstrofo, guion y punto (una o más palabras). */
export const nombrePersona = formato(RE_NOMBRE_PERSONA, 'nombrePersona');
/** Nombre completo (registro y perfil): como nombrePersona, con nombre y apellido. */
export const nombreCompleto = formato(RE_NOMBRE_COMPLETO, 'nombreCompleto');
/** Nombre comercial o razón social: letras, dígitos, espacio y & . , - ' ( ). */
export const nombreComercial = formato(RE_NOMBRE_COMERCIAL, 'nombreComercial');
/** Localidad o barrio: letras, dígitos, espacio y # - . ° (al menos una letra). */
export const localidad = formato(RE_LOCALIDAD, 'localidad');
/** Dirección: letras, dígitos, espacio y # - . , ° / (al menos una letra). */
export const direccion = formato(RE_DIRECCION, 'direccion');
/** Celular (3XX) o fijo con indicativo (60X) de Colombia, con +57 opcional, espacios o guiones (Datos/Common/FormatosTexto.cs). */
export const RE_TELEFONO = /^ *(?:\+ *57[ -]*)?(?:3[0-9]{2}|60[1-8])[ -]*[0-9]{3}[ -]*[0-9]{4} *$/;
export const telefono = formato(RE_TELEFONO, 'telefono');

const PESOS_NIT = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

/** Dígito de verificación del NIT (módulo 11 de la DIAN, igual que DocumentosFiscales.DigitoVerificacion). */
export function digitoVerificacionNit(numero: string): number {
  let suma = 0;
  for (let i = 0; i < numero.length; i++) suma += Number(numero[numero.length - 1 - i]) * PESOS_NIT[i]!;
  const residuo = suma % 11;
  return residuo <= 1 ? residuo : 11 - residuo;
}

/** null si el NIT es válido; si no, la clave del error ('nit' formato o largo, 'nitDv' dígito de verificación). */
function errorNit(v: string): 'nit' | 'nitDv' | null {
  if (!RE_NIT.test(v)) return 'nit';
  const [numero = '', dv] = v.replace(/[.\s]/g, '').split('-');
  if (numero.length < 6 || numero.length > 15) return 'nit';
  if (dv !== undefined && Number(dv) !== digitoVerificacionNit(numero)) return 'nitDv';
  return null;
}

/** NIT: dígitos y puntos, con el dígito de verificación opcional tras un guion (si viene, debe ser el correcto). */
export const nit: ValidatorFn = (c: AbstractControl): ValidationErrors | null => {
  const v = String(c.value ?? '');
  if (!v) return null;
  const e = errorNit(v);
  return e ? { [e]: true } : null;
};

export type TipoDocumento = 'CC' | 'CE' | 'NIT' | 'Pasaporte';

/** Documento según su tipo (como DocumentosFiscales.Normalizar): CC 5-10 dígitos; CE y Pasaporte 3-20 letras o dígitos. */
export function errorDocumento(tipo: TipoDocumento, valor: string): string | null {
  const d = valor.trim().replace(/[.\s]/g, '');
  switch (tipo) {
    case 'NIT':
      return errorNit(valor);
    case 'CC':
      return /^\d{5,10}$/.test(d) ? null : 'cedula';
    default:
      return /^[A-Za-z0-9]{3,20}$/.test(d) ? null : 'documentoAlfanumerico';
  }
}

/**
 * Validador de grupo: el documento según el tipo elegido y el nombre según sea persona o empresa (razón social si es NIT).
 * Escribe los errores en los controles (como `coinciden`) para que cada campo muestre el suyo.
 */
export function documentoSegunTipo(campoTipo: string, campoDocumento: string, campoNombre?: string): ValidatorFn {
  const CLAVES = ['nit', 'nitDv', 'cedula', 'documentoAlfanumerico', 'nombrePersona', 'razonSocial'];
  const poner = (c: AbstractControl, error: string | null) => {
    const errores = { ...(c.errors ?? {}) };
    for (const k of CLAVES) delete errores[k];
    if (error) errores[error] = true;
    c.setErrors(Object.keys(errores).length ? errores : null);
  };
  return (g: AbstractControl): ValidationErrors | null => {
    const tipo = g.get(campoTipo)?.value as TipoDocumento | undefined;
    const doc = g.get(campoDocumento);
    if (!tipo || !doc) return null;
    const vDoc = String(doc.value ?? '');
    poner(doc, vDoc ? errorDocumento(tipo, vDoc) : null);
    const nombre = campoNombre ? g.get(campoNombre) : null;
    if (nombre) {
      const v = String(nombre.value ?? '');
      const re = tipo === 'NIT' ? RE_NOMBRE_COMERCIAL : RE_NOMBRE_PERSONA;
      poner(nombre, v && !re.test(v) ? (tipo === 'NIT' ? 'razonSocial' : 'nombrePersona') : null);
    }
    return null;
  };
}

/**
 * Para (input) en campos numéricos (CC, NIT): quita lo que no sea dígito, punto, guion o espacio. No bloquea el pegado:
 * lo pegado se limpia igual.
 */
export function filtrarNumerico(evento: Event, control: AbstractControl): void {
  const el = evento.target as HTMLInputElement;
  const limpio = el.value.replace(/[^0-9.\- ]/g, '');
  if (limpio !== el.value) control.setValue(limpio);
}

/** Caracteres invisibles de engaño (inversión de sentido, ancho cero…). El ZWJ (U+200D) se permite: lo usan los emojis. */
const RE_INVISIBLES = /[\u{202A}-\u{202E}\u{2066}-\u{2069}\u{200B}\u{FEFF}\u{2060}\u{180E}]/u;

/** Textos libres (igual que TextoSeguro.cs): sin caracteres de control (salvo saltos de línea si es multilínea) ni invisibles. */
export function textoSeguro(multilinea = false): ValidatorFn {
  return (c: AbstractControl): ValidationErrors | null => {
    const v = String(c.value ?? '');
    const sinSaltos = multilinea ? v.replace(/[\r\n]/g, '') : v;
    // eslint-disable-next-line no-control-regex
    return RE_INVISIBLES.test(v) || /[\u0000-\u001F\u007F-\u009F]/u.test(sinSaltos) ? { textoInvisible: true } : null;
  };
}
