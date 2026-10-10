import { expect, test } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { URL_API } from './ambiente';
import { cuentaLista, publicar } from './utilidades';

test.describe('visitante sin cuenta', () => {
  let titulo = '';
  let id = '';

  test.beforeAll(async ({ browser }) => {
    // Alguien publicó antes (por la API, con foto real en el almacenamiento).
    const page = await (await browser.newContext()).newPage();
    titulo = `Cámara fotográfica E2E ${randomBytes(3).toString('hex')}`;
    id = await publicar(page.request, await cuentaLista(page, 'Diana'), titulo);
    await page.context().close();
  });

  test('la portada carga y lleva al catálogo', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/Trueke Bogotá Solidario/);
    await page.getByRole('link', { name: 'Explorar' }).first().click();
    await expect(page.getByRole('heading', { name: 'Explora el catálogo' })).toBeVisible();
  });

  test('busca sin tildes, elige una sugerencia y ve el detalle', async ({ page }) => {
    await page.goto('/explorar');
    const buscador = page.getByRole('combobox', { name: 'Buscar publicaciones' });
    await buscador.fill(`camaras ${titulo.split(' ').at(-1)}`); // plural y sin tilde
    await expect(page.getByRole('option', { name: titulo })).toBeVisible();
    await buscador.press('ArrowDown');
    await buscador.press('Enter');
    await expect(page).toHaveURL(/texto=/);

    await page.locator('app-tarjeta-publicacion', { hasText: titulo }).getByRole('link').first().click();
    await expect(page.getByRole('heading', { name: titulo })).toBeVisible();
  });

  test('para proponer un trueke le pide ingresar y vuelve a la publicación', async ({ page }) => {
    await page.goto(`/publicacion/${id}`);
    await page.getByRole('button', { name: 'Proponer trueke' }).click();
    await expect(page).toHaveURL(/\/ingresar\?volver=/);
    await expect(page.getByRole('heading', { name: 'Hola de nuevo' })).toBeVisible();
  });

  test('el enlace para compartir trae la vista previa de WhatsApp con la foto', async ({ request }) => {
    // Como en producción, la vista previa la sirve la API en su propio dominio.
    const pagina = await request.get(`${URL_API}/compartir/publicaciones/${id}`);
    expect(pagina.status()).toBe(200);
    const html = await pagina.text();
    expect(html).toContain(`<meta property="og:title" content="${titulo}">`);
    const imagen = html.match(/<meta property="og:image" content="([^"]+)"/)?.[1];
    expect(imagen).toMatch(/^https?:\/\/.+\/imagen\.jpg$/);

    const jpeg = await request.get(`${URL_API}/compartir/publicaciones/${id}/imagen.jpg`);
    expect(jpeg.status()).toBe(200);
    expect(jpeg.headers()['content-type']).toBe('image/jpeg');
  });
});
