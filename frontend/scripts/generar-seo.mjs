// Genera lo que necesitan los buscadores en el build de GitHub Pages:
//  - Una copia de index.html por cada ruta pública (explorar/index.html…). Sin ella, Pages sirve esas rutas desde
//    404.html y responde HTTP 404: la página se ve bien, pero Google no la indexa.
//  - publicacion/{id}/index.html para cada publicación del catálogo público, con título, descripción y canonical propios.
//  - sitemap.xml y robots.txt.
// Si la API no responde (plan gratis dormido), publica solo las rutas estáticas: el despliegue nunca falla por esto.
// Uso: node scripts/generar-seo.mjs <carpeta dist/browser> <URL pública del front> [URL de la API]
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [dist, frontArg, apiArg] = process.argv.slice(2);
if (!dist || !frontArg) {
  console.error('Uso: node scripts/generar-seo.mjs <dist> <url-front> [url-api]');
  process.exit(1);
}
const front = frontArg.replace(/\/?$/, '/');
const api = apiArg ? apiArg.replace(/\/$/, '') : '';
const marca = 'Trueke Bogotá Solidario';

// Rutas públicas que vale la pena indexar (las privadas exigen sesión y van en Disallow).
const estaticas = [
  ['', 'Intercambia, compra y dona', 'daily', '1.0'],
  ['explorar', 'Explorar', 'hourly', '0.9'],
  ['como-funciona', 'Cómo funciona', 'monthly', '0.7'],
  ['impacto', 'Impacto de la comunidad', 'weekly', '0.6'],
  ['ayuda', 'Centro de ayuda', 'monthly', '0.6'],
  ['eco-puntos', 'Eco-Puntos', 'monthly', '0.6'],
  ['planes', 'Planes', 'monthly', '0.5'],
  ['pqrs', 'PQRS: peticiones, quejas y reclamos', 'yearly', '0.4'],
  ['privacidad', 'Política de tratamiento de datos', 'yearly', '0.3'],
  ['terminos', 'Términos de uso', 'yearly', '0.3'],
  ['accesibilidad', 'Declaración de accesibilidad', 'yearly', '0.3'],
  ['ingresar', 'Ingresar', 'yearly', '0.3'],
  ['registro', 'Crear cuenta', 'yearly', '0.5'],
];
const privadas = [
  'publicar', 'mis-publicaciones', 'favoritos', 'lo-que-busco', 'intercambios', 'mensajes', 'bienvenida',
  'notificaciones', 'pagos', 'cuenta', 'admin', 'olvide-clave', 'restablecer-clave', 'verificar-correo',
  'completar-perfil', 'verifica-tu-correo',
];

const html = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
const corto = (s, n) => (s.length <= n ? s : `${s.slice(0, n - 1).trimEnd()}…`);
const plano = (s) => (s ?? '').replace(/\s+/g, ' ').trim();

const base = readFileSync(join(dist, 'index.html'), 'utf8');

