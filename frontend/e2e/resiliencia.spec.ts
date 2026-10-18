import { expect, test } from '@playwright/test';
import { CLAVE, cuentaLista } from './utilidades';

// Sin service worker: así la petición del fragmento llega a la red y se puede cortar (con él, sale de su caché).
test.use({ serviceWorkers: 'block' });

test('si un fragmento de la app no carga al ingresar, la sesión sigue y se llega a la página destino', async ({ page }) => {
  const cuenta = await cuentaLista(page, 'Resiliente');
  await page.goto('/ingresar');
  await page.getByLabel('Correo electrónico').fill(cuenta.correo);
  await page.getByLabel('Contraseña', { exact: true }).fill(CLAVE);

  // Un corte de red puntual justo cuando se descarga la página siguiente (pasa en celular o tras un despliegue).
  let cortado = false;
  await page.route(/\/chunk-[^/]+\.js$/, (r) => {
    if (cortado) return r.continue();
    cortado = true;
    return r.abort('connectionreset');
  });
  await page.getByRole('button', { name: 'Ingresar', exact: true }).click();

  await expect(page).toHaveURL(/\/explorar/);
  await expect(page.getByText('Ocurrió un error inesperado')).toHaveCount(0);
  expect(cortado).toBe(true);
  // La sesión volvió sola con la cookie de refresco tras cargar la página completa.
  const r = await page.request.post('/api/v1/auth/refrescar', { headers: { 'X-Trueke-Csrf': '1' } });
  expect(r.ok(), await r.text()).toBe(true);
});
