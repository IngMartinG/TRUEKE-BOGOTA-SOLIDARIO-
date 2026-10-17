import { expect, test, type Browser, type Page } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { cuentaLista, ingresar, publicar, type Cuenta } from './utilidades';

async function persona(browser: Browser, nombre: string, opciones: Parameters<Browser['newContext']>[0]): Promise<{ page: Page; cuenta: Cuenta }> {
  const contexto = await browser.newContext(opciones);
  const page = await contexto.newPage();
  const cuenta = await cuentaLista(page, nombre);
  await ingresar(page, cuenta);
  return { page, cuenta };
}

test('lo que busco: anotar un deseo, encontrar el trueke mutuo y proponerlo con lo propio ya elegido', async ({ browser, contextOptions, viewport, userAgent, isMobile, hasTouch }) => {
  const dispositivo = { ...contextOptions, viewport, userAgent, isMobile, hasTouch };
  const sufijo = randomBytes(3).toString('hex');
  const bicicleta = `Bicicleta E2E ${sufijo}`;
  const casco = `Casco E2E ${sufijo}`;
  const ana = await persona(browser, 'Ana', dispositivo);
  const beto = await persona(browser, 'Beto', dispositivo);

  // Beto ofrece una bicicleta y busca un casco (en público); Ana tiene un casco publicado
  const idBicicleta = await publicar(beto.page.request, beto.cuenta, bicicleta);
  const deseo = await beto.page.request.post('/api/v1/deseos', {
    headers: { Authorization: `Bearer ${beto.cuenta.token}` },
    data: { texto: `Casco ${sufijo}`, publico: true, avisos: true },
  });
  expect(deseo.status(), await deseo.text()).toBe(201);
  await publicar(ana.page.request, ana.cuenta, casco);

  await test.step('Ana anota lo que busca y aparece el trueke posible', async () => {
    await ana.page.goto('/lo-que-busco');
    await ana.page.getByLabel('¿Qué buscas?').fill(`Bicicleta ${sufijo}`);
    await ana.page.getByRole('button', { name: 'Agregar a mi lista' }).click();
    await expect(ana.page.getByRole('status').filter({ hasText: 'Agregado a tu lista' })).toBeVisible();
    const mutua = ana.page.getByRole('list', { name: 'Truekes posibles' }).getByRole('listitem').filter({ hasText: bicicleta });
    await expect(mutua).toContainText(casco);
    await expect(mutua).toContainText(`busca «Casco ${sufijo}»`);
    await mutua.getByRole('link', { name: 'Proponer trueke' }).click();
  });

  await test.step('La propuesta se abre con su casco ya elegido', async () => {
    await expect(ana.page).toHaveURL(new RegExp(`/publicacion/${idBicicleta}\\?ofrecer=`));
    await expect(ana.page.getByRole('radio', { name: `Ofrecer ${casco}` })).toBeChecked();
  });

  await test.step('En el perfil de Beto, Ana ve lo que busca y que ella lo tiene', async () => {
    await ana.page.goto(`/usuarios/${beto.cuenta.id}`);
    await expect(ana.page.getByRole('heading', { name: 'Lo que busca' })).toBeVisible();
    await expect(ana.page.getByRole('listitem').filter({ hasText: `Casco ${sufijo}` })).toContainText('Lo tienes');
  });
});
