import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { apiBase } from '../entorno';
import { SesionService } from '../sesion.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let control: HttpTestingController;
  let sesion: SesionService;
  const api = apiBase();
  const sesionValida = (token: string) => ({ token, expiraUtc: new Date(Date.now() + 900_000).toISOString(), usuario: { id: 'u1' } });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    control = TestBed.inject(HttpTestingController);
    sesion = TestBed.inject(SesionService);
  });

  afterEach(() => {
    control.verify();
    sesion.limpiar();
  });

  it('añade el JWT solo a la API propia', () => {
    sesion.establecer(sesionValida('t1'));
    void firstValueFrom(http.get(`${api}/usuarios/yo`));
    void firstValueFrom(http.get('https://cuenta.blob.core.windows.net/imagenes/x.webp'));

    expect(control.expectOne(`${api}/usuarios/yo`).request.headers.get('Authorization')).toBe('Bearer t1');
    expect(control.expectOne('https://cuenta.blob.core.windows.net/imagenes/x.webp').request.headers.has('Authorization')).toBe(false);
  });

  it('envía cookie y cabecera anti-CSRF en las rutas /auth/*', () => {
    const p = firstValueFrom(http.post(`${api}/auth/refrescar`, {})).catch(() => null);
    const req = control.expectOne(`${api}/auth/refrescar`);
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.headers.get('X-Trueke-Csrf')).toBe('1');
    req.flush(null, { status: 401, statusText: 'No autorizado' });
    return p;
  });

  it('ante un 401 refresca una sola vez y reintenta con el token nuevo', async () => {
    sesion.establecer(sesionValida('viejo'));
    const a = firstValueFrom(http.get(`${api}/solicitudes/enviadas`));
    const b = firstValueFrom(http.get(`${api}/solicitudes/recibidas`));

    control.expectOne(`${api}/solicitudes/enviadas`).flush(null, { status: 401, statusText: 'No autorizado' });
    control.expectOne(`${api}/solicitudes/recibidas`).flush(null, { status: 401, statusText: 'No autorizado' });

    // Las dos peticiones fallidas comparten un único refresco (el backend detecta reuso de la cookie).
    control.expectOne(`${api}/auth/refrescar`).flush(sesionValida('nuevo'));

    const reintentoA = control.expectOne(`${api}/solicitudes/enviadas`);
    const reintentoB = control.expectOne(`${api}/solicitudes/recibidas`);
    expect(reintentoA.request.headers.get('Authorization')).toBe('Bearer nuevo');
    reintentoA.flush([]);
    reintentoB.flush([]);
    await expect(a).resolves.toEqual([]);
    await expect(b).resolves.toEqual([]);
  });

  it('no intenta refrescar ante el 401 del login ni ante un 401 con código (2FA)', async () => {
    const login = firstValueFrom(http.post(`${api}/auth/login`, {}));
    control.expectOne(`${api}/auth/login`).flush({ title: 'Credenciales inválidas' }, { status: 401, statusText: 'No autorizado' });
    await expect(login).rejects.toBeTruthy();

    sesion.establecer(sesionValida('t'));
    const admin = firstValueFrom(http.get(`${api}/admin/usuarios`));
    control.expectOne(`${api}/admin/usuarios`).flush({ codigo: '2fa_requerido' }, { status: 401, statusText: 'No autorizado' });
    await expect(admin).rejects.toBeTruthy();
    control.expectNone(`${api}/auth/refrescar`);
  });

  it('si el refresco falla, cierra la sesión local', async () => {
    sesion.establecer(sesionValida('viejo'));
    const p = firstValueFrom(http.get(`${api}/usuarios/yo`));
    control.expectOne(`${api}/usuarios/yo`).flush(null, { status: 401, statusText: 'No autorizado' });
    control.expectOne(`${api}/auth/refrescar`).flush(null, { status: 401, statusText: 'No autorizado' });
    await expect(p).rejects.toBeTruthy();
    expect(sesion.autenticado()).toBe(false);
  });
});
