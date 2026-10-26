import { expect, test, type Browser, type Locator, type Page } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { cuentaLista, fotoPng, ingresar, publicar, type Cuenta } from './utilidades';

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

    // Proponer un lugar seguro solo escribe la propuesta: no la envía.
    // (La conversación ya trae el mensaje de la solicitud, así que se abre con el botón del escudo.)
    const lugares = ana.page.getByRole('button', { name: 'Proponer un lugar seguro' });
    await expect(lugares).toHaveAttribute('aria-expanded', 'false');
    await lugares.click();
    await expect(lugares).toHaveAttribute('aria-expanded', 'true');
    await ana.page.getByRole('button', { name: 'Proponer CAI de la Policía' }).click();
    await expect(ana.page.getByPlaceholder('Escribe un mensaje…')).toHaveValue(/frente a un CAI de la Policía/);
    await expect(ana.page.getByPlaceholder('Escribe un mensaje…')).toBeFocused();
    await expect(lugares).toHaveAttribute('aria-expanded', 'false');
    await expect(beto.page.locator('section').getByText(/CAI de la Policía/)).toHaveCount(0);

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
    await beto.page.getByRole('checkbox', { name: `Ofrecer ${ofrecida}` }).check({ force: true });
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

test('contraoferta en un trueke: el dueño pide otros objetos y quien propuso la acepta', async ({ browser, contextOptions, viewport, userAgent, isMobile, hasTouch }) => {
  const dispositivo = { ...contextOptions, viewport, userAgent, isMobile, hasTouch };
  const sufijo = randomBytes(3).toString('hex');
  const pedida = `Teclado E2E ${sufijo}`;
  const patineta = `Patineta E2E ${sufijo}`;
  const casco = `Casco E2E ${sufijo}`;
  const ana = await persona(browser, 'Ana', dispositivo);
  const beto = await persona(browser, 'Beto', dispositivo);
  const idPedida = await publicar(ana.page.request, ana.cuenta, pedida);
  const idPatineta = await publicar(beto.page.request, beto.cuenta, patineta);
  const idCasco = await publicar(beto.page.request, beto.cuenta, casco);
  const estado = async (quien: { page: Page; cuenta: Cuenta }, id: string) => {
    const r = await quien.page.request.get('/api/v1/publicaciones/mias', { headers: { Authorization: `Bearer ${quien.cuenta.token}` } });
    return ((await r.json()) as { id: string; estado: string }[]).find((p) => p.id === id)?.estado;
  };

  await test.step('Beto ofrece solo la patineta', async () => {
    await beto.page.goto(`/publicacion/${idPedida}`);
    await beto.page.getByRole('button', { name: 'Proponer trueke' }).click();
    await beto.page.getByLabel('Tu mensaje').fill('¿Te sirve mi patineta?');
    await beto.page.getByRole('checkbox', { name: `Ofrecer ${patineta}` }).check({ force: true });
    await beto.page.getByRole('button', { name: 'Enviar solicitud' }).click();
    await expect(beto.page.locator('app-tarjeta-solicitud', { hasText: pedida }).getByText('Esperando la respuesta de Ana')).toBeVisible();
  });

  await test.step('Ana contraoferta pidiendo la patineta y el casco', async () => {
    await ana.page.goto('/intercambios');
    const tarjeta = ana.page.locator('app-tarjeta-solicitud', { hasText: pedida });
    await tarjeta.getByRole('button', { name: 'Contraofertar' }).click();
    const dialogo = ana.page.getByRole('dialog');
    await dialogo.getByRole('checkbox', { name: `Pedir ${casco}` }).check({ force: true });
    await dialogo.getByRole('button', { name: 'Enviar contraoferta' }).click();
    await expect(ana.page.getByRole('status').filter({ hasText: 'Contraoferta enviada' })).toBeVisible();
    await expect(tarjeta.getByText(/Esperando la respuesta de Beto.* \(contraoferta 1 de 5\)/)).toBeVisible();
  });

  await test.step('Beto acepta la contraoferta: lo pedido y sus dos objetos quedan reservados', async () => {
    await beto.page.goto('/intercambios?tab=enviadas');
    const tarjeta = beto.page.locator('app-tarjeta-solicitud', { hasText: pedida });
    await expect(tarjeta.getByRole('link', { name: `Ofreces: ${casco}` })).toBeVisible();
    await tarjeta.getByRole('button', { name: 'Aceptar contraoferta' }).click();
    await beto.page.getByRole('dialog').getByRole('button', { name: 'Aceptar contraoferta' }).click();
    await expect(tarjeta.getByRole('button', { name: 'Confirmar entrega' })).toBeVisible();
    await expect.poll(() => estado(beto, idPatineta)).toBe('EnNegociacion');
    await expect.poll(() => estado(beto, idCasco)).toBe('EnNegociacion');
    await expect.poll(() => estado(ana, idPedida)).toBe('EnNegociacion');
  });
});

