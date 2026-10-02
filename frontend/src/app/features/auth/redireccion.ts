/**
 * Solo permite volver a rutas internas de la app (evita redirecciones abiertas
 * del tipo ?volver=https://sitio-malicioso.com o //sitio.com).
 */
export function destinoSeguro(volver: string | null | undefined, porDefecto = '/explorar'): string {
  if (!volver || !volver.startsWith('/') || volver.startsWith('//') || volver.startsWith('/\\')) return porDefecto;
  if (volver.startsWith('/ingresar') || volver.startsWith('/registro')) return porDefecto;
  return volver;
}
