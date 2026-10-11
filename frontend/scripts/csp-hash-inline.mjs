// Imprime los hashes CSP ('sha256-…') de los <script> en línea de un index.html compilado.
// Angular agrega un <script type="importmap"> con los hashes SRI de los chunks: sin su hash en script-src,
// una CSP sin 'unsafe-inline' lo bloquea y la integridad de los chunks diferidos no se verifica.
// Uso: node scripts/csp-hash-inline.mjs dist/trueke-web/browser/index.html
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';

const ruta = process.argv[2];
if (!ruta) {
  console.error('Uso: node scripts/csp-hash-inline.mjs <index.html>');
  process.exit(1);
}
const html = readFileSync(ruta, 'utf8');
const hashes = [...html.matchAll(/<script(?![^>]*\ssrc=)[^>]*>([\s\S]*?)<\/script>/g)]
  .map((m) => `'sha256-${createHash('sha256').update(m[1], 'utf8').digest('base64')}'`);
process.stdout.write([...new Set(hashes)].join(' '));
