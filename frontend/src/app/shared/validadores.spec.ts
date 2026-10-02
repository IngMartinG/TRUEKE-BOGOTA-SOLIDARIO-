import { FormControl, FormGroup } from '@angular/forms';
import { destinoSeguro } from '../features/auth/redireccion';
import { cop, iniciales } from './pipes';
import { claveSegura, coinciden } from './validadores';

describe('claveSegura (misma regla que el backend)', () => {
  const valida = (v: string) => claveSegura(new FormControl(v)) === null;
  it('exige 8+ caracteres con mayúscula, minúscula y número', () => {
    expect(valida('Prueba2026')).toBe(true);
    expect(valida('prueba2026')).toBe(false);
    expect(valida('PRUEBA2026')).toBe(false);
    expect(valida('PruebaSin')).toBe(false);
    expect(valida('Pr1')).toBe(false);
  });
});

describe('coinciden', () => {
  it('marca la confirmación cuando no coincide', () => {
    const g = new FormGroup({ a: new FormControl('Clave1234'), b: new FormControl('Otra1234') }, { validators: coinciden('a', 'b') });
    expect(g.controls.b.hasError('noCoincide')).toBe(true);
    g.controls.b.setValue('Clave1234');
    expect(g.controls.b.hasError('noCoincide')).toBe(false);
  });
});

describe('destinoSeguro (evita redirecciones abiertas)', () => {
  it('acepta solo rutas internas', () => {
    expect(destinoSeguro('/publicacion/123')).toBe('/publicacion/123');
    expect(destinoSeguro('https://malicioso.com')).toBe('/explorar');
    expect(destinoSeguro('//malicioso.com')).toBe('/explorar');
    expect(destinoSeguro('/\\malicioso.com')).toBe('/explorar');
    expect(destinoSeguro('/ingresar')).toBe('/explorar');
    expect(destinoSeguro(undefined)).toBe('/explorar');
  });
});

describe('formatos', () => {
  it('formatea pesos colombianos sin decimales', () => {
    expect(cop(25000).replace(/\s/g, ' ')).toMatch(/\$\s?25\.000/);
  });
  it('calcula iniciales', () => {
    expect(iniciales('María Fernanda Ruiz')).toBe('MF');
    expect(iniciales('')).toBe('?');
  });
});
