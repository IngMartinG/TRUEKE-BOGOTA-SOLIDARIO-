import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { SesionService } from '../../core/sesion.service';
import { SubidasService } from '../../core/subidas.service';
import EcoPuntos from './eco-puntos';

const politica = {
  copPorEcoPunto: 100,
  recargaMinimaCop: 5000,
  recargaMaximaCop: 500000,
  paquetesRecarga: [
    { montoCop: 10000, puntos: 100, bono: 10 },
    { montoCop: 20000, puntos: 200, bono: 30 },
    { montoCop: 50000, puntos: 500, bono: 100 },
  ],
  maxTransaccionesConPuntosPorDia: 5,
  puntosPrimeraPublicacion: 20,
  horasPrimeraPublicacionActiva: 24,
  puntosPerfilCompleto: 30,
  puntosCalificacionCincoEstrellas: 15,
  puntosCalificacionCuatroEstrellas: 8,
  diasEntreCalificacionesMismaPareja: 30,
  puntosDenunciaConfirmada: 20,
  maxDenunciasRecompensadasPorCiclo: 3,
  diasCicloDenuncias: 30,
};

function montar() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: CuentaApi, useValue: { politica$: of(politica), resumenPuntos: () => of({}) } },
      { provide: CatalogoApi, useValue: {} },
      { provide: SubidasService, useValue: {} },
      { provide: AvisosService, useValue: {} },
      { provide: ConfigService, useValue: { pagosDePrueba: () => false, subidasHabilitadas: () => false } },
      { provide: SesionService, useValue: { autenticado: () => false, usuario: () => null, correoVerificado: () => false } },
    ],
  });
  const fixture = TestBed.createComponent(EcoPuntos);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Recarga de Eco-Puntos con paquetes', () => {
  it('muestra los tres paquetes con su bono y deja elegido el de $20.000 como el más elegido', () => {
    const el = montar();
    const grupo = el.querySelector('[role="radiogroup"][aria-label="Paquete de recarga"]');
    const radios = Array.from(grupo?.querySelectorAll('[role="radio"]') ?? []);
    expect(radios.length).toBe(3);
    expect(radios[0].textContent).toContain('100');
    expect(radios[0].textContent).toContain('10 de bono');
    expect(radios[1].getAttribute('aria-checked')).toBe('true');
    expect(radios[1].textContent).toContain('Más elegido');
    expect(radios[1].textContent).toContain('30 de bono');
    expect(radios[0].getAttribute('aria-checked')).toBe('false');
  });

  it('el campo de otro monto avisa que no da bono', () => {
    const el = montar();
    expect(el.querySelector('#monto')).not.toBeNull();
    expect((el.querySelector('#titulo-recarga')?.closest('section')?.textContent ?? '')).toContain('Sin bono');
  });
});

describe('Cómo ganar puntos', () => {
  it('muestra las demás formas de ganar con los valores de la política en vivo', () => {
    const el = montar();
    const seccion = el.querySelector('#titulo-ganar')?.closest('section')?.textContent ?? '';
    expect(seccion).toContain('Más formas de ganar');
    expect(seccion).toContain('Perfil completo');
    expect(seccion).toContain('+30');
    expect(seccion).toContain('Tu primera publicación');
    expect(seccion).toContain('24 h');
    expect(seccion).toContain('+15 / +8');
    expect(seccion).toContain('Denuncia confirmada');
    expect(seccion).toContain('hasta 3 cada 30 días');
  });
});
