import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { exigirCorreoVerificado, exigirModerador } from './guardas';
import { SesionService } from './sesion.service';

function ejecutar(guarda: typeof exigirCorreoVerificado, sesion: { autenticado: boolean; verificado: boolean; moderador?: boolean }) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      {
        provide: SesionService,
        useValue: {
          autenticado: () => sesion.autenticado,
          correoVerificado: () => sesion.verificado,
          esModerador: () => sesion.moderador ?? false,
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
