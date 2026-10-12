/** true si la persona pidió reducir el movimiento en su sistema: desplazamientos y zoom sin animación. */
export function reducirMovimiento(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/** Comportamiento de desplazamiento que respeta "reducir movimiento". */
export function desplazamiento(): ScrollBehavior {
  return reducirMovimiento() ? 'auto' : 'smooth';
}
