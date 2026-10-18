import { expect, test } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { URL_API } from './ambiente';
import { correoUnico, cuentaLista, esperarCorreo, publicar } from './utilidades';

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

  test('el impacto de la comunidad está a un toque desde el encabezado o el pie', async ({ page, isMobile }) => {
    await page.goto('/');
    // En PC está en la navegación principal; en celular (sin espacio en la barra) en el pie y en el menú de cuenta.
    const enlace = isMobile
      ? page.getByRole('contentinfo').getByRole('link', { name: 'Impacto de la comunidad' })
      : page.getByRole('navigation', { name: 'Principal' }).getByRole('link', { name: 'Impacto' });
    await enlace.click();
    await expect(page).toHaveURL(/\/impacto$/);
    await expect(page.getByRole('heading', { name: 'Lo que hemos hecho circular' })).toBeVisible();
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
  test('radica una PQRS sin cuenta, recibe el radicado por correo y consulta el estado', async ({ page }) => {
    const correo = correoUnico('pqrs');
    await page.goto('/');
    await page.getByRole('contentinfo').getByRole('link', { name: /PQRS/ }).click();
    await expect(page.getByRole('heading', { name: 'PQRS', exact: true })).toBeVisible();
    await page.getByText('Consulta de mis datos').click();
    await page.getByLabel('Nombre y apellido').fill('Elena Prueba Ríos');
    await page.getByLabel('Correo para la respuesta').fill(correo);
    await page.getByLabel('Asunto').fill('Qué datos míos tienen');
    await page.getByLabel('Cuéntanos qué pasó').fill('Quiero saber qué datos personales guardan sobre mí.');
    await page.getByRole('button', { name: 'Radicar solicitud' }).click();
    const radicado = (await page.locator('[role="status"] strong.font-mono').textContent())!.trim();
    expect(radicado).toMatch(/^PQR-\d{8}-[0-9A-F]{6}$/);

    const texto = await esperarCorreo(page.request, correo, /Recibimos tu solicitud/);
    expect(texto).toContain(radicado);
    expect(texto).toContain('10 días hábiles');

    await page.getByRole('tab', { name: 'Consultar el estado' }).click();
    await page.getByLabel('Radicado').fill(radicado);
    await page.getByLabel('Correo con el que la radicaste').fill(correo);
    await page.getByRole('button', { name: 'Consultar' }).click();
    await expect(page.getByText('Qué datos míos tienen')).toBeVisible();
    await expect(page.getByText('Recibida')).toBeVisible();
  });
});
