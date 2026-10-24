import { TestBed } from '@angular/core/testing';
import type { PublicacionDto } from '../../api/tipos';
import { MAX_OBJETOS_OFRECIDOS, SelectorObjetos } from './selector-objetos';

describe('SelectorObjetos', () => {
  const objetos = ['a', 'b', 'c', 'd'].map((id) => ({ id, titulo: `Objeto ${id}`, modo: 'Trueke', condicion: 'Usado' }) as PublicacionDto);

  function crear() {
    const f = TestBed.createComponent(SelectorObjetos);
    f.componentRef.setInput('objetos', objetos);
    f.detectChanges();
    const casillas = () => [...(f.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type=checkbox]')];
    const marcar = (i: number) => {
      casillas()[i]!.dispatchEvent(new Event('change'));
      f.detectChanges();
    };
    return { f, casillas, marcar };
  }

  it(`elige hasta ${MAX_OBJETOS_OFRECIDOS} en orden y desactiva el resto`, () => {
    const { f, casillas, marcar } = crear();
    marcar(2);
    marcar(0);
    marcar(1);
    expect(f.componentInstance.seleccion()).toEqual(['c', 'a', 'b']);
    expect(casillas()[3]!.disabled).toBe(true);
    expect((f.nativeElement as HTMLElement).textContent).toContain('3 de 3 elegidos');

    marcar(0); // quitar uno libera el cupo
    expect(f.componentInstance.seleccion()).toEqual(['c', 'b']);
    expect(casillas()[3]!.disabled).toBe(false);
  });
});
