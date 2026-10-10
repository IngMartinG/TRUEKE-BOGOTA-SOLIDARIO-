/** Emulador de Azure Blob (Azurite) en el equipo de desarrollo. */
const EMULADOR_BLOB = /^https?:\/\/(?:127\.0\.0\.1|localhost):10000\//i;

/** CDN opcional delante de Blob (la API la publica en `GET /configuracion`; ver ConfigService). */
let cdn: { origen: string; destino: string } | null = null;

export function configurarCdn(origen: string | null | undefined, destino: string | null | undefined): void {
  cdn = origen && destino ? { origen, destino } : null;
}

/**
 * En desarrollo las fotos viven en Azurite (`http://127.0.0.1:10000/...`), que solo existe en el equipo del desarrollador.
 * Se reescriben a una ruta del mismo sitio (`/devstoreaccount1/...`) que el servidor de Angular reenvía al emulador
 * (proxy.conf.json): así se ven también desde otros equipos (túnel de VS Code, celular) y sin contenido mixto http/https.
 * Con CDN configurada, las fotos de Blob se descargan desde la CDN. La API guarda y recibe siempre la URL de Blob:
 * esta función es solo para mostrar (nunca para enviar de vuelta).
 */
export function urlPublica(url: string): string;
export function urlPublica(url: string | null | undefined): string | null | undefined;
export function urlPublica(url: string | null | undefined): string | null | undefined {
  if (!url) return url;
  if (cdn && url.startsWith(cdn.origen)) return cdn.destino + url.slice(cdn.origen.length);
  return url.replace(EMULADOR_BLOB, '/');
}

/** Foto publicada por la plataforma: {usuario}/{aleatorio}.{jpg|png|webp} (igual que ReglasArchivos en la API). */
const FOTO_PUBLICADA = /(\/[0-9a-f]{32}\/[0-9a-f]{32})\.(?:jpg|png|webp)$/;

/**
 * URL de la miniatura WEBP (640 px) que la API crea junto a cada foto: `…/abc.jpg` → `…/abc.min.webp`.
 * Para tarjetas, listas y avatares; la foto grande queda para el detalle y el visor. Si la URL no es una foto
 * de la plataforma, se devuelve igual. Quien la use debe volver a la foto original si la miniatura no carga.
 */
export function miniatura(url: string): string;
export function miniatura(url: string | null | undefined): string | null | undefined;
export function miniatura(url: string | null | undefined): string | null | undefined {
  return url ? url.replace(FOTO_PUBLICADA, '$1.min.webp') : url;
}

/** Para un `<img>` que muestra una miniatura: si no carga, usa la foto original (una sola vez). */
export function usarOriginal(evento: Event, url: string | null | undefined): void {
  const img = evento.target as HTMLImageElement;
  if (!url || img.dataset['original']) return;
  img.dataset['original'] = '1';
  img.src = urlPublica(url);
}

/**
 * Reduce fotos grandes (las de celular suelen pesar 4-10 MB) antes de subirlas:
 * lado mayor ≤ 1600 px y recompresión a WebP (o JPEG si el navegador no la soporta).
 * Además elimina los metadatos EXIF (ubicación GPS de la foto), que no se copian al lienzo.
 */
export async function comprimirImagen(archivo: File, ladoMaximo = 1600, calidad = 0.82): Promise<File> {
  if (!archivo.type.startsWith('image/')) return archivo;
  let mapa: ImageBitmap;
  try {
    mapa = await createImageBitmap(archivo, { imageOrientation: 'from-image' });
  } catch {
    return archivo;
  }
  const escala = Math.min(1, ladoMaximo / Math.max(mapa.width, mapa.height));
  const ancho = Math.round(mapa.width * escala);
  const alto = Math.round(mapa.height * escala);
  const lienzo = document.createElement('canvas');
  lienzo.width = ancho;
  lienzo.height = alto;
  const ctx = lienzo.getContext('2d');
  if (!ctx) return archivo;
  ctx.drawImage(mapa, 0, 0, ancho, alto);
  mapa.close();

  for (const tipo of ['image/webp', 'image/jpeg']) {
    const blob = await new Promise<Blob | null>((ok) => lienzo.toBlob(ok, tipo, calidad));
    if (blob && blob.type === tipo) {
      // Siempre se usa la versión recodificada, aunque pese un poco más: así nunca viaja la ubicación GPS.
      const extension = tipo === 'image/webp' ? 'webp' : 'jpg';
      return new File([blob], archivo.name.replace(/\.[^.]+$/, '') + '.' + extension, { type: tipo });
    }
  }
  return archivo;
}
