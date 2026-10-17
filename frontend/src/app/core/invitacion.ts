/** Código de invitación ("Invita y ganan los dos"): llega en `/registro?invita=CODIGO` y se guarda hasta crear la cuenta. */
export const CLAVE_INVITA = 'trueke.invita';

const PATRON = /^[A-Za-z0-9]{6,12}$/;

export const esCodigoInvitacion = (v: unknown): v is string => typeof v === 'string' && PATRON.test(v);

/** Guarda el código si es válido; devuelve el código guardado (el nuevo o, si el nuevo no sirve, el que ya había). */
export function guardarCodigoInvitacion(crudo: unknown): string | null {
  if (esCodigoInvitacion(crudo)) {
    try {
      localStorage.setItem(CLAVE_INVITA, crudo);
    } catch {
      // Almacenamiento bloqueado: el código no se recuerda.
    }
    return crudo;
  }
  return codigoInvitacionGuardado();
}

export function codigoInvitacionGuardado(): string | null {
  try {
    const v = localStorage.getItem(CLAVE_INVITA);
    return esCodigoInvitacion(v) ? v : null;
  } catch {
    return null;
  }
}

export function olvidarCodigoInvitacion(): void {
  try {
    localStorage.removeItem(CLAVE_INVITA);
  } catch {
    // Almacenamiento bloqueado: nada que borrar.
  }
}
