import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import type { FotosEnRevisionDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import Fotos from './fotos';

describe('Fotos de perfil en revisión', () => {
  // jsdom no implementa <dialog>.showModal() (lo usa el modal de rechazo).
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true;
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false;
    };
  });
  const cola: FotosEnRevisionDto = {
    items: [
      { usuarioId: 'u1', nombreCompleto: 'Ana Pérez', fotoUrl: 'https://almacen.test/a.jpg', desdeUtc: new Date().toISOString() },
      { usuarioId: 'u2', nombreCompleto: 'Luis Gómez', fotoUrl: 'https://almacen.test/b.jpg', desdeUtc: new Date().toISOString() },
    ],
    total: 2,
    sinRevisar: 0,
  };

  async function crear(datos: FotosEnRevisionDto = cola) {
    const api = {
      fotosEnRevision: vi.fn().mockReturnValue(of(datos)),
      aprobarFoto: vi.fn().mockReturnValue(of(undefined)),
      rechazarFoto: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [Fotos],
      providers: [
        { provide: AdminApi, useValue: api },
        { provide: AvisosService, useValue: { exito: vi.fn(), error: vi.fn() } },
      ],
    });
    const f = TestBed.createComponent(Fotos);
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    const tecla = async (key: string) => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
      await f.whenStable();
      f.detectChanges();
    };
    return { f, el: f.nativeElement as HTMLElement, api, tecla };
  }

  it('muestra la primera foto en grande', async () => {
    const { el } = await crear();
    expect(el.querySelector('figure')?.textContent).toContain('Ana Pérez');
    expect(el.textContent).toContain('Foto 1 de 2');
  });

  it('A aprueba la foto actual y pasa a la siguiente', async () => {
    const { el, api, tecla } = await crear();
    await tecla('a');
    expect(api.aprobarFoto).toHaveBeenCalledWith('u1');
    expect(el.querySelector('figure')?.textContent).toContain('Luis Gómez');
  });

  it('las flechas cambian de foto y R abre el rechazo con un motivo listo', async () => {
    const { el, api, tecla } = await crear();
    await tecla('ArrowRight');
    expect(el.querySelector('figure')?.textContent).toContain('Luis Gómez');
    await tecla('r');
    const enviar = [...document.querySelectorAll<HTMLButtonElement>('button[type="submit"]')].find((b) => b.textContent?.includes('Rechazar'))!;
    expect(enviar.disabled).toBe(false);
    enviar.click();
    await new Promise((r) => setTimeout(r));
    expect(api.rechazarFoto).toHaveBeenCalledWith('u2', 'No se ve una cara en la foto.');
  });

  it('sin fotos pendientes lo dice', async () => {
    const { el } = await crear({ items: [], total: 0, sinRevisar: 3 });
    expect(el.textContent).toContain('No hay fotos por revisar');
  });
});
