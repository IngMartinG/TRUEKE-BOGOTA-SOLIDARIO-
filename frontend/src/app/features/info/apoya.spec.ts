import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { ConfigService } from '../../core/config.service';
import Apoya from './apoya';

function montar(url: string | null, impacto: object | null = null) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ConfigService, useValue: { donacionUrl: () => url } },
      { provide: CatalogoApi, useValue: { impacto$: of(impacto) } },
    ],
  });
  const fixture = TestBed.createComponent(Apoya);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Apoya el proyecto', () => {
  it('con enlace configurado abre el aporte en otra pestaña y aclara que no da beneficios', () => {
    const el = montar('https://pagos.ejemplo.co/aporte');
    const enlaces = el.querySelectorAll<HTMLAnchorElement>('a[target="_blank"]');
    expect(enlaces.length).toBeGreaterThan(0);
    for (const a of enlaces) {
      expect(a.href).toBe('https://pagos.ejemplo.co/aporte');
      expect(a.rel).toContain('noopener');
    }
    expect(el.textContent).toContain('no da Eco-Puntos');
    expect(el.textContent).toContain('no se puede descontar de impuestos');
  });

  it('muestra las cifras reales de la comunidad solo si hay intercambios', () => {
    expect(montar('https://pagos.ejemplo.co/aporte', { objetosConNuevoHogar: 0 }).textContent).not.toContain('hemos sostenido');
    TestBed.resetTestingModule();
    const el = montar('https://pagos.ejemplo.co/aporte', { objetosConNuevoHogar: 42, personasQueIntercambiaron: 30 });
    expect(el.textContent).toContain('hemos sostenido');
    expect(el.textContent).toContain('42');
  });

  it('sin enlace dice que por ahora no se reciben aportes', () => {
    const el = montar(null);
    expect(el.querySelector('a[target="_blank"]')).toBeNull();
    expect(el.textContent).toContain('no estamos recibiendo aportes');
  });
});
