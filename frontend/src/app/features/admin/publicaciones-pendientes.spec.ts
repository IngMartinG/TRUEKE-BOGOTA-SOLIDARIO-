import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import type { PublicacionEnRevisionDto, PublicacionesEnRevisionDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import PublicacionesPendientes from './publicaciones-pendientes';

describe('Publicaciones en revisión', () => {
  // jsdom no implementa <dialog>.showModal() (lo usa el modal de rechazo).
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true;
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false;
    };
  });

  const pub = (id: string, titulo: string, puntajeRiesgo: number, motivos: string[]): PublicacionEnRevisionDto => ({
    id,
    titulo,
    descripcion: 'En buen estado',
    condicion: 'Usado',
    categoria: 'Hogar',
    modo: 'Trueke',
    localidad: 'Kennedy',
    municipio: 'Bogotá, D.C.',
    imagenes: [],
    autor: { id: 'a-' + id, nombreCompleto: 'Ana Pérez', fechaRegistro: new Date().toISOString(), identidadVerificada: false },
    puntajeRiesgo,
    motivos,
    desdeUtc: new Date().toISOString(),
  });
  const cola: PublicacionesEnRevisionDto = {
    items: [
      pub('p1', 'Cachorros', 140, ['Posible artículo prohibido (cachorros)', 'Cuenta de menos de 7 días']),
      pub('p2', 'Silla de madera', 100, ['Una de sus primeras 3 publicaciones']),
      pub('p3', 'Mesa', 60, ['Datos de contacto o enlaces en el texto']),
    ],
    total: 3,
  };

  async function crear(datos: PublicacionesEnRevisionDto = cola) {
    const api = {
      publicacionesEnRevision: vi.fn().mockReturnValue(of(datos)),
      aprobarPublicaciones: vi.fn((ids: string[]) => of(ids.length)),
      rechazarPublicacion: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [PublicacionesPendientes],
      providers: [
        { provide: AdminApi, useValue: api },
        { provide: AvisosService, useValue: { exito: vi.fn(), error: vi.fn() } },
        { provide: TiempoRealService, useValue: { colaModeracion: signal<number | null>(null) } },
      ],
    });
    const f = TestBed.createComponent(PublicacionesPendientes);
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const listo = async () => {
      await new Promise((r) => setTimeout(r));
      await f.whenStable();
      f.detectChanges();
    };
    const tecla = async (key: string) => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
      await listo();
    };
    return { el, api, tecla, listo };
  }

  const titulo = (el: HTMLElement) => el.querySelector('article h3')?.textContent?.trim();

  it('muestra la de más riesgo con sus motivos', async () => {
    const { el } = await crear();
    expect(titulo(el)).toBe('Cachorros');
    expect(el.textContent).toContain('Posible artículo prohibido (cachorros)');
    expect(el.textContent).toContain('Riesgo alto');
  });

  it('A aprueba la actual y pasa a la siguiente', async () => {
    const { el, api, tecla } = await crear();
    await tecla('a');
    expect(api.aprobarPublicaciones).toHaveBeenCalledWith(['p1']);
    expect(titulo(el)).toBe('Silla de madera');
  });

  it('X marca y "Aprobar marcadas" las aprueba juntas', async () => {
    const { el, api, tecla, listo } = await crear();
    await tecla('x');
    await tecla('ArrowDown');
    await tecla('x');
    const boton = [...el.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent?.includes('Aprobar marcadas'))!;
    expect(boton.textContent).toContain('(2)');
    boton.click();
    await listo();
    expect(api.aprobarPublicaciones).toHaveBeenCalledWith(['p1', 'p2']);
    expect(titulo(el)).toBe('Mesa');
  });

  it('R rechaza con un motivo listo', async () => {
    const { api, tecla, listo } = await crear();
    await tecla('r');
    const enviar = [...document.querySelectorAll<HTMLButtonElement>('button[type="submit"]')].find((b) => b.textContent?.includes('Rechazar'))!;
    enviar.click();
    await listo();
    expect(api.rechazarPublicacion).toHaveBeenCalledWith('p1', expect.stringContaining('artículo prohibido'));
  });

  it('sin pendientes lo dice', async () => {
    const { el } = await crear({ items: [], total: 0 });
    expect(el.textContent).toContain('No hay publicaciones por revisar');
  });
});
