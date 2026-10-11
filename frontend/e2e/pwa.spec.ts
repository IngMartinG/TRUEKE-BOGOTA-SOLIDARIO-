import { expect, test } from '@playwright/test';
import { cuentaLista, ingresar } from './utilidades';

test.describe('app instalable (PWA)', () => {
  test('tiene manifiesto, service worker y funciona sin conexión una vez cargada', async ({ page, context, request }) => {
    const manifiesto = await request.get('/manifest.webmanifest');
    expect(manifiesto.ok()).toBe(true);
    const m = (await manifiesto.json()) as { display: string; icons: { sizes: string }[] };
    expect(m.display).toBe('standalone');
    expect(m.icons.map((i) => i.sizes)).toEqual(expect.arrayContaining(['192x192', '512x512']));

    // El service worker se registra cuando la app ya cargó (máximo 30 s) y toma control de la página.
    await page.goto('/');
    await page.evaluate(() => navigator.serviceWorker.ready);
    await page.reload();
    await expect.poll(() => page.evaluate(() => !!navigator.serviceWorker.controller), { timeout: 40_000 }).toBe(true);

    // Espera a que el service worker guarde lo básico (grupo "base"); lo demás se guarda a medida que se usa.
    await expect
      .poll(
        () =>
          page.evaluate(async () => {
            const manifiesto = (await (await fetch('ngsw.json', { cache: 'no-store' })).json()) as { assetGroups: { name: string; urls: string[] }[] };
            const urls = manifiesto.assetGroups.find((g) => g.name === 'base')?.urls ?? [];
            const nombre = (await caches.keys()).find((k) => k.endsWith(':assets:base:cache'));
            if (!nombre) return false;
            const cache = await caches.open(nombre);
            for (const u of urls) if (!(await cache.match(u))) return false;
            return true;
          }),
        { timeout: 60_000 },
      )
      .toBe(true);

    // Sin conexión, la portada (ya visitada) sigue abriendo: la app quedó guardada; los datos se piden en vivo.
    await context.setOffline(true);
    await page.goto('/');
    await expect(page.getByRole('link', { name: 'Trueke Bogotá Solidario, inicio' })).toBeVisible();
    await context.setOffline(false);
  });

  test('ofrece instalarla y activar las notificaciones en este dispositivo', async ({ page }) => {
    const cuenta = await cuentaLista(page, 'Elena');
    await ingresar(page, cuenta);
    await page.goto('/cuenta/avisos');
    await expect(page.getByRole('heading', { name: 'En este dispositivo' })).toBeVisible();
    await expect(page.getByText('Trueke como app')).toBeVisible();
    // Activarlas de verdad exige el servicio de push de Google (fuera del ambiente): se prueba que se ofrecen.
    await expect(page.getByRole('switch', { name: 'Notificaciones en este dispositivo' })).not.toBeChecked();
  });
});
