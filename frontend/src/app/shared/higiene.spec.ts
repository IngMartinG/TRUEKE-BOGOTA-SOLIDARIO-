import { esArticuloDeHigiene } from './higiene';

describe('esArticuloDeHigiene', () => {
  it.each([
    'Ropa interior de algodón',
    'CALZÓN talla M',
    'Set de medias deportivas',
    'Traje de baño de dos piezas',
    'Bóxer para hombre',
    'Calcetines de lana',
  ])('detecta «%s»', (titulo) => expect(esArticuloDeHigiene(titulo)).toBe(true));

  it.each(['Zapatos de cuero', 'Calzado deportivo', 'Media hora de clase de guitarra', 'Interiores de un carro', '', null])(
    'no confunde «%s»',
    (titulo) => expect(esArticuloDeHigiene(titulo)).toBe(false),
  );
});
