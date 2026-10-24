import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { exigirCorreoVerificado, exigirCuentaCompleta, exigirMayoriaEdad, exigirModerador } from './guardas';
import { SesionService } from './sesion.service';

function ejecutar(
  guarda: typeof exigirCorreoVerificado,
  sesion: { autenticado: boolean; verificado: boolean; moderador?: boolean; foto?: boolean; mayor?: boolean; sinNombre?: boolean; sinLocalidad?: boolean; legalViejo?: boolean },
  url = '/cuenta/perfil',
) {
  TestBed.resetTestingModule(); // varias llamadas en la misma prueba
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      {
        provide: SesionService,
        useValue: {
          autenticado: () => sesion.autenticado,
          correoVerificado: () => sesion.verificado,
          esModerador: () => sesion.moderador ?? false,
          tieneFoto: () => sesion.foto ?? true,
          nombreRealPendiente: () => sesion.sinNombre ?? false,
          localidadPendiente: () => sesion.sinLocalidad ?? false,
          usuario: () => (sesion.autenticado ? {} : null),
          mayoriaEdadConfirmada: () => sesion.mayor ?? true,
          legalPendiente: () => sesion.legalViejo ?? false,
        },
      },
    ],
  });
  const resultado = TestBed.runInInjectionContext(() =>
    guarda({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  );
  return resultado instanceof UrlTree ? TestBed.inject(Router).serializeUrl(resultado) : resultado;
}

describe('exigirCorreoVerificado (sin verificar solo se puede mirar el catálogo)', () => {
  it('sin sesión lleva a ingresar y vuelve luego a la página pedida', () => {
    expect(ejecutar(exigirCorreoVerificado, { autenticado: false, verificado: false })).toBe('/ingresar?volver=%2Fcuenta%2Fperfil');
  });

  it('con sesión pero sin verificar lleva a confirmar el correo', () => {
    expect(ejecutar(exigirCorreoVerificado, { autenticado: true, verificado: false })).toBe('/verifica-tu-correo');
  });

  it('con el correo verificado deja pasar', () => {
    expect(ejecutar(exigirCorreoVerificado, { autenticado: true, verificado: true })).toBe(true);
  });
});

describe('exigirModerador', () => {
  it('un moderador sin correo verificado también debe confirmarlo primero', () => {
    expect(ejecutar(exigirModerador, { autenticado: true, verificado: false, moderador: true })).toBe('/verifica-tu-correo');
  });
});

describe('exigirCuentaCompleta (publicar y editar)', () => {
  it('sin localidad (cuenta de Google) lleva a escribirla y luego vuelve', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: true, sinLocalidad: true }, '/publicar')).toBe(
      '/cuenta/perfil?volver=%2Fpublicar#datos',
    );
  });

  it('sin nombre y apellido reales lleva a escribirlos y luego vuelve', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: true, sinNombre: true }, '/publicar')).toBe(
      '/cuenta/perfil?volver=%2Fpublicar#datos',
    );
  });

  it('sin foto de perfil lleva a subirla', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: true, foto: false })).toBe('/cuenta/perfil?volver=%2Fcuenta%2Fperfil#foto');
  });

  it('primero exige el correo verificado', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: false, foto: false })).toBe('/verifica-tu-correo');
  });

  it('con correo verificado y foto deja pasar', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: true, foto: true })).toBe(true);
  });
});

describe('exigirMayoriaEdad (cuentas sin fecha de nacimiento)', () => {
  it('lleva a completar el perfil y vuelve luego a la página pedida', () => {
    expect(ejecutar(exigirMayoriaEdad, { autenticado: true, verificado: true, mayor: false }, '/publicar')).toBe(
      '/completar-perfil?volver=%2Fpublicar',
    );
  });

  it('deja abrir lo legal, la ayuda, la privacidad de la cuenta y la propia pantalla', () => {
    for (const url of ['/completar-perfil?volver=%2F', '/privacidad', '/terminos', '/ayuda', '/cuenta/privacidad', '/cuenta/soporte'])
      expect(ejecutar(exigirMayoriaEdad, { autenticado: true, verificado: true, mayor: false }, url)).toBe(true);
  });

  it('con una versión vieja de la política o los términos también lleva a completar el perfil', () => {
    expect(ejecutar(exigirMayoriaEdad, { autenticado: true, verificado: true, mayor: true, legalViejo: true }, '/mensajes')).toBe(
      '/completar-perfil?volver=%2Fmensajes',
    );
  });

  it('sin sesión o con la fecha registrada no hace nada', () => {
    expect(ejecutar(exigirMayoriaEdad, { autenticado: false, verificado: false, mayor: false }, '/publicar')).toBe(true);
    expect(ejecutar(exigirMayoriaEdad, { autenticado: true, verificado: true, mayor: true }, '/publicar')).toBe(true);
  });
});
