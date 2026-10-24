import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError, type Observable } from 'rxjs';
import type { ImpactoDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { ResumenImpacto, UMBRAL_IMPACTO } from './resumen-impacto';

describe('ResumenImpacto', () => {
  function crear(impacto$: Observable<ImpactoDto>) {
    TestBed.configureTestingModule({
      imports: [ResumenImpacto],
      providers: [provideRouter([]), { provide: CatalogoApi, useValue: { impacto$ } }],
    });
    const f = TestBed.createComponent(ResumenImpacto);
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('muestra las cifras reales y lleva a la página de impacto', () => {
    const el = crear(of({ objetosConNuevoHogar: 1250, personasQueIntercambiaron: 800, esteMes: 42 } as ImpactoDto));
    expect(el.querySelector('h2')?.textContent).toContain('Lo que hemos logrado juntos');
    expect(el.textContent).toMatch(/1\.250/);
    expect(el.textContent).toContain('42');
    expect(el.querySelector('a')?.getAttribute('href')).toBe('/impacto');
  });

  it('muestra los kilos y el CO₂ como estimación y enlaza al cálculo', () => {
    const el = crear(of({ objetosConNuevoHogar: 300, personasQueIntercambiaron: 120, esteMes: 5, kgReutilizados: 412.4, kgCo2Evitado: 2350 } as ImpactoDto));
    expect(el.textContent).toContain('≈ 412 kg');
    expect(el.textContent).toContain('≈ 2,4 t');
    expect(el.textContent).toContain('estimación');
    expect(el.querySelector('a[href="/impacto#calculo"]')).not.toBeNull();
  });

  it('no presume ceros: se oculta si todavía no hay intercambios', () => {
    const el = crear(of({ objetosConNuevoHogar: 0, personasQueIntercambiaron: 0, esteMes: 0 } as ImpactoDto));
    expect(el.querySelector('section')).toBeNull();
  });

  it('se oculta por debajo del umbral y aparece al alcanzarlo', () => {
    const bajo = crear(of({ objetosConNuevoHogar: UMBRAL_IMPACTO - 1, personasQueIntercambiaron: 10, esteMes: 2 } as ImpactoDto));
    expect(bajo.querySelector('section')).toBeNull();
    TestBed.resetTestingModule();
    const justo = crear(of({ objetosConNuevoHogar: UMBRAL_IMPACTO, personasQueIntercambiaron: 10, esteMes: 2 } as ImpactoDto));
    expect(justo.querySelector('section')).not.toBeNull();
  });

  it('sin intercambios en el mes no muestra "0 este mes"', () => {
    const el = crear(of({ objetosConNuevoHogar: 300, personasQueIntercambiaron: 120, esteMes: 0 } as ImpactoDto));
    expect(el.querySelector('section')).not.toBeNull();
    expect(el.textContent).not.toContain('este mes');
  });

  it('se oculta si la API falla', () => {
    const el = crear(throwError(() => new Error('sin red')));
    expect(el.querySelector('section')).toBeNull();
  });
});
