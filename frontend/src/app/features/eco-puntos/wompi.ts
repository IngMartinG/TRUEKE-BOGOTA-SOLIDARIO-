import type { PagoIniciadoDto } from '../../api/tipos';

/**
 * Redirige al Web Checkout de Wompi. La firma de integridad la calcula el backend con el secreto
 * (el front nunca lo conoce); aquí solo se arma la URL pública con los datos del pago.
 */
export function abrirCheckoutWompi(pago: PagoIniciadoDto, urlRetorno: string): void {
  const params = new URLSearchParams({
    'public-key': pago.llavePublica ?? '',
    currency: pago.moneda ?? 'COP',
    'amount-in-cents': String(pago.montoEnCentavos ?? 0),
    reference: pago.referencia ?? '',
    'redirect-url': urlRetorno,
  });
  if (pago.firmaIntegridad) params.set('signature:integrity', pago.firmaIntegridad);
  window.location.assign(`https://checkout.wompi.co/p/?${params.toString()}`);
}
