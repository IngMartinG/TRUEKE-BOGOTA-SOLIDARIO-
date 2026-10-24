import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { SolicitudDto } from '../../api/tipos';
import { TarjetaSolicitud } from './tarjeta-solicitud';

describe('TarjetaSolicitud (negociación)', () => {
  const base: SolicitudDto = {
    id: 's1',
    publicacionId: 'p1',
    publicacionTitulo: 'Bicicleta',
    modo: 'Trueke',
    estado: 'Pendiente',
    mensaje: 'Hola',
    fechaSolicitud: '2026-10-24T10:00:00Z',
    propietario: { id: 'u1', nombre: 'Ana' },
    solicitante: { id: 'u2', nombre: 'Beto' },
    ofrecidas: [],
    contraofertas: 0,
    contraofertasRestantes: 5,
  } as SolicitudDto;

  function crear(s: Partial<SolicitudDto>) {
    TestBed.configureTestingModule({ imports: [TarjetaSolicitud], providers: [provideRouter([])] });
    const f = TestBed.createComponent(TarjetaSolicitud);
    f.componentRef.setInput('solicitud', { ...base, ...s });
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }
  const botones = (el: HTMLElement) => [...el.querySelectorAll('footer button')].map((b) => b.textContent?.trim());

  it('al dueño con el turno le muestra rechazar, contraofertar y aceptar', () => {
    const el = crear({ soyDuenio: true, meTocaResponder: true });
    expect(botones(el)).toEqual(['Rechazar', 'Contraofertar', 'Aceptar y reservar']);
  });

  it('quien envió una contraoferta solo espera o se retira', () => {
    const el = crear({ soyDuenio: true, meTocaResponder: false, contraofertas: 1, contraofertasRestantes: 4 });
    expect(botones(el)).toEqual(['Rechazar']);
    expect(el.textContent).toContain('Esperando la respuesta de Beto (contraoferta 1 de 5)');
  });

  it('quien pidió acepta la contraoferta del dueño y ve el precio propuesto', () => {
    const el = crear({ modo: 'Compra', soyDuenio: false, meTocaResponder: true, contraofertas: 1, contraofertasRestantes: 4, precioPublicadoCop: 50000, precioPropuestoCop: 45000 });
    expect(botones(el)).toEqual(['Retirar solicitud', 'Contraofertar', 'Aceptar contraoferta']);
    expect(el.textContent).toContain('Ana te hizo una contraoferta');
    expect(el.textContent).toMatch(/45\.000/);
  });

  it('sin contraofertas restantes ni en una donación hay botón de contraofertar', () => {
    expect(botones(crear({ soyDuenio: false, meTocaResponder: true, contraofertas: 5, contraofertasRestantes: 0 }))).not.toContain('Contraofertar');
    TestBed.resetTestingModule();
    expect(botones(crear({ modo: 'Donacion', soyDuenio: true, meTocaResponder: true }))).toEqual(['Rechazar', 'Aceptar y reservar']);
  });

  it('muestra todos los objetos ofrecidos', () => {
    const el = crear({
      soyDuenio: true,
      meTocaResponder: true,
      ofrecidas: [
        { id: 'o1', titulo: 'Patineta', condicion: 'Usado' },
        { id: 'o2', titulo: 'Casco', condicion: 'Nuevo' },
      ],
    });
    expect([...el.querySelectorAll('a[aria-label^="Te ofrece:"]')].map((a) => a.getAttribute('aria-label'))).toEqual(['Te ofrece: Patineta', 'Te ofrece: Casco']);
  });
});
