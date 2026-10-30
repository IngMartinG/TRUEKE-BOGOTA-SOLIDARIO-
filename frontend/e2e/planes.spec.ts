import { expect, test } from '@playwright/test';
import { cuentaLista, ingresar } from './utilidades';

test('planes: compara Gratis, Premium y Negocio y activa la prueba gratis de Premium una sola vez', async ({ page }) => {
  await test.step('un visitante ve los tres planes y la tabla comparativa', async () => {
    await page.goto('/planes');
    await expect(page.getByRole('heading', { name: 'Planes para mover más cosas' })).toBeVisible();
    for (const plan of ['Gratis', 'Premium', 'Negocio']) await expect(page.getByRole('heading', { name: plan, exact: true })).toBeVisible();
    await expect(page.getByRole('table', { name: 'Qué incluye cada plan' })).toBeVisible();
  });

  const cuenta = await cuentaLista(page, 'Pablo');
  const auth = { Authorization: `Bearer ${cuenta.token}` };

  await test.step('con la cuenta completa activa Premium gratis, sin pagar', async () => {
    await ingresar(page, cuenta);
    await page.goto('/planes');
    await page.getByRole('button', { name: /Probar 7 días gratis/ }).click();
    await expect(page.getByText(/Estás probando Premium gratis hasta el/)).toBeVisible();
    await expect(page.getByRole('button', { name: /Probar 7 días gratis/ })).toHaveCount(0);

    const resumen = (await (await page.request.get('/api/v1/eco-puntos/resumen', { headers: auth })).json()) as {
      tipoCuenta: string; enPruebaPremium: boolean; pruebaPremiumDisponible: boolean; destacadosGratisRestantes: number;
    };
    expect(resumen).toMatchObject({ tipoCuenta: 'Premium', enPruebaPremium: true, pruebaPremiumDisponible: false, destacadosGratisRestantes: 1 });
  });

  await test.step('no se puede activar dos veces', async () => {
    const otra = await page.request.post('/api/v1/planes/prueba-premium', { headers: auth });
    expect(otra.status()).toBe(400);
  });
});
