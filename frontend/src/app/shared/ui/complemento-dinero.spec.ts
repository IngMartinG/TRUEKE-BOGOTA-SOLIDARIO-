import { cuerpoComplemento } from './complemento-dinero';

describe('cuerpoComplemento', () => {
  it('sin monto no envía nada', () => {
    expect(cuerpoComplemento(null, true, false)).toEqual({});
    expect(cuerpoComplemento(0, true, false)).toEqual({});
  });

  it('el sentido se guarda respecto de quien solicita', () => {
    // Quien solicita: "lo pago yo" = lo paga quien solicita
    expect(cuerpoComplemento(50000, true, false)).toEqual({ complementoCop: 50000, complementoLoPagaSolicitante: true });
    expect(cuerpoComplemento(50000, false, false)).toEqual({ complementoCop: 50000, complementoLoPagaSolicitante: false });
    // El dueño: "lo pago yo" = no lo paga quien solicita
    expect(cuerpoComplemento(20000, true, true)).toEqual({ complementoCop: 20000, complementoLoPagaSolicitante: false });
    expect(cuerpoComplemento(20000, false, true)).toEqual({ complementoCop: 20000, complementoLoPagaSolicitante: true });
  });

  it('redondea a pesos enteros', () => {
    expect(cuerpoComplemento(1500.6, true, false).complementoCop).toBe(1501);
  });
});
