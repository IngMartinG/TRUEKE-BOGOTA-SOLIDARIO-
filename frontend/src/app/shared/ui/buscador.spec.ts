import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { Buscador } from './buscador';

describe('Buscador', () => {
  const pedidos: string[] = [];

  async function crear() {
    pedidos.length = 0;
    TestBed.configureTestingModule({
      imports: [Buscador],
      providers: [
        {
          provide: CatalogoApi,
          useValue: {
            sugerencias: (texto: string) => {
              pedidos.push(texto);
              return of(['Bicicleta de ruta', 'Bicicleta infantil']);
            },
          },
        },
      ],
    });
    const f = TestBed.createComponent(Buscador);
    const enviados: string[] = [];
    f.componentInstance.buscar.subscribe((t) => enviados.push(t));
    f.detectChanges();
    const input = f.nativeElement.querySelector('input') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    return { f, input, enviados };
  }

  async function escribir(f: Awaited<ReturnType<typeof crear>>['f'], input: HTMLInputElement, texto: string) {
    input.value = texto;
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    await new Promise((ok) => setTimeout(ok, 300)); // espera del debounce
    f.detectChanges();
  }

  it('pide sugerencias desde 2 letras y elige una con el teclado', async () => {
    const { f, input, enviados } = await crear();
    await escribir(f, input, 'b');
    expect(pedidos).toEqual([]);

    await escribir(f, input, 'bici');
    expect(pedidos).toEqual(['bici']);
    const opciones = f.nativeElement.querySelectorAll('[role=option]');
    expect(opciones.length).toBe(2);
    expect(input.getAttribute('aria-expanded')).toBe('true');

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    f.detectChanges();
    expect(input.getAttribute('aria-activedescendant')).toMatch(/-1$/);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    f.detectChanges();

    expect(enviados).toEqual(['Bicicleta infantil']);
    expect(f.nativeElement.querySelector('[role=listbox]')).toBeNull();
  });

  it('Escape cierra la lista sin buscar', async () => {
    const { f, input, enviados } = await crear();
    await escribir(f, input, 'bici');
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    f.detectChanges();
    expect(f.nativeElement.querySelector('[role=listbox]')).toBeNull();
    expect(enviados).toEqual([]);
  });
});
