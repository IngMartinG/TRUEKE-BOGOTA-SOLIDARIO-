import { esFalloDeCarga } from './navegacion';

describe('Fallo al cargar un fragmento de la app', () => {
  it('reconoce el mensaje de cada navegador', () => {
    expect(esFalloDeCarga(new TypeError('Failed to fetch dynamically imported module: https://x/chunk-A1.js'))).toBe(true); // Chrome
    expect(esFalloDeCarga(new TypeError('error loading dynamically imported module: https://x/chunk-A1.js'))).toBe(true); // Firefox
    expect(esFalloDeCarga(new TypeError('Importing a module script failed.'))).toBe(true); // Safari
  });

  it('no confunde otros errores con un fallo de carga', () => {
    expect(esFalloDeCarga(new Error('No se pudo resolver la ruta'))).toBe(false);
    expect(esFalloDeCarga(new TypeError("Cannot read properties of undefined (reading 'id')"))).toBe(false);
    expect(esFalloDeCarga(null)).toBe(false);
  });
});
