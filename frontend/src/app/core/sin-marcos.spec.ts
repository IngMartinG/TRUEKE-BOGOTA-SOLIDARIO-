import { cargadaEnMarcoAjeno, mostrarAvisoDeMarco } from './sin-marcos';

describe('sin marcos (clickjacking)', () => {
  it('detecta si la app está dentro de un iframe', () => {
    const propia = { top: null, self: null } as unknown as Window;
    (propia as { top: unknown }).top = propia;
    (propia as { self: unknown }).self = propia;
    expect(cargadaEnMarcoAjeno(propia)).toBe(false);

    expect(cargadaEnMarcoAjeno({ top: {}, self: {} } as unknown as Window)).toBe(true);

    const ajena = { self: {} } as Window;
    Object.defineProperty(ajena, 'top', { get: () => { throw new Error('SecurityError'); } });
    expect(cargadaEnMarcoAjeno(ajena)).toBe(true);
  });

  it('en un marco ajeno muestra solo un enlace para abrirla aparte', () => {
    document.body.innerHTML = '<app-root>contenido</app-root>';
    mostrarAvisoDeMarco(document);
    const enlace = document.body.querySelector('a');
    expect(document.body.children.length).toBe(1);
    expect(enlace?.target).toBe('_top');
    expect(document.querySelector('app-root')).toBeNull();
  });
});
