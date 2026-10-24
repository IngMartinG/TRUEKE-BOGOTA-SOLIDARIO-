import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { FundacionPublicaDto, FundacionResumenDto, PublicacionDtoPaginaDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { SesionService } from '../../core/sesion.service';
import Fundacion from './fundacion';
import Fundaciones from './fundaciones';

const sesion = { autenticado: signal(false), usuario: signal<{ id: string } | null>(null) };

describe('Fundaciones', () => {
  const lista: FundacionResumenDto[] = [
    { id: 'a', nombre: 'Fundación Manos de Bosa', localidad: 'Bosa', municipio: 'Bogotá, D.C.', descripcion: 'Refuerzo escolar', necesidades: 3, necesidadesUrgentes: 1 },
    { id: 'b', nombre: 'Corporación Semillas', localidad: 'Kennedy', municipio: 'Bogotá, D.C.', descripcion: 'Huertas', necesidades: 0, necesidadesUrgentes: 0 },
  ];

  async function crear() {
    TestBed.configureTestingModule({
      imports: [Fundaciones],
      providers: [provideRouter([]), { provide: SesionService, useValue: sesion }, { provide: FundacionesApi, useValue: { listar: () => of(lista) } }],
    });
    const f = TestBed.createComponent(Fundaciones);
    await f.whenStable();
    f.detectChanges();
    return f;
  }

  it('muestra cuántas cosas necesita cada una y cuántas son urgentes', async () => {
    const el = (await crear()).nativeElement as HTMLElement;
    expect(el.querySelectorAll('ul li a').length).toBe(2);
    expect(el.textContent).toContain('3 cosas que necesita');
    expect(el.textContent).toContain('1 urgente');
    expect(el.textContent).toContain('Aún no ha publicado lo que necesita');
  });

  it('filtra por nombre o barrio sin importar tildes ni mayúsculas', async () => {
    const f = await crear();
    const input = (f.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#buscar-fundacion')!;
    input.value = 'BOSÁ';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    const tarjetas = (f.nativeElement as HTMLElement).querySelectorAll('ul li a');
    expect(tarjetas.length).toBe(1);
    expect(tarjetas[0]!.textContent).toContain('Manos de Bosa');
  });
});

describe('Fundacion (página pública)', () => {
  const fundacion: FundacionPublicaDto = {
    id: 'a', nombre: 'Fundación Manos de Bosa', localidad: 'Bosa', municipio: 'Bogotá, D.C.', descripcion: 'Refuerzo escolar',
    nit: '900123456-8', verificadaDesde: '2026-10-24T10:00:00Z', totalCalificaciones: 0,
    necesidades: [{ id: 'n1', texto: 'Cuadernos cuadriculados', categoriaId: 3, categoria: 'Libros', cantidad: 40, urgente: true, fechaUtc: '2026-10-24T10:00:00Z' }],
  };

  async function crear() {
    TestBed.configureTestingModule({
      imports: [Fundacion],
      providers: [
        provideRouter([]),
        { provide: SesionService, useValue: sesion },
        { provide: FundacionesApi, useValue: { obtener: () => of(fundacion) } },
        { provide: CuentaApi, useValue: { publicacionesDe: () => of({ items: [], total: 0, pagina: 1, tamano: 12 } as PublicacionDtoPaginaDto) } },
      ],
    });
    const f = TestBed.createComponent(Fundacion);
    f.componentRef.setInput('id', 'a');
    await f.whenStable();
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('lleva cada necesidad a publicar una donación con ese título y categoría', async () => {
    const el = await crear();
    expect(el.textContent).toContain('Fundación verificada');
    expect(el.textContent).toContain('NIT 900123456-8');
    expect(el.textContent).toContain('Urgente');
    const donar = el.querySelector<HTMLAnchorElement>('a[aria-label="Donar: Cuadernos cuadriculados"]')!;
    const url = new URL(donar.href);
    expect(url.pathname).toBe('/publicar');
    expect(url.searchParams.get('modo')).toBe('Donacion');
    expect(url.searchParams.get('titulo')).toBe('Cuadernos cuadriculados');
    expect(url.searchParams.get('categoria')).toBe('3');
  });
});
