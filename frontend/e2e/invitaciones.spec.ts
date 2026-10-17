import { expect, test } from '@playwright/test';
import { cuentaLista, ingresar } from './utilidades';

test('invita y ganan los dos: el enlace personal lleva al registro con el aviso de invitación', async ({ page, browser }) => {
  const cuenta = await cuentaLista(page, 'Ines');
  await ingresar(page, cuenta);

  await page.goto('/eco-puntos');
  await expect(page.getByRole('heading', { name: 'Invita y ganan los dos' })).toBeVisible();
  const enlace = await page.getByLabel('Tu enlace de invitación').inputValue();
  expect(enlace).toContain('/registro?invita=');

  const visitante = await browser.newContext();
  try {
    const otra = await visitante.newPage();
    await otra.goto(enlace);
    await expect(otra.getByText('Te invitaron', { exact: false })).toBeVisible();
  } finally {
    await visitante.close();
  }
});
