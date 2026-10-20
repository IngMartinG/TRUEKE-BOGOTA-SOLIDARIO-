/**
 * Regreso del enlace de pago de Wompi (aportes de "Apoya el proyecto"): Wompi agrega `?id=<transacción>&env=<test|prod>`
 * a la dirección de regreso. El estado se consulta en la API pública de Wompi (solo lectura, sin llaves).
 */
export type EstadoAporte = 'APPROVED' | 'PENDING' | 'DECLINED' | 'VOIDED' | 'ERROR';

export interface Aporte {
  estado: EstadoAporte;
  /** Valor en pesos (null si Wompi no lo informó). */
  valor: number | null;
}

/** Los ids de Wompi son como "12345-1668624561-38705": letras, números y guiones. */
const PATRON_ID = /^[A-Za-z0-9-]{6,64}$/;

export function esRegresoWompi(id: unknown, env: unknown): id is string {
  return typeof id === 'string' && PATRON_ID.test(id) && (env === 'test' || env === 'prod');
}

export function urlTransaccionWompi(id: string, env: string): string {
  const base = env === 'test' ? 'https://sandbox.wompi.co' : 'https://production.wompi.co';
  return `${base}/v1/transactions/${encodeURIComponent(id)}`;
}

/**
 * Consulta la transacción con fetch (no con HttpClient: los interceptores añadirían la sesión de la API a un tercero).
 * Solo se toman el estado y el valor; el resto de la respuesta (correo, medio de pago…) se descarta.
 */
export async function consultarAporte(id: string, env: string, senal?: AbortSignal): Promise<Aporte> {
  const r = await fetch(urlTransaccionWompi(id, env), {
    headers: { Accept: 'application/json' },
    credentials: 'omit',
    referrerPolicy: 'no-referrer',
    signal: senal,
  });
  if (!r.ok) throw new Error(`Wompi respondió ${r.status}`);
  const cuerpo = (await r.json()) as { data?: { status?: string; amount_in_cents?: number } };
  const estado = cuerpo.data?.status;
  if (estado !== 'APPROVED' && estado !== 'PENDING' && estado !== 'DECLINED' && estado !== 'VOIDED' && estado !== 'ERROR')
    throw new Error('Estado desconocido');
  const centavos = cuerpo.data?.amount_in_cents;
  return { estado, valor: typeof centavos === 'number' && centavos > 0 ? Math.round(centavos / 100) : null };
}
