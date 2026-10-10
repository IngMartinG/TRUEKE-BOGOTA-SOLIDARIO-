import { urlRetornoPago } from './wompi';

describe('urlRetornoPago (regreso desde Wompi)', () => {
  it('respeta la subcarpeta del <base href> (GitHub Pages)', () => {
    expect(urlRetornoPago('TRK-abc', 'https://ingmarting.github.io/TRUEKE-BOGOTA-SOLIDARIO-/'))
      .toBe('https://ingmarting.github.io/TRUEKE-BOGOTA-SOLIDARIO-/pagos/TRK-abc');
  });

  it('funciona en la raíz del dominio (Docker / dominio propio)', () => {
    expect(urlRetornoPago('TRK-abc', 'https://trueke.co/')).toBe('https://trueke.co/pagos/TRK-abc');
  });

  it('codifica la referencia para que no altere la ruta', () => {
    expect(urlRetornoPago('a/b?c', 'https://trueke.co/')).toBe('https://trueke.co/pagos/a%2Fb%3Fc');
  });
});
