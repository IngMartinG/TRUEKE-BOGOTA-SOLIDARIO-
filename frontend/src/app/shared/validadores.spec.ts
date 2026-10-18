import { FormControl, FormGroup } from '@angular/forms';
import { destinoSeguro } from '../features/auth/redireccion';
import { cop, iniciales } from './pipes';
import { claveSegura, coinciden, hoyEnColombia, mayorDeEdad, restarAnios } from './validadores';

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

describe('mayorDeEdad (misma regla que el backend: solo mayores de 18)', () => {
  const control = (v: string) => new FormControl(v, mayorDeEdad);
  const hoy = hoyEnColombia();

  it('quien cumple 18 hoy ya puede; quien los cumple mañana no', () => {
    expect(control(restarAnios(hoy, 18)).valid).toBe(true);
    const manana = new Date(Date.parse(`${restarAnios(hoy, 18)}T00:00:00Z`) + 86_400_000).toISOString().slice(0, 10);
    expect(control(manana).hasError('menorDeEdad')).toBe(true);
  });

  it('rechaza fechas futuras, imposibles o inexistentes', () => {
    expect(control('2999-01-01').hasError('fechaInvalida')).toBe(true);
    expect(control('1800-01-01').hasError('fechaInvalida')).toBe(true);
    expect(control('1990-02-31').hasError('fechaInvalida')).toBe(true);
    expect(control('1990-13-01').hasError('fechaInvalida')).toBe(true);
    expect(control('').valid).toBe(true); // lo obligatorio lo decide Validators.required
  });

  it('el 29 de febrero se resta como en el backend', () => {
    expect(restarAnios('2026-02-28', 18)).toBe('2008-02-28');
    expect(restarAnios('2024-02-29', 1)).toBe('2023-02-28');
  });

  it('hoy en Colombia es UTC-5', () => {
    expect(hoyEnColombia(Date.parse('2026-10-18T03:00:00Z'))).toBe('2026-10-17');
  });
});
