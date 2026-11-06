import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { PendientesModeracionDto } from '../../api/tipos';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import Panel from './panel';

describe('Panel de administración', () => {
  const vacio: PendientesModeracionDto = {
    publicaciones: 0, denuncias: 0, apelaciones: 0, fotos: 0, verificaciones: 0, negocios: 0,
    pqr: 0, pagos: 0, facturas: 0, menores: 0, fundaciones: 0, empresas: 0, total: 0,
  };

  async function crear(pendientes: PendientesModeracionDto | null) {
    const pendientesModeracion = signal(pendientes);
    const sincronizarModeracion = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [Panel],
      providers: [
        provideRouter([]),
        { provide: TiempoRealService, useValue: { pendientesModeracion, sincronizarModeracion } },
        {
          provide: SesionService,
          useValue: { usuario: signal({ rol: 'SuperUsuario', dosFactoresActivo: true }), esSuperUsuario: computed(() => true) },
        },
      ],
    });
    const f = TestBed.createComponent(Panel);
    f.detectChanges();
    await f.whenStable();
    return { f, el: f.nativeElement as HTMLElement, pendientesModeracion, sincronizarModeracion };
  }

  const insignia = (el: HTMLElement, texto: string) =>
    [...el.querySelectorAll('nav a')].find((a) => a.textContent?.includes(texto))?.querySelector('.rounded-full')?.textContent?.trim();

  it('muestra una insignia en cada sección con pendientes y cuenta al abrir', async () => {
    const { el, sincronizarModeracion } = await crear({ ...vacio, denuncias: 2, fotos: 1, menores: 3, fundaciones: 120, total: 126 });
    expect(sincronizarModeracion).toHaveBeenCalled();
    expect(insignia(el, 'Denuncias')).toContain('2');
    expect(insignia(el, 'Fotos de perfil')).toContain('1');
    expect(insignia(el, 'Usuarios')).toContain('3'); // posibles menores de edad
    expect(insignia(el, 'Fundaciones')).toContain('99+');
    expect(insignia(el, 'Publicaciones')).toBeUndefined(); // en cero no se muestra
    expect(insignia(el, 'Denuncias')).toContain('por revisar'); // el lector de pantalla oye qué significa el número
    const opcion = [...el.querySelectorAll('option')].find((o) => o.textContent?.includes('Denuncias'));
    expect(opcion?.textContent).toContain('(2 por revisar)');
  });

  it('se actualiza en vivo cuando cambian los pendientes', async () => {
    const { f, el, pendientesModeracion } = await crear(null);
    expect(insignia(el, 'Verificaciones')).toBeUndefined();
    pendientesModeracion.set({ ...vacio, verificaciones: 4, total: 4 });
    f.detectChanges();
    expect(insignia(el, 'Verificaciones')).toContain('4');
  });
});
