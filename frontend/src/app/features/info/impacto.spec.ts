import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { ImpactoDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import Impacto from './impacto';

describe('Impacto', () => {
  async function crear(d: Partial<ImpactoDto>) {
    TestBed.configureTestingModule({
      imports: [Impacto],
      providers: [provideRouter([]), { provide: CatalogoApi, useValue: { impacto$: of(d as ImpactoDto) } }],
    });
    const f = TestBed.createComponent(Impacto);
    await f.whenStable();
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('carga las cifras (el recurso no puede depender del contexto de inyección)', async () => {
    const el = await crear({ objetosConNuevoHogar: 12, truekes: 5, donaciones: 4, compras: 3, kgReutilizados: 8.5, kgCo2Evitado: 1250 });
    expect(el.textContent).not.toContain('No pudimos cargar');
    expect(el.textContent).toContain('≈ 8,5 kg');
    expect(el.textContent).toContain('≈ 1,3 t');
  });

  it('publica la tabla de factores del cálculo', async () => {
    const el = await crear({ objetosConNuevoHogar: 0, factores: [{ categoria: 'Tecnología', kgPorObjeto: 1, kgCo2PorObjeto: 40 }] });
    const fila = el.querySelector('#calculo tbody tr');
    expect(fila?.textContent).toContain('Tecnología');
    expect(fila?.textContent).toContain('40 kg');
    // Sin intercambios no se muestran "≈ 0 kg"
    expect(el.textContent).not.toContain('≈');
  });
});
