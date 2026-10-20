import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { ConfigService } from '../../core/config.service';
import ApoyaGracias from './apoya-gracias';
import { esRegresoWompi, urlTransaccionWompi } from './wompi-transaccion';

function respuesta(status: string, amount_in_cents = 1_500_000) {
  return { ok: true, status: 200, json: async () => ({ data: { status, amount_in_cents, customer_email: 'no@se.usa' } }) };
}

async function montar(params: Record<string, string>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(params) } } },
      { provide: ConfigService, useValue: { donacionUrl: () => 'https://pagos.ejemplo.co/aporte' } },
    ],
  });
  const fixture = TestBed.createComponent(ApoyaGracias);
  await fixture.whenStable();
  await new Promise((r) => setTimeout(r));
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Agradecimiento por el aporte', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('valida el regreso de Wompi y arma la URL según el ambiente', () => {
    expect(esRegresoWompi('12345-1668624561-38705', 'prod')).toBe(true);
    expect(esRegresoWompi('12345', 'prod')).toBe(false);
    expect(esRegresoWompi('../../x', 'test')).toBe(false);
    expect(esRegresoWompi('12345-1668624561-38705', 'otro')).toBe(false);
    expect(urlTransaccionWompi('1-2-3', 'test')).toBe('https://sandbox.wompi.co/v1/transactions/1-2-3');
    expect(urlTransaccionWompi('1-2-3', 'prod')).toBe('https://production.wompi.co/v1/transactions/1-2-3');
  });

  it('aprobado: celebra con el valor y no muestra datos de Wompi', async () => {
    const fetch = vi.fn().mockResolvedValue(respuesta('APPROVED'));
    vi.stubGlobal('fetch', fetch);
    const el = await montar({ id: '12345-1668624561-38705', env: 'prod' });
    expect(fetch).toHaveBeenCalledWith('https://production.wompi.co/v1/transactions/12345-1668624561-38705', expect.objectContaining({ credentials: 'omit' }));
    expect(el.textContent).toContain('¡Gracias!');
    expect(el.textContent).toContain('$15.000');
    expect(el.textContent).not.toContain('no@se.usa');
  });

  it('rechazado: dice que no se cobró y ofrece intentarlo de nuevo', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta('DECLINED')));
    const el = await montar({ id: '12345-1668624561-38705', env: 'test' });
    expect(el.textContent).toContain('no se te cobró');
    expect(el.querySelector<HTMLAnchorElement>('a[target="_blank"]')?.href).toBe('https://pagos.ejemplo.co/aporte');
    expect(el.textContent).not.toContain('¡Gracias!');
  });

  it('si no se puede consultar, agradece sin afirmar que se aprobó', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('CSP')));
    const el = await montar({ id: '12345-1668624561-38705', env: 'prod' });
    expect(el.textContent).toContain('No pudimos confirmar');
    expect(el.textContent).not.toContain('¡Gracias!');
  });

  it('sin parámetros de Wompi no consulta nada', async () => {
    const fetch = vi.fn();
    vi.stubGlobal('fetch', fetch);
    const el = await montar({});
    expect(fetch).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Gracias por querer apoyar');
  });
});
