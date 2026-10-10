import { defineConfig, devices } from '@playwright/test';
import { URL_FRONT } from './e2e/ambiente';

/**
 * Pruebas de punta a punta contra las imágenes Docker de producción (ver e2e/docker-compose.e2e.yml).
 *   npm run e2e                     levanta el ambiente, prueba y lo baja
 *   E2E_CONSERVAR=1 npm run e2e     lo deja arriba para revisar (http://localhost:8081, Mailpit en :8025)
 *   E2E_URL=http://localhost:8081   usa un ambiente ya levantado
 */
export default defineConfig({
  testDir: './e2e',
  outputDir: './e2e/.resultados',
  globalSetup: './e2e/preparacion.ts',
  globalTeardown: './e2e/cierre.ts',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  workers: process.env['CI'] ? 2 : 3,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never', outputFolder: 'e2e/.reporte' }]] : [['list'], ['html', { open: 'never', outputFolder: 'e2e/.reporte' }]],
  use: {
    baseURL: URL_FRONT,
    locale: 'es-CO',
    timezoneId: 'America/Bogota',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'pc', use: { ...devices['Desktop Chrome'] } },
    { name: 'celular', use: { ...devices['Pixel 7'] } },
  ],
});
