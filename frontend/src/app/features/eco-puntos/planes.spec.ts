import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { SesionService } from '../../core/sesion.service';
import Planes from './planes';

const politica = {
  precioPremiumCop: 15000,
  precioEmpresaCop: 50000,
  precioPremiumAnualCop: 150000,
  precioEmpresaAnualCop: 500000,
  duracionSuscripcionDias: 30,
  duracionSuscripcionAnualDias: 365,
};

function montar() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: CuentaApi, useValue: { politica$: of(politica), resumenPuntos: () => of({}) } },
      { provide: AvisosService, useValue: {} },
      { provide: SesionService, useValue: { autenticado: () => false } },
    ],
  });
  const fixture = TestBed.createComponent(Planes);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const texto = () => (el.textContent ?? '').replace(/\s+/g, ' ');
  return { fixture, el, texto };
}

describe('Planes: interruptor Mensual / Anual', () => {
  it('arranca en Mensual con el precio de 30 días y sin el ahorro anual', () => {
    const { el, texto } = montar();
    expect(el.querySelector('[role="radio"][aria-checked="true"]')?.textContent).toContain('Mensual');
    expect(texto()).toContain('15.000');
    expect(texto()).not.toContain('ahorras');
  });

  it('al elegir Anual muestra $150.000 / año y el ahorro de $30.000', () => {
    const { fixture, el, texto } = montar();
    const anual = Array.from(el.querySelectorAll<HTMLButtonElement>('[role="radio"]')).find((b) => b.textContent?.includes('Anual'));
    anual?.click();
    fixture.detectChanges();
    expect(anual?.getAttribute('aria-checked')).toBe('true');
    expect(texto()).toContain('150.000');
    expect(texto()).toContain('/ año');
    expect(texto()).toContain('Equivale a $12.500 al mes · ahorras $30.000');
    expect(texto()).toContain('Equivale a $41.667 al mes · ahorras $100.000');
  });

  it('las flechas cambian de periodo', () => {
    const { fixture, el } = montar();
    const mensual = el.querySelector<HTMLButtonElement>('[role="radio"][aria-checked="true"]');
    mensual?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect(el.querySelector('[role="radio"][aria-checked="true"]')?.textContent).toContain('Anual');
  });
});
