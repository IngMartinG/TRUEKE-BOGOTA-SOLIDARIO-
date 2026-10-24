import { expect, test } from '@playwright/test';
import { cuentaLista, ingresar } from './utilidades';

/** PDF mínimo válido (la API comprueba la firma %PDF- del contenido real). */
const certificado = Buffer.from('%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n');

test('fundaciones: el visitante ve la página y una cuenta pide la verificación subiendo el certificado', async ({ page }) => {
  await test.step('Sin sesión, la página de fundaciones invita a verificar la propia', async () => {
    await page.goto('/fundaciones');
    await expect(page.getByRole('heading', { name: 'Fundaciones de la comunidad' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Verificar mi fundación' }).first()).toBeVisible();
  });

  const cuenta = await cuentaLista(page, 'Fundacion');
  await ingresar(page, cuenta);

  await test.step('Envía la solicitud con el certificado (sube directo a Storage, privado)', async () => {
    await page.goto('/cuenta/fundacion');
    await page.getByLabel('Razón social', { exact: true }).fill('Fundación Pruebas de Punta a Punta');
    await page.getByLabel('NIT', { exact: true }).fill(`9${Math.floor(10_000_000 + Math.random() * 89_999_999)}`);
    await page.getByLabel('¿Qué hacen y a quién atienden?').fill('Refuerzo escolar y almuerzo los sábados para niños del barrio.');
    await page.locator('input[type=file]').setInputFiles({ name: 'certificado.pdf', mimeType: 'application/pdf', buffer: certificado });
    await expect(page.getByText('Certificado cargado. Toca para cambiarlo.')).toBeVisible();
    await page.getByLabel(/Represento legalmente a esta entidad/).check();
    await page.getByRole('button', { name: 'Enviar solicitud' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Solicitud en revisión' })).toBeVisible();
  });

  await test.step('Mientras se revisa no tiene insignia ni página pública', async () => {
    const r = await page.request.get(`/api/v1/fundaciones/${cuenta.id}`);
    expect(r.status()).toBe(404);
  });
});
