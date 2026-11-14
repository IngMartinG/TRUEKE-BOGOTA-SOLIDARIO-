/**
 * Ropa interior, medias y trajes de baño solo se publican como «Nuevo» (por higiene). El servidor es quien decide; esta lista
 * solo avisa antes de enviar y debe ser la misma de `ArticulosDeHigiene` (backend/…/Datos/Common/ArticulosDeHigiene.cs).
 */
const TERMINOS = [
  'ropa interior', 'calzoncillo', 'calzoncillos', 'calzon', 'calzones', 'panty', 'pantys', 'panties', 'pantaleta', 'pantaletas',
  'brasier', 'brasieres', 'sosten', 'sostenes', 'boxer', 'boxers', 'tanga', 'tangas', 'lenceria', 'lingerie',
  'medias', 'calcetin', 'calcetines', 'calceta', 'calcetas',
  'traje de bano', 'trajes de bano', 'vestido de bano', 'vestidos de bano', 'ropa de bano', 'bikini', 'bikinis',
  'banador', 'banadores',
];

export const MENSAJE_HIGIENE =
  'Por higiene, la ropa interior, las medias y los trajes de baño solo se publican como «Nuevo». Si tu artículo no es de ese tipo, ajusta el título.';

/** Minúsculas, sin tildes y con espacios en los extremos y entre palabras (como el servidor). */
function normalizar(texto: string): string {
  const limpio = texto.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim();
  return ` ${limpio} `;
}

/** true si el título nombra un artículo de higiene. */
export function esArticuloDeHigiene(titulo: string | null | undefined): boolean {
  const normal = normalizar(titulo ?? '');
  return TERMINOS.some((t) => normal.includes(` ${t} `));
}
