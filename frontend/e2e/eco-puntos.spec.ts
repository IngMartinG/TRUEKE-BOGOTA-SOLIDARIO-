import { expect, test } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { cuentaLista, publicar } from './utilidades';

test('destacar pagando todo con Eco-Puntos: la persona elige usarlos y se aplica sin pasarela', async ({ page }) => {
  const titulo = `Lámpara E2E ${randomBytes(3).toString('hex')}`;
  const cuenta = await cuentaLista(page, 'Elena');
  const auth = { Authorization: `Bearer ${cuenta.token}` };
  const id = await publicar(page.request, cuenta, titulo);

  await test.step('recarga 100 Eco-Puntos (pasarela simulada del ambiente de pruebas)', async () => {
    const r = await page.request.post('/api/v1/pagos/iniciar', {
      headers: auth,
      data: { concepto: 'Recarga', montoRecargaCop: 10000, facturaANombre: false, aceptoConsumidorFinal: true },
    });
    expect(r.status(), await r.text()).toBe(201);
    const { referencia } = (await r.json()) as { referencia: string };
    expect((await page.request.post(`/api/v1/pagos/${referencia}/simular?aprobado=true`, { headers: auth })).ok()).toBe(true);
  });

  await test.step('en el resumen nada se canjea hasta que elige pagar con puntos', async () => {
    await page.goto('/eco-puntos');
    await page.getByRole('button', { name: 'Destacar una publicación' }).click();
    const dialogo = page.getByRole('dialog');
    await dialogo.getByLabel('¿Qué publicación quieres destacar?').selectOption({ label: titulo });

    await dialogo.getByRole('button', { name: /Pagar todo con 60 Eco-Puntos/ }).click();
    await expect(dialogo.getByText(/sin pasarela ni factura/)).toBeVisible();
    await dialogo.getByRole('button', { name: 'Usar 60 Eco-Puntos' }).click();
    await expect(dialogo).toBeHidden();
  });

  await test.step('la publicación queda destacada y el saldo baja exactamente 60', async () => {
    const resumen = await page.request.get('/api/v1/eco-puntos/resumen', { headers: auth });
    const { saldo } = (await resumen.json()) as { saldo: number };
    expect(saldo).toBe(110 - 60); // 10 de bienvenida + 100 de la recarga
    const pub = await page.request.get(`/api/v1/publicaciones/${id}`, { headers: auth });
    expect(((await pub.json()) as { destacada: boolean }).destacada).toBe(true);
  });
});
