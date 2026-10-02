import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, firstValueFrom, map, Observable, of, shareReplay, tap } from 'rxjs';
import type { SesionDto, UsuarioDto } from '../api/tipos';
import { AuthApi } from './api/auth.api';
import { CuentaApi } from './api/cuenta.api';

/**
 * Estado de la sesión. El token de acceso vive SOLO en memoria (nunca en localStorage):
 * al recargar la página se recupera con el refresco, que viaja en una cookie HttpOnly.
 */
@Injectable({ providedIn: 'root' })
export class SesionService {
  private readonly authApi = inject(AuthApi);
  private readonly cuentaApi = inject(CuentaApi);
  private readonly router = inject(Router);

  private readonly _token = signal<string | null>(null);
  private readonly _usuario = signal<UsuarioDto | null>(null);
  private readonly _expira = signal<number>(0);
  private refrescoEnCurso: Observable<string | null> | null = null;
  private temporizador: ReturnType<typeof setTimeout> | undefined;

  readonly token = this._token.asReadonly();
  readonly usuario = this._usuario.asReadonly();
  readonly autenticado = computed(() => this._token() !== null);
  readonly esModerador = computed(() => {
    const rol = this._usuario()?.rol;
    return rol === 'Administrador' || rol === 'SuperUsuario';
  });
  readonly esSuperUsuario = computed(() => this._usuario()?.rol === 'SuperUsuario');
  readonly correoVerificado = computed(() => this._usuario()?.correoVerificado === true);
  readonly primerNombre = computed(() => this._usuario()?.nombreCompleto?.split(' ')[0] ?? '');

  /** Al arrancar la app: intenta recuperar la sesión con la cookie de refresco. */
  async restaurar(): Promise<void> {
    await firstValueFrom(this.refrescar());
  }

  establecer(sesion: SesionDto): void {
    if (!sesion.token) return;
    this._token.set(sesion.token);
    this._usuario.set(sesion.usuario ?? null);
    const expira = sesion.expiraUtc ? Date.parse(sesion.expiraUtc) : Date.now() + 15 * 60_000;
    this._expira.set(expira);
    this.programarRenovacion(expira);
  }

  actualizarUsuario(usuario: UsuarioDto): void {
    this._usuario.set(usuario);
  }

  /** Vuelve a leer el perfil (saldo de Eco-Puntos, verificación, etc.). */
  recargarUsuario(): void {
    if (!this.autenticado()) return;
    this.cuentaApi.yo().subscribe({ next: (u) => this._usuario.set(u), error: () => {} });
  }

  /**
   * Pide un token nuevo. Peticiones concurrentes comparten un único refresco
   * (el backend rota la cookie y detecta reuso: dos refrescos en paralelo cerrarían la sesión).
   */
  refrescar(): Observable<string | null> {
    if (!this.refrescoEnCurso) {
      this.refrescoEnCurso = this.authApi.refrescar().pipe(
        tap((s) => this.establecer(s)),
        map((s) => s.token ?? null),
        catchError(() => {
          this.limpiar();
          return of(null);
        }),
        finalize(() => (this.refrescoEnCurso = null)),
        shareReplay(1),
      );
    }
    return this.refrescoEnCurso;
  }

  async salir(destino = '/'): Promise<void> {
    try {
      await firstValueFrom(this.authApi.salir());
    } catch {
      // Aunque falle la red, la sesión local se cierra igual.
    }
    this.limpiar();
    await this.router.navigateByUrl(destino);
  }

  /** Cierra la sesión local sin llamar a la API (refresco vencido, cuenta eliminada...). */
  limpiar(): void {
    clearTimeout(this.temporizador);
    this._token.set(null);
    this._usuario.set(null);
    this._expira.set(0);
  }

  private programarRenovacion(expira: number): void {
    clearTimeout(this.temporizador);
    // Renueva un minuto antes de que venza (mínimo 10 s) para no cortar el chat en tiempo real.
    const espera = Math.max(expira - Date.now() - 60_000, 10_000);
    this.temporizador = setTimeout(() => this.refrescar().subscribe(), espera);
  }
}
