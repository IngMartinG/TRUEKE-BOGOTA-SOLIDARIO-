import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CuentaApi } from './api/cuenta.api';
import { conBypass, SubidasService } from './subidas.service';

const SAS = 'https://cuenta.blob.core.windows.net/imagenes/u/f.webp?sv=2024&sig=abc';

describe('SubidasService', () => {
  it('agrega ngsw-bypass sin tocar la SAS', () => {
    expect(conBypass(SAS)).toBe(`${SAS}&ngsw-bypass=true`);
    expect(conBypass('https://x.test/a')).toBe('https://x.test/a?ngsw-bypass=true');
  });

  it('sube sin pasar por el service worker y, si el almacenamiento rechaza el parámetro, reintenta sin él', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CuentaApi, useValue: { solicitarSubida: () => of({ urlSubida: SAS, urlArchivo: 'https://x/f.webp', metodo: 'PUT', cabeceras: {} }) } },
      ],
    });
    const control = TestBed.inject(HttpTestingController);
    const archivo = new File(['x'], 'f.webp', { type: 'image/webp' });
    const url = TestBed.inject(SubidasService).subirTodo(archivo);

    control.expectOne(`${SAS}&ngsw-bypass=true`).flush('no', { status: 403, statusText: 'Prohibido' });
    control.expectOne(SAS).flush(null, { status: 201, statusText: 'Created' });
    expect(await url).toBe('https://x/f.webp');
    control.verify();
  });
});
