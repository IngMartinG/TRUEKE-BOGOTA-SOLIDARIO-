import { HttpErrorResponse } from '@angular/common/http';
import type { Breadcrumb, ErrorEvent } from '@sentry/browser';
import { debeReportarse, limpiarEvento, limpiarMiga, sinQuery } from './monitoreo';

describe('Monitoreo de errores sin datos personales', () => {
  it('quita usuario, cookies, query, Referer y correos del evento', () => {
    const evento = {
      type: undefined,
      user: { id: 'u1', ip_address: '1.2.3.4', email: 'ana@correo.co' },
      message: 'No se pudo avisar a ana@correo.co',
      request: {
        url: 'https://app/verificar-correo?token=SECRETO#x',
        query_string: 'token=SECRETO',
        cookies: { a: 'b' },
        headers: { 'User-Agent': 'Firefox', Referer: 'https://app/restablecer?token=SECRETO' },
      },
      exception: {
        values: [
          { type: 'Error', value: 'Falló con pepe@x.com' },
          { type: 'Error', value: 'Falló https://x.supabase.co/a.jpg?token=SECRETO con 403' },
        ],
      },
    } as ErrorEvent;

    const limpio = limpiarEvento(evento);

    expect(limpio.user).toBeUndefined();
    expect(limpio.request).toEqual({ url: 'https://app/verificar-correo', headers: { 'User-Agent': 'Firefox' } });
    expect(limpio.message).toBe('No se pudo avisar a [correo]');
    expect(limpio.exception?.values?.[0].value).toBe('Falló con [correo]');
    expect(limpio.exception?.values?.[1].value).toBe('Falló https://x.supabase.co/a.jpg con 403');
    expect(JSON.stringify(limpio)).not.toContain('SECRETO');
  });

  it('quita la query de las URL de las migas (enlaces firmados, navegación)', () => {
    const miga = limpiarMiga({
      category: 'fetch',
      data: { url: 'https://x.supabase.co/upload/sign/a.jpg?token=SECRETO', method: 'PUT', from: '/a?b=1', to: '/c#d' },
    } as Breadcrumb);
    expect(miga.data).toEqual({ url: 'https://x.supabase.co/upload/sign/a.jpg', method: 'PUT', from: '/a', to: '/c' });
    expect(sinQuery('/sin-nada')).toBe('/sin-nada');
  });

  it('no reporta errores de la API ni fragmentos que no cargaron', () => {
    expect(debeReportarse(new HttpErrorResponse({ status: 500 }))).toBe(false);
    const deRecurso = new Error('Resource is currently in an error state', {
      cause: new HttpErrorResponse({ status: 502 }),
    });
    expect(debeReportarse(deRecurso)).toBe(false);
    expect(debeReportarse(new TypeError('Failed to fetch dynamically imported module: https://x/chunk-A1.js'))).toBe(false);
    expect(debeReportarse(new TypeError("Cannot read properties of undefined (reading 'id')"))).toBe(true);
  });
});
