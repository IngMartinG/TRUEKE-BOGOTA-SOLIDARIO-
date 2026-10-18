import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CuentaApi } from './api/cuenta.api';
import { conBypass, SubidasService } from './subidas.service';

const FIRMADA = 'https://proyecto.supabase.co/storage/v1/object/upload/sign/subidas/u/f.webp?token=abc';

describe('SubidasService', () => {
  it('agrega ngsw-bypass sin tocar el token de la URL firmada', () => {
    expect(conBypass(FIRMADA)).toBe(`${FIRMADA}&ngsw-bypass=true`);
    expect(conBypass('https://x.test/a')).toBe('https://x.test/a?ngsw-bypass=true');
  });

  it('sube sin pasar por el service worker y, si el almacenamiento rechaza el parámetro, reintenta sin él', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CuentaApi, useValue: { solicitarSubida: () => of({ urlSubida: FIRMADA, urlArchivo: 'https://x/f.webp', metodo: 'PUT', cabeceras: {} }) } },
      ],
    });
    const control = TestBed.inject(HttpTestingController);
    const archivo = new File(['x'], 'f.webp', { type: 'image/webp' });
    const url = TestBed.inject(SubidasService).subirTodo(archivo);

    control.expectOne(`${FIRMADA}&ngsw-bypass=true`).flush('no', { status: 403, statusText: 'Prohibido' });
    control.expectOne(FIRMADA).flush(null, { status: 200, statusText: 'OK' });
    expect(await url).toBe('https://x/f.webp');
    control.verify();
  });
});
