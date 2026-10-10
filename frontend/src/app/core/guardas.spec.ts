import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { exigirCorreoVerificado, exigirCuentaCompleta, exigirModerador } from './guardas';
import { SesionService } from './sesion.service';

function ejecutar(
  guarda: typeof exigirCorreoVerificado,
  sesion: { autenticado: boolean; verificado: boolean; moderador?: boolean; foto?: boolean },
) {
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
        },
      },
    ],
  });
  const resultado = TestBed.runInInjectionContext(() =>
    guarda({} as ActivatedRouteSnapshot, { url: '/cuenta/perfil' } as RouterStateSnapshot),
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
  it('sin foto de perfil lleva a subirla', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: true, foto: false })).toBe('/cuenta/perfil#foto');
  });

  it('primero exige el correo verificado', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: false, foto: false })).toBe('/verifica-tu-correo');
  });

  it('con correo verificado y foto deja pasar', () => {
    expect(ejecutar(exigirCuentaCompleta, { autenticado: true, verificado: true, foto: true })).toBe(true);
  });
});
