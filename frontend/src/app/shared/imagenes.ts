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
