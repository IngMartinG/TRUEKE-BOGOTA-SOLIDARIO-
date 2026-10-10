import type { PagoIniciadoDto } from '../../api/tipos';

/**
 * El cortafuegos de Wompi rechaza (403 de CloudFront) los pagos cuya dirección de regreso es localhost.
 * En desarrollo local se abre el checkout sin dirección de regreso; en producción (dominio real) se usa normal.
 */
export function esOrigenLocal(hostname = window.location.hostname): boolean {
  return ['localhost', '127.0.0.1', '[::1]', '::1'].includes(hostname) || hostname.endsWith('.localhost');
}

/**
 * URL del Web Checkout de Wompi. La firma de integridad la calcula el backend con el secreto
 * (el front nunca lo conoce); aquí solo se arma la URL pública con los datos del pago.
 */
export function urlCheckoutWompi(pago: PagoIniciadoDto, urlRetorno: string | null): string {
  const params = new URLSearchParams({
    'public-key': pago.llavePublica ?? '',
    currency: pago.moneda ?? 'COP',
    'amount-in-cents': String(pago.montoEnCentavos ?? 0),
    reference: pago.referencia ?? '',
  });
  if (urlRetorno) params.set('redirect-url', urlRetorno);
  if (pago.firmaIntegridad) params.set('signature:integrity', pago.firmaIntegridad);
  return `https://checkout.wompi.co/p/?${params.toString()}`;
}

/**
 * Dirección a la que Wompi devuelve al usuario: la página del pago dentro de la app. Se resuelve contra el
 * `<base href>` (no contra el origen) para que funcione también si la app vive en una subcarpeta,
 * p. ej. https://usuario.github.io/repositorio/pagos/REF.
 */
export function urlRetornoPago(referencia: string, base = document.baseURI): string {
  return new URL(`pagos/${encodeURIComponent(referencia)}`, base).href;
}

/** Producción: redirige en la misma pestaña y Wompi devuelve al usuario a la página del pago. */
export function abrirCheckoutWompi(pago: PagoIniciadoDto, urlRetorno: string): void {
  window.location.assign(urlCheckoutWompi(pago, urlRetorno));
}
