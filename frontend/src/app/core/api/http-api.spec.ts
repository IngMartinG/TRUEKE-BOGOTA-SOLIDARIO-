import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { apiBase } from '../entorno';
import { HttpApi, SIN_SERVICE_WORKER } from './http-api';

describe('HttpApi', () => {
  it('ninguna llamada a la API pasa por el service worker (ni espera a que se instale) y limpia los parámetros vacíos', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(HttpApi);
    const control = TestBed.inject(HttpTestingController);

    void firstValueFrom(api.get('/publicaciones', { params: { texto: 'bici', modo: null, pagina: 2, localidad: '' } }));
    void firstValueFrom(api.post('/auth/registrar', {}));

    const lista = control.expectOne((r) => r.url === `${apiBase()}/publicaciones`).request;
    expect(lista.params.get(SIN_SERVICE_WORKER)).toBe('true');
    expect(lista.params.keys().sort()).toEqual([SIN_SERVICE_WORKER, 'pagina', 'texto']);
    expect(control.expectOne((r) => r.url === `${apiBase()}/auth/registrar`).request.params.get(SIN_SERVICE_WORKER)).toBe('true');
    control.verify();
  });
});
