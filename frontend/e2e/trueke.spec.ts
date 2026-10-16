import { expect, test, type Browser, type Page } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { cuentaLista, ingresar, publicar, type Cuenta } from './utilidades';

/** Cada persona en su propio navegador (cookies y sesión separadas), con el mismo dispositivo del proyecto. */
async function persona(browser: Browser, nombre: string, opciones: Parameters<Browser['newContext']>[0]): Promise<{ page: Page; cuenta: Cuenta }> {
  const contexto = await browser.newContext(opciones);
  const page = await contexto.newPage();
  const cuenta = await cuentaLista(page, nombre);
  await ingresar(page, cuenta);
  return { page, cuenta };
}

async function saldo(page: Page, cuenta: Cuenta): Promise<number> {
  const r = await page.request.get('/api/v1/usuarios/yo', { headers: { Authorization: `Bearer ${cuenta.token}` } });
  return ((await r.json()) as { saldoEcoPuntos: number }).saldoEcoPuntos;
}

test('trueke completo: solicitar, aceptar, chatear en tiempo real, confirmar la entrega y ganar Eco-Puntos', async ({ browser, contextOptions, viewport, userAgent, isMobile, hasTouch }) => {
  const dispositivo = { ...contextOptions, viewport, userAgent, isMobile, hasTouch };
  const titulo = `Bicicleta E2E ${randomBytes(3).toString('hex')}`;
  const ana = await persona(browser, 'Ana', dispositivo);   // publica
  const beto = await persona(browser, 'Beto', dispositivo); // propone el trueke
  const publicacion = await publicar(ana.page.request, ana.cuenta, titulo);
  const saldoInicialBeto = await saldo(beto.page, beto.cuenta);

  await test.step('Beto propone el trueke desde la publicación', async () => {
    await beto.page.goto(`/publicacion/${publicacion}`);
    await beto.page.getByRole('button', { name: 'Proponer trueke' }).click();
    await beto.page.getByLabel('Tu mensaje').fill('Te la cambio por un casco casi nuevo.');
    await beto.page.getByRole('button', { name: 'Enviar solicitud' }).click();
    // Al enviarla, la app lleva a "Mis intercambios", donde aparece la solicitud.
    await expect(beto.page).toHaveURL(/\/intercambios/);
    await expect(beto.page.locator('app-tarjeta-solicitud', { hasText: titulo })).toBeVisible();
  });

  await test.step('Ana la acepta y la publicación queda reservada', async () => {
    await ana.page.goto('/intercambios');
    const tarjeta = ana.page.locator('app-tarjeta-solicitud', { hasText: titulo });
    await tarjeta.getByRole('button', { name: 'Aceptar y reservar' }).click();
    // Pide confirmación antes de reservar (afecta a quienes quedan en lista de espera)
    await ana.page.getByRole('dialog').getByRole('button', { name: 'Aceptar y reservar' }).click();
    await expect(tarjeta.getByRole('link', { name: 'Abrir chat' })).toBeVisible();
    await tarjeta.getByRole('link', { name: 'Abrir chat' }).click();
    await expect(ana.page).toHaveURL(/\/mensajes\/.+/);
  });

  await test.step('el chat llega en tiempo real a la otra persona (SignalR, sin recargar)', async () => {
    await beto.page.goto('/intercambios?tab=enviadas');
    await beto.page.locator('app-tarjeta-solicitud', { hasText: titulo }).getByRole('link', { name: 'Abrir chat' }).click();
    await expect(beto.page).toHaveURL(/\/mensajes\/.+/);

    // Proponer un lugar seguro solo escribe la propuesta: no la envía
    await ana.page.getByRole('button', { name: 'Proponer CAI de la Policía' }).click();
    await expect(ana.page.getByPlaceholder('Escribe un mensaje…')).toHaveValue(/frente a un CAI de la Policía/);
    await expect(ana.page.getByPlaceholder('Escribe un mensaje…')).toBeFocused();
    await ana.page.getByRole('button', { name: 'Proponer un lugar seguro' }).click();
    await expect(ana.page.getByRole('button', { name: 'Proponer Biblioteca pública' })).toBeVisible();
    await ana.page.getByRole('button', { name: 'Proponer un lugar seguro' }).click();

    await ana.page.getByPlaceholder('Escribe un mensaje…').fill('¿Nos vemos el sábado en la biblioteca pública?');
    await ana.page.getByRole('button', { name: 'Enviar', exact: true }).click();
    await expect(beto.page.locator('section').getByText('¿Nos vemos el sábado en la biblioteca pública?')).toBeVisible();

    await beto.page.getByPlaceholder('Escribe un mensaje…').fill('¡Listo! A las 10.');
    await beto.page.getByRole('button', { name: 'Enviar', exact: true }).click();
    await expect(ana.page.locator('section').getByText('¡Listo! A las 10.')).toBeVisible();
  });

  await test.step('ambos confirman la entrega y se otorgan los Eco-Puntos', async () => {
    await ana.page.goto('/intercambios');
    await ana.page.locator('app-tarjeta-solicitud', { hasText: titulo }).getByRole('button', { name: 'Confirmar entrega' }).click();
    await ana.page.getByRole('dialog').getByRole('button', { name: 'Sí, confirmar entrega' }).click();
    await expect(ana.page.getByRole('status').filter({ hasText: 'Entrega confirmada' })).toBeVisible();

    await beto.page.goto('/intercambios?tab=enviadas');
    await beto.page.locator('app-tarjeta-solicitud', { hasText: titulo }).getByRole('button', { name: 'Confirmar entrega' }).click();
    await beto.page.getByRole('dialog').getByRole('button', { name: 'Sí, confirmar entrega' }).click();
    await expect(beto.page.locator('app-tarjeta-solicitud', { hasText: titulo }).getByRole('button', { name: 'Calificar' })).toBeVisible();

    // Trueke = 10 Eco-Puntos para cada persona (PoliticaEcoPuntos), solo al completarse.
    await expect.poll(() => saldo(beto.page, beto.cuenta)).toBe(saldoInicialBeto + 10);
  });

  await test.step('la publicación ya no se puede solicitar', async () => {
    await beto.page.goto(`/publicacion/${publicacion}`);
    await expect(beto.page.getByText('¡Este objeto ya encontró un nuevo hogar!')).toBeVisible();
  });
});

