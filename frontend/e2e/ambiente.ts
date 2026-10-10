import { execFileSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { resolve } from 'node:path';

/** Front de producción (nginx) del ambiente E2E; sirve también /api y /hubs en el mismo origen. */
export const URL_FRONT = process.env['E2E_URL'] ?? 'http://localhost:8081';
/** La API publicada directamente (para lo que en producción sirve su propio dominio, como /compartir). */
export const URL_API = process.env['E2E_API'] ?? 'http://localhost:8080';
/** API HTTP de Mailpit (buzón de prueba). */
export const URL_MAILPIT = process.env['E2E_MAILPIT'] ?? 'http://localhost:8025';

const aqui = __dirname; // Playwright transpila estos archivos como CommonJS
const backend = resolve(aqui, '../../backend');
const PROYECTO = 'trueke-e2e';

function compose(...args: string[]): void {
  execFileSync(
    'docker',
    ['compose', '-p', PROYECTO, '--project-directory', backend, '-f', resolve(backend, 'docker-compose.yml'), '-f', resolve(aqui, 'docker-compose.e2e.yml'), ...args],
    { stdio: 'inherit', env: { ...process.env, ...secretos() } },
  );
}

/** Secretos de un solo uso: cada ambiente E2E tiene los suyos y nunca se guardan. */
let generados: Record<string, string> | null = null;
function secretos(): Record<string, string> {
  generados ??= {
    SQL_SA_PASSWORD: `E2e!${randomBytes(12).toString('hex')}Aa1`,
    JWT_KEY: randomBytes(48).toString('base64'),
    CLAVE_CIFRADO: randomBytes(32).toString('base64'),
    REDIS_PASSWORD: randomBytes(16).toString('hex'),
    ASPNETCORE_ENVIRONMENT: 'Staging',
  };
  return generados;
}

async function esperarListo(url: string, minutos: number): Promise<void> {
  const limite = Date.now() + minutos * 60_000;
  let ultimo = '';
  while (Date.now() < limite) {
    try {
      const r = await fetch(url);
      if (r.ok) return;
      ultimo = `HTTP ${r.status}`;
    } catch (e) {
      ultimo = String(e);
    }
    await new Promise((ok) => setTimeout(ok, 3000));
  }
  throw new Error(`El ambiente E2E no respondió en ${minutos} min (${url}): ${ultimo}`);
}

/** Construye las imágenes de producción y levanta el ambiente (salvo E2E_URL: usar uno ya levantado). */
export async function levantar(): Promise<void> {
  if (process.env['E2E_URL']) return;
  compose('down', '-v', '--remove-orphans'); // siempre desde cero: base, buzón y fotos vacíos
  compose('up', '-d', '--build');
  // A través de nginx → API → SQL Server (con las migraciones aplicadas): si responde, todo está arriba.
  await esperarListo(`${URL_FRONT}/api/v1/categorias`, 6);
  await esperarListo(`${URL_MAILPIT}/api/v1/info`, 1);
}

export function bajar(): void {
  if (process.env['E2E_URL'] || process.env['E2E_CONSERVAR']) return;
  compose('down', '-v', '--remove-orphans');
}

export function registrosApi(): void {
  compose('logs', '--no-color', '--tail', '300', 'api', 'web', 'mailpit');
}
