import { expect, test } from '@playwright/test';
import { cuentaLista, ingresar } from './utilidades';

test.describe('asistente de ayuda', () => {
  test('responde, ofrece la acción y, si no sirvió, lleva a la PQRS pública con la conversación', async ({ page }) => {
    await page.goto('/');
    const lanzador = page.getByRole('button', { name: 'Ayuda', exact: true });
    await lanzador.click();
    const panel = page.getByRole('dialog', { name: 'Asistente de ayuda' });
    await expect(panel).toBeVisible();
    await expect(panel.getByText('Temas frecuentes')).toBeVisible();

    // Esc cierra y devuelve el foco al botón que lo abrió.
    await page.keyboard.press('Escape');
    await expect(panel).toBeHidden();
    await expect(lanzador).toBeFocused();

    await lanzador.click();
    await panel.getByLabel('Escribe tu pregunta').fill('me cobraron dos veces la recarga');
    await panel.getByRole('button', { name: 'Enviar pregunta' }).click();
    await expect(panel.getByText('Pagué y no veo mis Eco-Puntos o mi beneficio')).toBeVisible();
    await expect(panel.getByRole('button', { name: /Ir a mis pagos/ })).toBeVisible();

    await panel.getByRole('button', { name: 'No', exact: true }).click();
    await panel.getByRole('button', { name: 'Crear PQRS' }).click();

    // Sin sesión no hay retracto ni reversión: se propone un reclamo en la PQRS pública.
    await expect(page).toHaveURL(/\/pqrs\?tipo=Reclamo$/);
    await expect(page.getByRole('radio', { name: /^Reclamo/ })).toBeChecked();
    await expect(page.getByLabel('Asunto')).toHaveValue('me cobraron dos veces la recarga');
    await expect(page.getByLabel('Cuéntanos qué pasó')).toHaveValue(/pero no me sirvió/);
  });

  test('con sesión, lo que no encuentra lo escala a Soporte de la cuenta y se radica', async ({ page }) => {
    await ingresar(page, await cuentaLista(page, 'Gloria'));
    await page.goto('/intercambios');
    await page.getByRole('button', { name: 'Ayuda', exact: true }).click();
    const panel = page.getByRole('dialog', { name: 'Asistente de ayuda' });
    await panel.getByLabel('Escribe tu pregunta').fill('tengo una idea: modo oscuro automático por horario');
    await panel.getByLabel('Escribe tu pregunta').press('Enter');
    await expect(panel.getByText('No encontré una respuesta')).toBeVisible();
    await panel.getByRole('button', { name: 'Crear PQRS' }).click();

    await expect(page).toHaveURL(/\/cuenta\/soporte\?tipo=Sugerencia$/);
    await expect(page.getByRole('radio', { name: /^Sugerencia/ })).toBeChecked();
    await expect(page.getByLabel('Asunto')).toHaveValue('tengo una idea: modo oscuro automático por horario');
    await expect(page.getByLabel('Cuéntanos qué pasó')).toHaveValue(/no encontró una respuesta/);
    await page.getByRole('button', { name: 'Radicar solicitud' }).click();
    await expect(page.getByRole('listitem').filter({ hasText: 'tengo una idea: modo oscuro' })).toBeVisible();
  });
});