/** index.html con título, descripción, canonical y og: propios de la página. */
function pagina({ titulo, descripcion, url, imagen }) {
  let h = base
    .replace(/<title>[\s\S]*?<\/title>/, `<title>${html(titulo)}</title>`)
    .replace(/(<meta\s+property="og:title"\s+content=")[^"]*"/, `$1${html(titulo)}"`)
    .replace(/(<meta\s+property="og:url"\s+content=")[^"]*"/, `$1${html(url)}"`)
    .replace(/<\/head>/, `  <link rel="canonical" href="${html(url)}" />\n  </head>`);
  if (descripcion) {
    h = h
      .replace(/(<meta\s+name="description"\s+content=")[^"]*"/, `$1${html(descripcion)}"`)
      .replace(/(<meta\s+property="og:description"\s+content=")[^"]*"/, `$1${html(descripcion)}"`);
  }
  if (imagen) {
    h = h
      .replace(/(<meta\s+property="og:image"\s+content=")[^"]*"/, `$1${html(imagen)}"`)
      .replace(/(<meta\s+property="og:image:type"\s+content=")[^"]*"/, '$1image/jpeg"');
  }
  return h;
}

function escribir(ruta, contenido) {
  const carpeta = join(dist, ruta);
  mkdirSync(carpeta, { recursive: true });
  writeFileSync(join(carpeta, 'index.html'), contenido);
}

async function catalogo() {
  if (!api) return null;
  const todas = [];
  for (let numero = 1; numero <= 200; numero++) {
    const url = `${api}/api/v1/publicaciones?Pagina=${numero}&Tamano=50`;
    let datos = null;
    for (let intento = 1; intento <= 3 && !datos; intento++) {
      try {
        // El primer pedido puede despertar la API (30-60 s en el plan gratis).
        const r = await fetch(url, { signal: AbortSignal.timeout(90_000), headers: { Accept: 'application/json' } });
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        datos = await r.json();
      } catch (e) {
        console.log(`Catálogo, página ${numero}, intento ${intento}: ${e.message}`);
      }
    }
    if (!datos) return todas.length ? todas : null;
    todas.push(...datos.items);
    if (datos.items.length === 0 || todas.length >= datos.total) break;
  }
  return todas;
}

const urls = [];
const hoy = new Date().toISOString().slice(0, 10);

for (const [ruta, titulo, frecuencia, prioridad] of estaticas) {
  const url = `${front}${ruta ? `${ruta}/` : ''}`;
  if (ruta) escribir(ruta, pagina({ titulo: `${titulo} · ${marca}`, url }));
  urls.push({ url, lastmod: hoy, frecuencia, prioridad });
}

const publicaciones = await catalogo();
if (publicaciones === null) {
  console.log('::warning::No se pudo leer el catálogo de la API: el sitemap solo lleva las rutas estáticas.');
} else {
  for (const p of publicaciones) {
    if (!/^[0-9a-f-]{36}$/i.test(p.id)) continue;
    const url = `${front}publicacion/${p.id}/`;
    const lugar = [p.localidad, p.municipio].filter(Boolean).join(', ');
    const encabezado = `${p.modo}${lugar ? ` en ${lugar}` : ''}`.replace(/\.$/, ''); // "Bogotá, D.C." ya trae su punto
    const descripcion = corto(plano(`${encabezado}. ${p.descripcion ?? ''}`), 160);
    const imagen = api && p.imagenes?.length ? `${api}/compartir/publicaciones/${p.id}/imagen.jpg` : null;
    escribir(`publicacion/${p.id}`, pagina({ titulo: `${corto(plano(p.titulo), 70)} · ${marca}`, descripcion, url, imagen }));
    urls.push({ url, lastmod: (p.fechaEdicion ?? p.fechaPublicacion ?? hoy).slice(0, 10), frecuencia: 'weekly', prioridad: '0.8' });
  }
}

const xml = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
writeFileSync(
  join(dist, 'sitemap.xml'),
  `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls
    .map((u) => `  <url><loc>${xml(u.url)}</loc><lastmod>${u.lastmod}</lastmod><changefreq>${u.frecuencia}</changefreq><priority>${u.prioridad}</priority></url>`)
    .join('\n')}\n</urlset>\n`,
);

const ruta = new URL(front).pathname; // /TRUEKE-BOGOTA-SOLIDARIO-/ en Pages, / con dominio propio
writeFileSync(
  join(dist, 'robots.txt'),
  `User-agent: *\nAllow: ${ruta}\n${privadas.map((p) => `Disallow: ${ruta}${p}/`).join('\n')}\n\nSitemap: ${front}sitemap.xml\n`,
);

console.log(`SEO: ${estaticas.length - 1} rutas estáticas, ${publicaciones?.length ?? 0} publicaciones, ${urls.length} URLs en el sitemap.`);
