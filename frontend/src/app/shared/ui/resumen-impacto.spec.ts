import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError, type Observable } from 'rxjs';
import type { ImpactoDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { ResumenImpacto } from './resumen-impacto';

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

  it('no presume ceros: se oculta si todavía no hay intercambios', () => {
    const el = crear(of({ objetosConNuevoHogar: 0, personasQueIntercambiaron: 0, esteMes: 0 } as ImpactoDto));
    expect(el.querySelector('section')).toBeNull();
  });

  it('se oculta si la API falla', () => {
    const el = crear(throwError(() => new Error('sin red')));
    expect(el.querySelector('section')).toBeNull();
  });
});
