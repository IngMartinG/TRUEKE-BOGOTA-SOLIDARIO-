import { inject, Injectable } from '@angular/core';
import type {
  ActivacionDosFactoresDto,
  CambiarClaveRequest,
  CodigosRecuperacionDto,
  ConfiguracionDosFactoresDto,
  GoogleLoginRequest,
  LoginRequest,
  RegistroRequest,
  SesionDto,
} from '../../api/tipos';
import { HttpApi } from './http-api';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly api = inject(HttpApi);

  registrar = (r: RegistroRequest) => this.api.post<SesionDto>('/auth/registrar', r, { silencioso: true });
  login = (r: LoginRequest) => this.api.post<SesionDto>('/auth/login', r, { silencioso: true });
  google = (r: GoogleLoginRequest) => this.api.post<SesionDto>('/auth/google', r, { silencioso: true });
  refrescar = () => this.api.post<SesionDto>('/auth/refrescar', {}, { silencioso: true });
  salir = () => this.api.post<void>('/auth/salir', {}, { silencioso: true });
  verificarCorreo = (token: string) =>
    this.api.post<void>('/auth/verificar-correo', { token }, { silencioso: true });
  reenviarVerificacion = () => this.api.post<void>('/auth/reenviar-verificacion');
  olvideClave = (correo: string, captchaToken: string | null) =>
    this.api.post<void>('/auth/olvide-clave', { correo, captchaToken }, { silencioso: true });
  restablecerClave = (token: string, claveNueva: string) =>
    this.api.post<void>('/auth/restablecer-clave', { token, claveNueva }, { silencioso: true });
  cambiarClave = (r: CambiarClaveRequest) =>
    this.api.post<SesionDto>('/auth/cambiar-clave', r, { silencioso: true });
  cerrarSesiones = () => this.api.post<void>('/auth/cerrar-sesiones');

  configurar2fa = () => this.api.post<ConfiguracionDosFactoresDto>('/auth/2fa/configurar');
  activar2fa = (codigo: string) =>
    this.api.post<ActivacionDosFactoresDto>('/auth/2fa/activar', { codigo }, { silencioso: true });
  desactivar2fa = (codigo: string) =>
    this.api.post<SesionDto>('/auth/2fa/desactivar', { codigo }, { silencioso: true });
  regenerarCodigos = (codigo: string) =>
    this.api.post<CodigosRecuperacionDto>('/auth/2fa/codigos-recuperacion', { codigo }, { silencioso: true });
}
