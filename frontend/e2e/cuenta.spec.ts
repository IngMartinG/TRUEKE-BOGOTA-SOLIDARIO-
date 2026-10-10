import { expect, test } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { CLAVE, correoUnico, enlaceDe, esperarCorreo, fotoPng } from './utilidades';

test('cuenta nueva: registro, correo de verificación, foto obligatoria y primera publicación con foto', async ({ page }) => {
  const correo = correoUnico('carla');
  const titulo = `Mesa auxiliar E2E ${randomBytes(3).toString('hex')}`;

  await test.step('se registra', async () => {
    await page.goto('/registro');
    await page.getByLabel('Nombre completo').fill('Carla Pruebas Gómez');
    await page.getByLabel('Localidad donde vives').selectOption('Chapinero');
    await page.getByLabel('Correo electrónico').fill(correo);
    await page.getByLabel('Contraseña', { exact: true }).fill(CLAVE);
    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Crear mi cuenta' }).click();
    await expect(page.getByRole('heading', { name: 'Confirma tu correo' })).toBeVisible();
  });

  await test.step('confirma el correo con el enlace que le llegó (SMTP real con TLS)', async () => {
    const texto = await esperarCorreo(page.request, correo, /confirma|verifica/i);
    await page.goto(enlaceDe(texto, 'verificar-correo'));
    await expect(page.getByRole('heading', { name: '¡Correo confirmado!' })).toBeVisible();
  });

  await test.step('sin foto de perfil, publicar la lleva a subirla', async () => {
    await page.goto('/publicar');
    await expect(page).toHaveURL(/\/cuenta\/perfil#foto/);
    await page.locator('input[type=file]').first().setInputFiles({ name: 'yo.png', mimeType: 'image/png', buffer: fotoPng(500, 500, [200, 120, 60]) });
    await expect(page.getByText('Foto actualizada')).toBeVisible();
  });

  await test.step('publica con foto en 5 pasos', async () => {
    await page.goto('/publicar');
    await page.getByRole('radio', { name: 'Trueke', exact: true }).check({ force: true });
    await page.getByRole('button', { name: 'Continuar' }).click();

    await page.getByLabel('Título').fill(titulo);
    await page.getByRole('radio', { name: 'Hogar y muebles' }).check({ force: true });
    await page.getByRole('radio', { name: 'Como nuevo' }).check({ force: true });
    await page.getByLabel('Descripción').fill('Mesa de madera, 50 cm de alto. Sin rayones.');
    await page.getByRole('button', { name: 'Continuar' }).click();

    await page.locator('input[type=file]').setInputFiles({ name: 'mesa.png', mimeType: 'image/png', buffer: fotoPng(800, 600) });
    await expect(page.getByText('Portada', { exact: true })).toBeVisible(); // "Continuar" espera a que termine la subida
    await page.getByRole('button', { name: 'Continuar' }).click();

    await page.getByLabel('Localidad').selectOption('Chapinero');
    await page.getByRole('button', { name: 'Continuar' }).click();

    await expect(page.getByRole('heading', { name: 'Así se verá tu publicación' })).toBeVisible();
    await page.getByRole('button', { name: 'Publicar ahora' }).click();
    await expect(page).toHaveURL(/\/publicacion\/[0-9a-f-]{36}/);
    await expect(page.getByRole('heading', { name: titulo })).toBeVisible();
  });

  await test.step('aparece en el catálogo con su foto (miniatura)', async () => {
    await page.goto(`/explorar?texto=${encodeURIComponent(titulo)}`);
    const tarjeta = page.locator('app-tarjeta-publicacion', { hasText: titulo });
    await expect(tarjeta).toBeVisible();
    const foto = tarjeta.locator('img').first();
    await expect(foto).toHaveAttribute('src', /\.min\.webp$/);
    await expect.poll(() => foto.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBe(true);
  });
});
