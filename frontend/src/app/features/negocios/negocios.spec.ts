import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { NegocioPublicoDto, NegocioResumenDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { NegociosApi } from '../../core/api/negocios.api';
import Negocios from './negocios';
import Vitrina from './vitrina';

const negocio = (id: string, nombre: string, verificado: boolean, descripcion: string): NegocioResumenDto => ({
  id, nombre, verificado, descripcion, localidad: 'Kennedy', municipio: 'Bogotá, D.C.', publicaciones: 3, horario: 'Lunes a sábado',
});

describe('Negocios', () => {
  it('lista los negocios con su sello y filtra por lo que venden', async () => {
    TestBed.configureTestingModule({
      imports: [Negocios],
      providers: [provideRouter([]), {
        provide: NegociosApi,
        useValue: { listar: () => of([negocio('a', 'Muebles La Esquina', true, 'Muebles restaurados'), negocio('b', 'Libros Ruta', false, 'Libros usados')]) },
      }],
    });
    const f = TestBed.createComponent(Negocios);
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.querySelectorAll('ul li').length).toBe(2);
    expect(el.textContent).toContain('Negocio verificado');

    const buscar = el.querySelector<HTMLInputElement>('#buscar-negocio')!;
    buscar.value = 'libros';
    buscar.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect(el.querySelectorAll('ul li').length).toBe(1);
    expect(el.textContent).toContain('Libros Ruta');
  });

  it('la vitrina muestra el negocio y busca solo en su catálogo', async () => {
    const listar = vi.fn().mockReturnValue(of({ items: [], total: 0 }));
    const vitrina: NegocioPublicoDto = {
      id: 'n1', nombre: 'Muebles La Esquina', verificado: true, localidad: 'Kennedy', municipio: 'Bogotá, D.C.', horario: '9 a. m. a 6 p. m.',
      descripcion: 'Muebles restaurados', miembroDesde: '2026-01-01T00:00:00Z', totalCalificaciones: 0, publicaciones: 0,
    };
    TestBed.configureTestingModule({
      imports: [Vitrina],
      providers: [provideRouter([]), { provide: NegociosApi, useValue: { obtener: () => of(vitrina) } }, { provide: CatalogoApi, useValue: { listar } }],
    });
    const f = TestBed.createComponent(Vitrina);
    f.componentRef.setInput('id', 'n1');
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent).toContain('Muebles La Esquina');
    expect(el.textContent).toContain('9 a. m. a 6 p. m.');
    expect(listar).toHaveBeenCalledWith(expect.objectContaining({ propietarioId: 'n1', pagina: 1 }));
  });
});
