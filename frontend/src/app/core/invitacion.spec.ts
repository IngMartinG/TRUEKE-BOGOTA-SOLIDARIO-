import { CLAVE_INVITA, codigoInvitacionGuardado, esCodigoInvitacion, guardarCodigoInvitacion, olvidarCodigoInvitacion } from './invitacion';

describe('Código de invitación', () => {
  beforeEach(() => localStorage.removeItem(CLAVE_INVITA));

  it('solo acepta de 6 a 12 letras o números', () => {
    expect(esCodigoInvitacion('ABC123')).toBe(true);
    expect(esCodigoInvitacion('abcdefghij12')).toBe(true);
    expect(esCodigoInvitacion('ABC12')).toBe(false);
    expect(esCodigoInvitacion('ABCDEFGHIJ123')).toBe(false);
    expect(esCodigoInvitacion('ABC-123')).toBe(false);
    expect(esCodigoInvitacion(undefined)).toBe(false);
  });

  it('guarda el código válido, lo devuelve y lo olvida', () => {
    expect(guardarCodigoInvitacion('Ab12Cd34')).toBe('Ab12Cd34');
    expect(localStorage.getItem(CLAVE_INVITA)).toBe('Ab12Cd34');
    expect(codigoInvitacionGuardado()).toBe('Ab12Cd34');
    olvidarCodigoInvitacion();
    expect(codigoInvitacionGuardado()).toBeNull();
  });

  it('ignora un código inválido y conserva el que ya había', () => {
    guardarCodigoInvitacion('Ab12Cd34');
    expect(guardarCodigoInvitacion('<script>')).toBe('Ab12Cd34');
    expect(localStorage.getItem(CLAVE_INVITA)).toBe('Ab12Cd34');
  });
});
