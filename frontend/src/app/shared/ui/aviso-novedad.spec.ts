import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { NotificacionDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { AvisoNovedad, TIPO_NOVEDAD_VENTA_TRUEKE } from './aviso-novedad';

describe('AvisoNovedad', () => {
  const novedad: NotificacionDto = { id: 'n1', tipo: TIPO_NOVEDAD_VENTA_TRUEKE, mensaje: 'Novedad', leida: false };

  async function crear(items: NotificacionDto[] | Error, autenticado = true) {
    const api = {
      notificaciones: vi.fn().mockReturnValue(items instanceof Error ? throwError(() => items) : of({ items, total: items.length })),
      leerNotificacion: vi.fn().mockReturnValue(of(undefined)),
    };
    const noLeidas = signal(3);
    TestBed.configureTestingModule({
      imports: [AvisoNovedad],
      providers: [
        provideRouter([]),
        { provide: CuentaApi, useValue: api },
        { provide: SesionService, useValue: { autenticado: signal(autenticado), usuario: signal(autenticado ? { id: 'u1' } : null) } },
        { provide: TiempoRealService, useValue: { notificacionesNoLeidas: noLeidas } },
      ],
    });
    const f = TestBed.createComponent(AvisoNovedad);
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    return { f, el: f.nativeElement as HTMLElement, api, noLeidas };
  }

  const botones = (el: HTMLElement) => [...el.querySelectorAll('button')];

  it('sale al abrir la cuenta si la novedad está sin leer', async () => {
    const { el, api } = await crear([{ id: 'x', tipo: 'SolicitudNueva', leida: false }, novedad]);
    expect(api.notificaciones).toHaveBeenCalledWith(true, 1, 50, true);
    expect(el.textContent).toContain('También acepto trueke');
    expect(el.querySelector('[role="status"]')).not.toBeNull();
  });

  it('no sale sin la novedad, sin sesión o si falla la consulta', async () => {
    expect((await crear([{ id: 'x', tipo: 'SolicitudNueva', leida: false }])).el.textContent?.trim()).toBe('');
    TestBed.resetTestingModule();
    const sinSesion = await crear([novedad], false);
    expect(sinSesion.el.textContent?.trim()).toBe('');
    expect(sinSesion.api.notificaciones).not.toHaveBeenCalled();
    TestBed.resetTestingModule();
    expect((await crear(new Error('red'))).el.textContent?.trim()).toBe('');
  });

  it('"Entendido" la marca leída y la quita', async () => {
    const { f, el, api, noLeidas } = await crear([novedad]);
    botones(el).find((b) => b.textContent?.includes('Entendido'))!.click();
    await f.whenStable();
    f.detectChanges();
    expect(api.leerNotificacion).toHaveBeenCalledWith('n1');
    expect(noLeidas()).toBe(2);
    expect(el.textContent?.trim()).toBe('');
  });

  it('"Ver mis ventas" la marca leída y lleva a mis publicaciones', async () => {
    const { f, el, api } = await crear([novedad]);
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    botones(el).find((b) => b.textContent?.includes('Ver mis ventas'))!.click();
    await f.whenStable();
    expect(api.leerNotificacion).toHaveBeenCalledWith('n1');
    expect(navegar).toHaveBeenCalledWith(['/mis-publicaciones']);
  });
});
