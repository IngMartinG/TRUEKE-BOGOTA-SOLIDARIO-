import { expect, test } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { cuentaLista, ingresar } from './utilidades';

test('guardar una búsqueda del catálogo, verla en Cuenta, apagar su alerta y volver a sus resultados', async ({ page }) => {
  const cuenta = await cuentaLista(page, 'Gina');
  await ingresar(page, cuenta);
  const texto = `bicicleta ${randomBytes(3).toString('hex')}`;

  await page.goto(`/explorar?texto=${encodeURIComponent(texto)}&modo=Trueke`);
  await page.getByRole('button', { name: 'Guardar búsqueda' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Búsqueda guardada' })).toBeVisible();

  await page.goto('/cuenta/busquedas');
  const fila = page.getByRole('listitem').filter({ hasText: `${texto} · Trueke` });
  await expect(fila).toBeVisible();
  const alerta = fila.getByRole('switch');
  await expect(alerta).toBeChecked();
  await alerta.uncheck({ force: true });
  await expect(page.getByRole('status').filter({ hasText: 'Alerta apagada' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('listitem').filter({ hasText: `${texto} · Trueke` }).getByRole('switch')).not.toBeChecked();

  await page.getByRole('listitem').filter({ hasText: `${texto} · Trueke` }).getByRole('link', { name: 'Ver resultados' }).click();
  await expect(page).toHaveURL(/\/explorar\?.*modo=Trueke/);
  await expect(page.getByRole('combobox', { name: 'Buscar publicaciones' })).toHaveValue(texto);
});
