import type { CampanaDto } from '../api/tipos';

const formatoDia = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });

/** "2026-10-30" (día de Colombia, sin hora) → "30 oct 2026". En UTC para que no se corra un día por la zona horaria. */
export const dia = (fecha: string | null | undefined): string =>
  fecha ? formatoDia.format(new Date(`${fecha}T00:00:00Z`)) : '';

/** Hoy en Colombia (UTC−5, sin horario de verano) como "AAAA-MM-DD", para los campos de fecha. */
export const hoyEnColombia = (): string => new Date(Date.now() - 5 * 3_600_000).toISOString().slice(0, 10);

export const estadoCampana = (c: CampanaDto): { texto: string; clase: string } => {
  if (c.activa) return { texto: 'Activa', clase: 'insignia-trueke' };
  return (c.inicio ?? '') > hoyEnColombia() ? { texto: 'Próxima', clase: 'insignia-agua' } : { texto: 'Terminada', clase: 'insignia-neutra' };
};
