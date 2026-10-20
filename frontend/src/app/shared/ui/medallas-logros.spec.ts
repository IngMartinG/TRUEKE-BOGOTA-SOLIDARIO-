import { TestBed } from '@angular/core/testing';
import type { LogroDto } from '../../api/tipos';
import { MedallasLogros } from './medallas-logros';

describe('MedallasLogros', () => {
  const logros: LogroDto[] = [
    { clave: 'primer-intercambio', nombre: 'Primer intercambio', descripcion: 'Completó su primer intercambio.', grado: null, obtenido: true, progreso: 1, meta: 1 },
    { clave: 'corazon-solidario', nombre: 'Corazón solidario', descripcion: 'Donaciones completadas.', grado: 'Plata', obtenido: true, progreso: 12, meta: 30 },
    { clave: 'truekero', nombre: 'Truekero', descripcion: 'Truekes completados.', grado: null, obtenido: false, progreso: 1, meta: 3 },
  ];

  function crear(progreso: boolean) {
    const f = TestBed.createComponent(MedallasLogros);
    f.componentRef.setInput('logros', logros);
    f.componentRef.setInput('progreso', progreso);
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('en el perfil público solo muestra los obtenidos, con su grado en texto', () => {
    const el = crear(false);
    expect(el.querySelectorAll('li').length).toBe(2);
    expect(el.textContent).toContain('Corazón solidario de plata');
    expect(el.textContent).not.toContain('Truekero');
    expect(el.querySelector('[role="progressbar"]')).toBeNull();
  });

  it('en tus logros muestra también lo que falta, con avance accesible', () => {
    const el = crear(true);
    expect(el.querySelectorAll('li').length).toBe(3);
    expect(el.querySelector('li')?.textContent).toContain('Primer intercambio'); // lo obtenido va primero
    const barra = el.querySelector('[aria-label="Avance en Truekero"]');
    expect(barra?.getAttribute('aria-valuenow')).toBe('1');
    expect(barra?.getAttribute('aria-valuemax')).toBe('3');
    expect(el.textContent).toContain('12 de 30 para oro');
  });
});