test('trueke con objeto: ofrecer uno propio, verlo, aceptarlo y que ambos objetos cambien de manos', async ({ browser, contextOptions, viewport, userAgent, isMobile, hasTouch }) => {
  const dispositivo = { ...contextOptions, viewport, userAgent, isMobile, hasTouch };
  const sufijo = randomBytes(3).toString('hex');
  const pedida = `Guitarra E2E ${sufijo}`;
  const ofrecida = `Patineta E2E ${sufijo}`;
  const ana = await persona(browser, 'Ana', dispositivo);
  const beto = await persona(browser, 'Beto', dispositivo);
  const idPedida = await publicar(ana.page.request, ana.cuenta, pedida);
  const idOfrecida = await publicar(beto.page.request, beto.cuenta, ofrecida);
  const estado = async (quien: { page: Page; cuenta: Cuenta }, id: string) => {
    const r = await quien.page.request.get('/api/v1/publicaciones/mias', { headers: { Authorization: `Bearer ${quien.cuenta.token}` } });
    return ((await r.json()) as { id: string; estado: string }[]).find((p) => p.id === id)?.estado;
  };

  await test.step('Beto propone el trueke ofreciendo su patineta', async () => {
    await beto.page.goto(`/publicacion/${idPedida}`);
    await beto.page.getByRole('button', { name: 'Proponer trueke' }).click();
    await beto.page.getByLabel('Tu mensaje').fill('Te la cambio por mi patineta.');
    await beto.page.getByRole('radio', { name: `Ofrecer ${ofrecida}` }).check({ force: true });
    await beto.page.getByRole('button', { name: 'Enviar solicitud' }).click();
    await expect(beto.page.locator('app-tarjeta-solicitud', { hasText: pedida }).getByText('Ofreces a cambio')).toBeVisible();
  });

  await test.step('Ana ve lo que le ofrecen, lo revisa y acepta: los dos objetos quedan reservados', async () => {
    await ana.page.goto('/intercambios');
    const tarjeta = ana.page.locator('app-tarjeta-solicitud', { hasText: pedida });
    await expect(tarjeta.getByRole('link', { name: `Te ofrece: ${ofrecida}` })).toBeVisible();
    await tarjeta.getByRole('button', { name: 'Aceptar y reservar' }).click();
    // Pide confirmación antes de reservar (afecta a quienes quedan en lista de espera)
    await ana.page.getByRole('dialog').getByRole('button', { name: 'Aceptar y reservar' }).click();
    await expect(tarjeta.getByRole('link', { name: 'Abrir chat' })).toBeVisible();
    await expect.poll(() => estado(beto, idOfrecida)).toBe('EnNegociacion');
    // Aunque ya salió del catálogo, Ana puede ver lo que va a recibir
    await tarjeta.getByRole('link', { name: `Te ofrece: ${ofrecida}` }).click();
    await expect(ana.page.getByRole('heading', { name: ofrecida })).toBeVisible();
  });

  await test.step('ambos confirman y los dos objetos quedan intercambiados', async () => {
    await ana.page.goto('/intercambios');
    await ana.page.locator('app-tarjeta-solicitud', { hasText: pedida }).getByRole('button', { name: 'Confirmar entrega' }).click();
    await ana.page.getByRole('dialog').getByRole('button', { name: 'Sí, confirmar entrega' }).click();
    await expect(ana.page.getByRole('status').filter({ hasText: 'Entrega confirmada' })).toBeVisible();
    await beto.page.goto('/intercambios?tab=enviadas');
    await beto.page.locator('app-tarjeta-solicitud', { hasText: pedida }).getByRole('button', { name: 'Confirmar entrega' }).click();
    await beto.page.getByRole('dialog').getByRole('button', { name: 'Sí, confirmar entrega' }).click();
    await expect.poll(() => estado(beto, idOfrecida)).toBe('Intercambiada');
    await expect.poll(() => estado(ana, idPedida)).toBe('Intercambiada');
  });
});