/**
 * Toca la tarjeta visible de una opción (los radios son sr-only). `dentro` es la página o un diálogo. El radio de `has` se
 * busca DENTRO del label, así que debe partir de la página: si partiera del diálogo, buscaría un diálogo dentro del label.
 */
async function elegir(dentro: Page | Locator, nombre: string | RegExp): Promise<void> {
  const page = 'page' in dentro ? dentro.page() : dentro;
  const opcion = page.getByRole('radio', { name: nombre });
  await dentro.locator('label', { has: opcion }).click();
  await expect(dentro.getByRole('radio', { name: nombre })).toBeChecked();
}

test('venta que acepta trueke: se publica con la casilla, se negocia entre precio y objetos y se cierra como trueke', async ({ browser, contextOptions, viewport, userAgent, isMobile, hasTouch }) => {
  const dispositivo = { ...contextOptions, viewport, userAgent, isMobile, hasTouch };
  const sufijo = randomBytes(3).toString('hex');
  const venta = `Guitarra E2E ${sufijo}`;
  const patineta = `Patineta E2E ${sufijo}`;
  const ana = await persona(browser, 'Ana', dispositivo);
  const beto = await persona(browser, 'Beto', dispositivo);
  const registro = await ana.page.request.put('/api/v1/usuarios/yo/oferente', {
    headers: { Authorization: `Bearer ${ana.cuenta.token}` },
    data: { tipoDocumento: 'CC', documento: '1012345678', nombre: 'Ana Pruebas', direccion: 'Calle 45 # 12-30', municipioCodigo: '11001', telefono: '3001234567' },
  });
  expect(registro.ok(), await registro.text()).toBe(true);
  const idPatineta = await publicar(beto.page.request, beto.cuenta, patineta);
  let idVenta = '';

  await test.step('Ana publica la venta marcando "También acepto trueke"', async () => {
    await ana.page.goto('/publicar');
    await elegir(ana.page, /^Compra y venta\b/);
    await ana.page.getByRole('checkbox', { name: 'También acepto trueke' }).check();
    await ana.page.getByRole('button', { name: 'Continuar' }).click();
    await ana.page.getByLabel('Título').fill(venta);
    await elegir(ana.page, 'Hogar y muebles');
    await elegir(ana.page, 'Como nuevo');
    await ana.page.getByLabel('Descripción').fill('Guitarra acústica con estuche.');
    await ana.page.getByLabel('Precio de venta').fill('300000');
    await ana.page.getByRole('button', { name: 'Continuar' }).click();
    await ana.page.locator('input[type=file]').setInputFiles({ name: 'guitarra.png', mimeType: 'image/png', buffer: fotoPng(800, 600) });
    await expect(ana.page.getByText('Portada', { exact: true })).toBeVisible();
    await ana.page.getByRole('button', { name: 'Continuar' }).click();
    await ana.page.getByLabel('Localidad').selectOption('Chapinero');
    await ana.page.getByRole('button', { name: 'Continuar' }).click();
    await expect(ana.page.getByText('También lo aceptas')).toBeVisible();
    await ana.page.getByRole('button', { name: 'Publicar ahora' }).click();
    await expect(ana.page).toHaveURL(/\/publicacion\/[0-9a-f-]{36}/);
    idVenta = ana.page.url().split('/publicacion/')[1].split(/[?#]/)[0];
    await expect(ana.page.getByText('Acepta trueke').first()).toBeVisible();
  });

  await test.step('Beto elige proponer un trueke con su patineta', async () => {
    await beto.page.goto(`/publicacion/${idVenta}`);
    await beto.page.getByRole('button', { name: 'Quiero comprarlo' }).click();
    const dialogo = beto.page.getByRole('dialog');
    await elegir(dialogo, 'Te propongo un trueke');
    await expect(dialogo.getByRole('button', { name: 'Enviar solicitud' })).toBeDisabled(); // sin objetos no hay trueke
    await dialogo.getByRole('checkbox', { name: `Ofrecer ${patineta}` }).check({ force: true });
    await dialogo.getByLabel('Tu mensaje').fill('¿Me la cambias por mi patineta?');
    await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
    const tarjeta = beto.page.locator('app-tarjeta-solicitud', { hasText: venta });
    await expect(tarjeta.getByText(/venta en .*300\.000 que también acepta trueke/)).toBeVisible();
  });

  await test.step('Ana prefiere que le paguen y contraoferta con un precio', async () => {
    await ana.page.goto('/intercambios');
    const tarjeta = ana.page.locator('app-tarjeta-solicitud', { hasText: venta });
    await tarjeta.getByRole('button', { name: 'Contraofertar' }).click();
    const dialogo = ana.page.getByRole('dialog');
    await elegir(dialogo, 'Un precio');
    await dialogo.getByLabel('Precio que propones').fill('280000');
    await dialogo.getByRole('button', { name: 'Enviar contraoferta' }).click();
    await expect(tarjeta.getByText(/Esperando la respuesta de Beto/)).toBeVisible();
  });

  await test.step('Beto vuelve al trueke y Ana lo acepta: la patineta queda reservada', async () => {
    await beto.page.goto('/intercambios?tab=enviadas');
    const tarjeta = beto.page.locator('app-tarjeta-solicitud', { hasText: venta });
    await expect(tarjeta.getByText(/280\.000/)).toBeVisible();
    await tarjeta.getByRole('button', { name: 'Contraofertar' }).click();
    const dialogo = beto.page.getByRole('dialog');
    await elegir(dialogo, 'Objetos a cambio');
    await dialogo.getByRole('checkbox', { name: `Ofrecer ${patineta}` }).check({ force: true });
    await dialogo.getByRole('button', { name: 'Enviar contraoferta' }).click();
    await expect(tarjeta.getByText(/Esperando la respuesta de Ana/)).toBeVisible();

    await ana.page.goto('/intercambios');
    const deAna = ana.page.locator('app-tarjeta-solicitud', { hasText: venta });
    await expect(deAna.getByRole('link', { name: `Te ofrece: ${patineta}` })).toBeVisible();
    await deAna.getByRole('button', { name: 'Aceptar contraoferta' }).click();
    await ana.page.getByRole('dialog').getByRole('button', { name: 'Aceptar contraoferta' }).click();
    await expect(deAna.getByRole('button', { name: 'Confirmar entrega' })).toBeVisible();
    const r = await beto.page.request.get('/api/v1/solicitudes/enviadas', { headers: { Authorization: `Bearer ${beto.cuenta.token}` } });
    const s = ((await r.json()) as { publicacionId: string; modo: string }[]).find((x) => x.publicacionId === idVenta);
    expect(s?.modo).toBe('Trueke');
    const mias = await beto.page.request.get('/api/v1/publicaciones/mias', { headers: { Authorization: `Bearer ${beto.cuenta.token}` } });
    expect(((await mias.json()) as { id: string; estado: string }[]).find((p) => p.id === idPatineta)?.estado).toBe('EnNegociacion');
  });
});
