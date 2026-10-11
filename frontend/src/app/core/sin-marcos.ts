/**
 * Protección contra "clickjacking" donde no se pueden enviar cabeceras (GitHub Pages no permite
 * `frame-ancestors` ni `X-Frame-Options`, y `frame-ancestors` no funciona en un `<meta>`).
 * Si la app se carga dentro de un iframe de otro sitio, no arranca: muestra un enlace para abrirla aparte.
 * Con nginx (Docker) la cabecera ya lo impide; esto es la segunda capa.
 */
export function cargadaEnMarcoAjeno(w: Window = window): boolean {
  try {
    return w.top !== w.self;
  } catch {
    return true; // acceder a window.top de otro origen lanza: está dentro de un marco ajeno
  }
}

export function mostrarAvisoDeMarco(d: Document = document): void {
  const enlace = d.createElement('a');
  enlace.href = d.location.href;
  enlace.target = '_top';
  enlace.rel = 'noopener';
  enlace.textContent = 'Abre Trueke Bogotá Solidario en su propia pestaña';
  enlace.style.cssText = 'display:block;margin:2rem;font:16px system-ui,sans-serif;color:#123d27';
  d.body.replaceChildren(enlace);
}
