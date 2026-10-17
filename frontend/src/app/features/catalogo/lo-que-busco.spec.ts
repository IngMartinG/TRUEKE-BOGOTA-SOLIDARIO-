import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { CoincidenciaDeseoDto, DeseoDto, PublicacionDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import LoQueBusco from './lo-que-busco';

const deseo: DeseoDto = {
  id: 'd1',
  texto: 'Bicicleta',
  categoriaId: null,
  categoria: null,
  publico: true,
  avisosActivos: true,
  oculto: false,
  motivoOcultamiento: null,
  fechaUtc: '2026-10-17T00:00:00Z',
};

function pub(id: string, titulo: string): PublicacionDto {
  return {
    id,
    titulo,
    modo: 'Trueke',
    imagenes: [],
    categoria: { id: 1, nombre: 'Deportes' },
    propietario: { id: 'u-' + id, nombre: 'Beto' },
  } as PublicacionDto;
}

describe('LoQueBusco', () => {
  async function crear(deseos: DeseoDto[], coincidencias: CoincidenciaDeseoDto[]) {
    TestBed.configureTestingModule({
      imports: [LoQueBusco],
      providers: [
        provideRouter([]),
        {
          provide: CatalogoApi,
          useValue: {
            deseos: () => of(deseos),
            coincidencias: () => of(coincidencias),
            categorias$: of([]),
          },
        },
      ],
    });
    const f = TestBed.createComponent(LoQueBusco);
    await f.whenStable();
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('el trueke posible propone ofrecer lo mío ya elegido', async () => {
    const el = await crear(
      [deseo],
      [
        {
          publicacion: pub('p1', 'Bicicleta de ruta'),
          tuDeseo: 'Bicicleta',
          mutua: true,
          suDeseo: 'Casco',
          tuPublicacionId: 'm1',
          tuPublicacionTitulo: 'Casco rojo',
        },
      ],
    );
    const lista = el.querySelector('[aria-label="Truekes posibles"]');
    expect(lista?.textContent).toContain('Casco rojo');
    expect(lista?.textContent).toContain('busca «Casco»');
    const proponer = [...el.querySelectorAll('a')].find((a) =>
      a.textContent?.includes('Proponer trueke'),
    );
    expect(proponer?.getAttribute('href')).toBe('/publicacion/p1?ofrecer=m1');
  });

  it('sin deseos invita a empezar la lista y no muestra coincidencias', async () => {
    const el = await crear([], []);
    expect(el.textContent).toContain('Empieza tu lista');
    expect(el.textContent).toContain('0 de 10');
  });
});
