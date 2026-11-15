import { parametrosDe } from './busquedas';

describe('parametrosDe', () => {
  it('convierte la búsqueda guardada en los filtros del catálogo, sin vacíos', () => {
    expect(
      parametrosDe({
        id: '1', nombre: 'Bicicleta · Trueke', texto: 'bicicleta', categoriaId: 5, modo: 'Trueke', condicion: null,
        departamentoCodigo: '11', municipioCodigo: '11001', localidad: '', precioMin: null, precioMax: 200000,
        soloVerificados: true, alertasActivas: true, fechaUtc: '2026-10-11T00:00:00Z',
      }),
    ).toEqual({
      texto: 'bicicleta', categoriaId: '5', modo: 'Trueke', departamentoCodigo: '11', municipioCodigo: '11001',
      precioMax: '200000', soloVerificados: 'true',
    });
  });

  it('conserva el origen (segunda mano o producto nuevo) al abrir la búsqueda', () => {
    const base = { id: '1', nombre: 'Zapatos', texto: 'zapatos', alertasActivas: true, soloVerificados: false, fechaUtc: '2026-11-15T00:00:00Z' };
    expect(parametrosDe({ ...base, origen: 'MercanciaNueva' })).toEqual({ texto: 'zapatos', origen: 'MercanciaNueva' });
    expect(parametrosDe({ ...base, origen: null })).toEqual({ texto: 'zapatos' });
  });
});
