import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { Invitar } from './invitar';

const invitacion = { codigo: 'ABCD1234', logradas: 3, recompensadasEnCiclo: 2, maxPorCiclo: 10, puntosPorInvitacion: 20, diasCiclo: 30 };

async function montar(inv = invitacion) {
  const avisos = { exito: vi.fn(), info: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: CuentaApi, useValue: { politica$: of({}), invitacion: () => of(inv) } },
      { provide: AvisosService, useValue: avisos },
    ],
  });
  const fixture = TestBed.createComponent(Invitar);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, avisos, texto: () => (el.textContent ?? '').replace(/\s+/g, ' ') };
}

describe('Invita y ganan los dos', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('muestra el enlace con el código y el contador', async () => {
    const { el, texto } = await montar();
    const campo = el.querySelector<HTMLInputElement>('#enlace-invitacion');
    expect(campo?.value).toBe(`${location.origin}/registro?invita=ABCD1234`);
    expect(campo?.readOnly).toBe(true);
    expect(texto()).toContain('Invita y ganan los dos');
    expect(texto()).toContain('Invitaciones logradas: 3');
    expect(texto()).not.toContain('Llegaste al tope');
  });

  it('sin navigator.share, «Copiar enlace» usa el portapapeles y avisa', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    const { fixture, el, avisos } = await montar();
    const boton = Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes('Copiar enlace'));
    boton?.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(writeText).toHaveBeenCalledWith(`${location.origin}/registro?invita=ABCD1234`);
    expect(avisos.exito).toHaveBeenCalled();
    expect(el.querySelector('[aria-live="polite"]')?.textContent).toContain('¡Enlace copiado!');
  });

  it('al llegar al tope lo explica con calma', async () => {
    const { texto } = await montar({ ...invitacion, recompensadasEnCiclo: 10 });
    expect(texto()).toContain('Llegaste al tope de 10 en 30 días');
  });
});
