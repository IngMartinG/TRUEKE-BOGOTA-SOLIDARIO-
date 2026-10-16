import { TestBed } from '@angular/core/testing';
import { PUNTOS_ENCUENTRO, PuntosEncuentro } from './puntos-encuentro';

describe('PuntosEncuentro', () => {
  function crear(variante: 'detalle' | 'chat') {
    const f = TestBed.createComponent(PuntosEncuentro);
    f.componentRef.setInput('variante', variante);
    f.detectChanges();
    return f;
  }

  it('recomienda los cuatro tipos de lugar, sin sedes concretas', () => {
    expect(PUNTOS_ENCUENTRO.map((p) => p.nombre)).toEqual([
      'Estación de TransMilenio',
      'Centro comercial',
      'Biblioteca pública',
      'CAI de la Policía',
    ]);
    // Solo tipos de lugar: ninguna frase nombra un portal, una sede o una dirección.
    for (const p of PUNTOS_ENCUENTRO) {
      expect(p.propuesta).not.toMatch(/portal \p{Lu}|calle|carrera|avenida|#\s?\d/iu);
    }
  });

  it('en el detalle explica cada lugar sin botones', () => {
    const el: HTMLElement = crear('detalle').nativeElement;
    expect(el.querySelectorAll('li')).toHaveLength(4);
    expect(el.querySelector('button')).toBeNull();
    expect(el.textContent).toContain('vigilancia');
  });

  it('en el chat propone el lugar elegido', () => {
    const f = crear('chat');
    const elegidas: string[] = [];
    f.componentInstance.elegir.subscribe((t) => elegidas.push(t));
    const botones = (f.nativeElement as HTMLElement).querySelectorAll('button');
    expect(botones).toHaveLength(4);
    expect(botones[3]!.getAttribute('aria-label')).toBe('Proponer CAI de la Policía');
    botones[2]!.click();
    expect(elegidas).toEqual([PUNTOS_ENCUENTRO[2]!.propuesta]);
  });
});
