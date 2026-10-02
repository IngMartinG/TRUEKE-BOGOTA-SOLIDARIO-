import { HttpErrorResponse } from '@angular/common/http';
import { erroresDeCampos, mensajeDe, problemaDe } from './problema';

describe('problema', () => {
  it('usa el title del ProblemDetails como mensaje', () => {
    const e = new HttpErrorResponse({ status: 400, error: { title: 'La publicación no está disponible.', codigo: 'x' } });
    expect(mensajeDe(e)).toBe('La publicación no está disponible.');
    expect(problemaDe(e).codigo).toBe('x');
  });

  it('da un mensaje claro sin conexión y por estado cuando no hay cuerpo', () => {
    expect(mensajeDe(new HttpErrorResponse({ status: 0 }))).toContain('No pudimos conectar');
    expect(mensajeDe(new HttpErrorResponse({ status: 429 }))).toContain('Espera');
    expect(mensajeDe(new HttpErrorResponse({ status: 503 }))).toContain('servidor');
  });

  it('convierte los errores de validación a nombres de campo en camelCase', () => {
    const e = new HttpErrorResponse({ status: 400, error: { errors: { Titulo: ['Muy corto'], '$.precioReferenciaCop': ['Inválido'] } } });
    expect(erroresDeCampos(e)).toEqual({ titulo: 'Muy corto', precioReferenciaCop: 'Inválido' });
  });
});
