import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { CrearPublicacionRequest } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { EmpresasApi } from '../../core/api/empresas.api';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { SesionService } from '../../core/sesion.service';
import { SubidasService } from '../../core/subidas.service';
import Publicar from './publicar';

const NECESIDAD = '3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b';

/** Lo que la prueba toca del componente (miembros protegidos). */
interface Interno {
  form: { patchValue(v: object): void };
  paso: { set(v: number): void };
  pasoMaximo: { set(v: number): void };
  publicar(): Promise<void>;
}

async function montar(entradas: Record<string, string>) {
  const crear = vi.fn((_: CrearPublicacionRequest) => of({ id: 'p1' }));
  const avisos = { exito: vi.fn(), error: vi.fn(), info: vi.fn() };
  TestBed.configureTestingModule({
    imports: [Publicar],
    providers: [
      provideRouter([]),
      { provide: CatalogoApi, useValue: { categorias$: of([{ id: 1, nombre: 'Libros' }]), objetosTipicos$: of([]), crear } },
      { provide: FundacionesApi, useValue: { listado$: of([]) } },
      { provide: EmpresasApi, useValue: { campanasActivas: () => of([]) } },
      { provide: AvisosService, useValue: avisos },
      { provide: SubidasService, useValue: {} },
      { provide: ConfigService, useValue: { maxImagenes: signal(5), tamanoMaximo: signal(5 * 1024 * 1024) } },
      { provide: SesionService, useValue: { usuario: signal({ id: 'u1', municipioCodigo: '11001', localidad: 'Bosa' }) } },
    ],
  });
  localStorage.clear();
  const fixture = TestBed.createComponent(Publicar);
  for (const [k, v] of Object.entries(entradas)) fixture.componentRef.setInput(k, v);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const c = fixture.componentInstance as unknown as Interno;
  c.form.patchValue({ condicion: 'Nuevo', descripcion: 'Veinte cuadernos sin usar' });
  return { fixture, c, crear, avisos, el: fixture.nativeElement as HTMLElement };
}

describe('Publicar desde «Tengo esto»', () => {
  const desdeFundacion = { modo: 'Donacion', titulo: 'Cuadernos cuadriculados', categoria: '1', necesidad: NECESIDAD };

  it('avisa que la fundación se enterará y envía la necesidad al crear la donación', async () => {
    const { fixture, c, crear, avisos, el } = await montar(desdeFundacion);
    c.pasoMaximo.set(4);
    c.paso.set(4);
    fixture.detectChanges();
    expect(el.querySelector('[data-aviso-fundacion]')?.textContent).toContain('Le avisaremos a la fundación');

    await c.publicar();
    const cuerpo = crear.mock.calls[0]![0];
    expect(cuerpo.modo).toBe('Donacion');
    expect(cuerpo.titulo).toBe('Cuadernos cuadriculados');
    expect(cuerpo.paraNecesidadId).toBe(NECESIDAD);
    expect(avisos.exito).toHaveBeenCalledWith('¡Tu publicación ya está visible!', 'Le avisamos a la fundación para que pueda pedírtela.');
  });

  it('si la persona cambia a otro modo, no se envía la necesidad', async () => {
    const { c, crear } = await montar(desdeFundacion);
    c.form.patchValue({ modo: 'Trueke' });
    await c.publicar();
    expect(crear.mock.calls[0]![0].paraNecesidadId).toBeUndefined();
  });

  it('ignora una necesidad que no es un identificador válido', async () => {
    const { c, crear } = await montar({ ...desdeFundacion, necesidad: 'no-es-un-guid' });
    await c.publicar();
    expect(crear.mock.calls[0]![0].paraNecesidadId).toBeUndefined();
  });
});
