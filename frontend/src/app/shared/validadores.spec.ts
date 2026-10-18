import { FormControl, FormGroup } from '@angular/forms';
import { destinoSeguro } from '../features/auth/redireccion';
import { cop, iniciales } from './pipes';
import { LOCALIDADES } from '../api/tipos';
import {
  claveSegura,
  coinciden,
  digitoVerificacionNit,
  direccion,
  documentoSegunTipo,
  hoyEnColombia,
  localidad,
  mayorDeEdad,
  nit,
  nombreComercial,
  nombreCompleto,
  nombrePersona,
  restarAnios,
} from './validadores';

const cumple = (v: typeof nombrePersona, valor: string) => v(new FormControl(valor)) === null;

describe('formatos de datos (mismas reglas que FormatosTexto.cs)', () => {
  it('nombre completo: letras con tildes, apóstrofo, guion y punto, con nombre y apellido', () => {
    for (const ok of ["María José O'Neill", 'Ana Pérez', 'Jean-Paul Muñoz', 'J. R. Güiza', 'José O’Brien', '  Ana   Pérez '])
      expect(cumple(nombreCompleto, ok), ok).toBe(true);
    for (const mal of ['Ana2 Pérez', '123', 'Ana', 'Ana_Pérez', 'Ana -- Pérez', 'Ana Pérez 😀', "'Ana Pérez"])
      expect(cumple(nombreCompleto, mal), mal).toBe(false);
  });

  it('nombre de persona admite una palabra pero no dígitos', () => {
    expect(cumple(nombrePersona, 'Ana')).toBe(true);
    expect(cumple(nombrePersona, 'Ana2')).toBe(false);
  });

  it('nombre comercial admite dígitos y & . , - \' ( )', () => {
    for (const ok of ['3M', 'Muebles La 80', 'Pérez & Hijos S.A.S.', 'Tienda (Centro), Ltda.']) expect(cumple(nombreComercial, ok)).toBe(true);
    for (const mal of ['Tienda <script>', 'Tienda_Bogotá', '&&&']) expect(cumple(nombreComercial, mal)).toBe(false);
  });

  it('localidad y dirección exigen al menos una letra', () => {
    for (const l of LOCALIDADES) expect(cumple(localidad, l), l).toBe(true);
    expect(cumple(localidad, '20 de Julio')).toBe(true);
    expect(cumple(localidad, '123')).toBe(false);
    expect(cumple(localidad, 'Kennedy!')).toBe(false);
    expect(cumple(direccion, 'Cra. 7 N° 45-10, apto 301')).toBe(true);
    expect(cumple(direccion, 'Km 5 vía La Calera 2/3')).toBe(true);
    expect(cumple(direccion, '12345')).toBe(false);
    expect(cumple(direccion, 'Calle 10 <b>')).toBe(false);
  });

  it('NIT: dígitos y puntos con DV módulo 11 correcto', () => {
    expect(digitoVerificacionNit('900123456')).toBe(8);
    expect(nit(new FormControl('900123456'))).toBeNull();
    expect(nit(new FormControl('900.123.456-8'))).toBeNull();
    expect(nit(new FormControl('900.123.456-7'))).toEqual({ nitDv: true });
    expect(nit(new FormControl('900A23456'))).toEqual({ nit: true });
    expect(nit(new FormControl('12345'))).toEqual({ nit: true });
  });

  it('documento y nombre según el tipo de documento', () => {
    const g = new FormGroup(
      { tipo: new FormControl('CC'), documento: new FormControl('12A4567'), nombre: new FormControl('Ana Pérez') },
      { validators: documentoSegunTipo('tipo', 'documento', 'nombre') },
    );
    expect(g.controls.documento.hasError('cedula')).toBe(true);
    g.controls.documento.setValue('1.234.567');
    expect(g.controls.documento.valid).toBe(true);
    g.controls.tipo.setValue('Pasaporte');
    g.controls.documento.setValue('AB-123');
    expect(g.controls.documento.hasError('documentoAlfanumerico')).toBe(true);
    g.controls.tipo.setValue('NIT');
    g.controls.documento.setValue('900123456');
    g.controls.nombre.setValue('3M Colombia S.A.');
    expect(g.controls.documento.valid).toBe(true);
    expect(g.controls.nombre.valid).toBe(true);
    g.controls.tipo.setValue('CC');
    expect(g.controls.nombre.hasError('nombrePersona')).toBe(true);
  });
});

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
