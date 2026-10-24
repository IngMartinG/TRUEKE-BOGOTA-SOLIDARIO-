import { expect, test, type Page } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { cuentaLista, fotoPng, ingresar } from './utilidades';

/**
 * Elige una opción tocando su tarjeta visible: los radios son sr-only y en celular un clic forzado sobre el input
 * puede caer fuera de la opción.
 */
async function elegir(page: Page, nombre: string | RegExp): Promise<void> {
  const radio = page.getByRole('radio', { name: nombre });
  await page.locator('label', { has: radio }).click();
  await expect(radio).toBeChecked();
}

test('vender por primera vez: la publicación en Compra y venta pide el registro de vendedor y luego se publica', async ({ page }) => {
  const titulo = `Lámpara de escritorio E2E ${randomBytes(3).toString('hex')}`;
  const cuenta = await cuentaLista(page, 'Valeria');
  await ingresar(page, cuenta);

  await test.step('elige Compra y venta y ve el aviso del registro', async () => {
    await page.goto('/publicar');
    await elegir(page, /^Compra y venta\b/);
    await expect(page.getByText('Para vender, la ley pide registrar una sola vez')).toBeVisible();
    await page.getByRole('button', { name: 'Continuar' }).click();

    await page.getByLabel('Título').fill(titulo);
    await elegir(page, 'Hogar y muebles');
    await elegir(page, 'Como nuevo');
    await page.getByLabel('Descripción').fill('Lámpara LED con brazo flexible, funciona perfecto.');
    await page.getByLabel('Precio de venta').fill('45000');
    await page.getByRole('button', { name: 'Continuar' }).click();

    await expect(page.getByRole('heading', { name: 'Agrega fotos' })).toBeVisible();
    await page.locator('input[type=file]').setInputFiles({ name: 'lampara.png', mimeType: 'image/png', buffer: fotoPng(800, 600) });
    await expect(page.getByText('Portada', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Continuar' }).click();

    await page.getByLabel('Localidad').selectOption('Chapinero');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await page.getByRole('button', { name: 'Publicar ahora' }).click();
  });

  await test.step('la API pide el registro: llega al formulario con ?volver=', async () => {
    await expect(page).toHaveURL(/\/cuenta\/vendedor\?volver=%2Fpublicar/);
    await expect(page.getByRole('heading', { name: 'Registro de vendedor' })).toBeVisible();
    // Un teléfono que no es de Colombia: el foco va al error
    await page.getByLabel('Número de documento').fill('1012345678');
    await page.getByLabel('Dirección de notificaciones').fill('Calle 45 # 12-30');
    await page.getByLabel('Teléfono').fill('12345');
    await page.getByRole('button', { name: 'Guardar y volver a la publicación' }).click();
    await expect(page.getByLabel('Teléfono')).toBeFocused();
    await expect(page.getByLabel('Teléfono')).toHaveAttribute('aria-invalid', 'true');

    await page.getByLabel('Teléfono').fill('300 123 4567');
    await page.getByRole('button', { name: 'Guardar y volver a la publicación' }).click();
    await expect(page).toHaveURL(/\/publicar$/);
  });

  await test.step('retoma el borrador y publica la venta', async () => {
    // El borrador vuelve al último paso al que había llegado: la revisión
    await page.getByRole('button', { name: 'Retomar' }).click();
    await page.getByRole('button', { name: 'Publicar ahora' }).click();
    await expect(page).toHaveURL(/\/publicacion\/[0-9a-f-]{36}/);
    await expect(page.getByRole('heading', { name: titulo })).toBeVisible();
    // Nada del registro aparece en la publicación
    await expect(page.getByText('1012345678')).toHaveCount(0);
    await expect(page.getByText('300 123 4567')).toHaveCount(0);
  });
});
