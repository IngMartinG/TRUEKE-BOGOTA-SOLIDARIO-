import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { ImpactoDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import Impacto from './impacto';

describe('Impacto', () => {
  async function crear(d: Partial<ImpactoDto>, filtrado: Partial<ImpactoDto> = d) {
    const api = { impacto$: of(d as ImpactoDto), impactoFiltrado: vi.fn().mockReturnValue(of(filtrado as ImpactoDto)) };
    TestBed.configureTestingModule({
      imports: [Impacto],
      providers: [provideRouter([]), { provide: CatalogoApi, useValue: api }],
    });
    const f = TestBed.createComponent(Impacto);
    await f.whenStable();
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const listo = async () => {
      await f.whenStable();
      f.detectChanges();
    };
    return { el, api, listo };
  }

  it('carga las cifras (el recurso no puede depender del contexto de inyección)', async () => {
    const { el } = await crear({ objetosConNuevoHogar: 12, truekes: 5, donaciones: 4, compras: 3, kgReutilizados: 8.5, kgCo2Evitado: 1250, kgDeclarados: 4.25, versionMetodologia: '2.0.0' });
    expect(el.textContent).not.toContain('No pudimos cargar');
    expect(el.textContent).toContain('≈ 8,5 kg');
    expect(el.textContent).toContain('≈ 1,3 t');
    expect(el.textContent).toContain('metodología 2.0.0');
    expect(el.textContent).toContain('El 50 % de los kilos lo declaró quien publicó');
  });

  it('publica los factores por material y el peso típico por categoría', async () => {
    const { el } = await crear({
      objetosConNuevoHogar: 0,
      versionMetodologia: '2.0.0',
      factores: [{ material: 'Electronica', kgCo2PorKg: 12, fuente: 'WRAP', version: '2.0.0' }],
      pesosPorCategoria: [{ categoria: 'Tecnología', kgPorObjeto: 1, material: 'Electronica' }],
    });
    const filas = el.querySelectorAll('#calculo tbody tr');
    expect(filas[0]?.textContent).toContain('Electrónica');
    expect(filas[0]?.textContent).toContain('12');
    expect(filas[1]?.textContent).toContain('Tecnología');
    // Sin intercambios no se muestran "≈ 0 kg"
    expect(el.querySelector('dl')?.textContent ?? '').not.toContain('≈');
  });

  it('filtra por periodo y avisa cuando una localidad tiene muy pocos intercambios', async () => {
    const suba = [{ localidad: 'Suba', municipio: 'Bogotá, D.C.', municipioCodigo: '11001', intercambios: 5 }];
    const { el, api, listo } = await crear({ objetosConNuevoHogar: 9, localidadesDisponibles: suba }, { reservado: true, localidadesDisponibles: suba });
    const mes = [...el.querySelectorAll<HTMLInputElement>('input[name="periodo"]')].find((i) => i.value === 'Mes')!;
    mes.dispatchEvent(new Event('change'));
    await listo();
    const select = el.querySelector<HTMLSelectElement>('#filtro-localidad')!;
    select.value = '11001|Suba';
    select.dispatchEvent(new Event('change'));
    await listo();
    expect(api.impactoFiltrado).toHaveBeenLastCalledWith({ periodo: 'Mes', modo: null, municipioCodigo: '11001', localidad: 'Suba' });
    expect(el.textContent).toContain('menos de 3 intercambios');
  });
});
