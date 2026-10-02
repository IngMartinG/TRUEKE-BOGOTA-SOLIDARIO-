const cargados = new Map<string, Promise<void>>();

/** Carga un script de terceros una sola vez (reCAPTCHA, Google Identity Services). */
export function cargarScript(src: string): Promise<void> {
  let promesa = cargados.get(src);
  if (!promesa) {
    promesa = new Promise<void>((resolver, rechazar) => {
      const s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.defer = true;
      s.onload = () => resolver();
      s.onerror = () => {
        cargados.delete(src);
        rechazar(new Error(`No se pudo cargar ${new URL(src).host}`));
      };
      document.head.appendChild(s);
    });
    cargados.set(src, promesa);
  }
  return promesa;
}
