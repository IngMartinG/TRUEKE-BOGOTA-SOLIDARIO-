import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ConfigService } from '../../core/config.service';
import Apoya from './apoya';

function montar(url: string | null) {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: ConfigService, useValue: { donacionUrl: () => url } }],
  });
  const fixture = TestBed.createComponent(Apoya);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Apoya el proyecto', () => {
  it('con enlace configurado abre el aporte en otra pestaña y aclara que no da beneficios', () => {
    const el = montar('https://pagos.ejemplo.co/aporte');
    const enlace = el.querySelector<HTMLAnchorElement>('a[target="_blank"]');
    expect(enlace?.href).toBe('https://pagos.ejemplo.co/aporte');
    expect(enlace?.rel).toContain('noopener');
    expect(el.textContent).toContain('no da Eco-Puntos');
    expect(el.textContent).toContain('no se puede descontar de impuestos');
  });

  it('sin enlace dice que por ahora no se reciben aportes', () => {
    const el = montar(null);
    expect(el.querySelector('a[target="_blank"]')).toBeNull();
    expect(el.textContent).toContain('no estamos recibiendo aportes');
  });
});
