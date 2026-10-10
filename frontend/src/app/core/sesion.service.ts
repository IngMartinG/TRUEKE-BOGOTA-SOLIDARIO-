import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { finalize, firstValueFrom, from, Observable, shareReplay } from 'rxjs';
import type { SesionDto, UsuarioDto } from '../api/tipos';
import { AuthApi } from './api/auth.api';
import { CuentaApi } from './api/cuenta.api';

/** Marca "este navegador tuvo sesión": solo un 1, nunca el token ni datos personales. */
const MARCA_SESION = 'trueke.sesion';
/** Candado y canal compartidos por todas las pestañas del mismo navegador. */
const CANDADO_REFRESCO = 'trueke-refresco';
const CANAL_SESION = 'trueke-sesion';
/** Si al volver a la pestaña el token vence antes de esto, se renueva de inmediato. */
const MARGEN_RENOVACION_MS = 2 * 60_000;

type MensajeCanal = { tipo: 'cerrada' };

const esperar = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));

/**
 * Estado de la sesión. El token de acceso vive SOLO en memoria (nunca en localStorage):
 * al recargar la página se recupera con el refresco, que viaja en una cookie HttpOnly.
 *
 * La sesión solo se cierra cuando el servidor la rechaza (401/403 al refrescar). Un corte de red,
 * un servidor reiniciándose o el refresco simultáneo de otra pestaña NO sacan al usuario.
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
  private readonly canal: BroadcastChannel | null = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CANAL_SESION);

  /** Espera base entre reintentos (las pruebas la ponen en 0). */
  esperaReintentoMs = 800;

  readonly token = this._token.asReadonly();
  readonly usuario = this._usuario.asReadonly();
  readonly autenticado = computed(() => this._token() !== null);
  readonly esModerador = computed(() => {
    const rol = this._usuario()?.rol;
    return rol === 'Administrador' || rol === 'SuperUsuario';
  });
  readonly esSuperUsuario = computed(() => this._usuario()?.rol === 'SuperUsuario');
  readonly correoVerificado = computed(() => this._usuario()?.correoVerificado === true);
  /** Foto de perfil: obligatoria (con el correo verificado) para publicar, solicitar, chatear y pagar. */
  readonly tieneFoto = computed(() => !!this._usuario()?.fotoUrl);
  readonly primerNombre = computed(() => this._usuario()?.nombreCompleto?.split(' ')[0] ?? '');

  constructor() {
    // Otra pestaña cerró la sesión (botón "Salir" o sesión rechazada): esta también.
    if (this.canal) this.canal.onmessage = (e: MessageEvent<MensajeCanal>) => e.data?.tipo === 'cerrada' && this.limpiar(false);

    // Al volver a la pestaña o despertar el equipo, los temporizadores pudieron no correr: se renueva si hace falta.
    const alVolver = () => {
      if (document.visibilityState === 'visible' && this.autenticado() && this.venceEn() < MARGEN_RENOVACION_MS)
        this.refrescar().subscribe();
    };
    document.addEventListener('visibilitychange', alVolver);
    window.addEventListener('focus', alVolver);
    window.addEventListener('online', alVolver);
    inject(DestroyRef).onDestroy(() => {
      document.removeEventListener('visibilitychange', alVolver);
      window.removeEventListener('focus', alVolver);
      window.removeEventListener('online', alVolver);
      this.canal?.close();
    });
  }

  /** Milisegundos que le quedan al token actual (negativo si ya venció). */
  venceEn(): number {
    return this._expira() - Date.now();
  }

  /**
   * Al arrancar la app: intenta recuperar la sesión con la cookie de refresco.
   * Solo si este navegador tuvo sesión (marca sin datos sensibles), para no hacer
   * una petición fallida en cada visita anónima.
   */
  async restaurar(): Promise<void> {
    if (!this.huboSesion()) return;
    await firstValueFrom(this.refrescar());
  }

  establecer(sesion: SesionDto): void {
    if (!sesion.token) return;
    this.marcarSesion(true);
    this._token.set(sesion.token);
    this._usuario.set(sesion.usuario ?? null);
    const expira = sesion.expiraUtc ? Date.parse(sesion.expiraUtc) : Date.now() + 15 * 60_000;
    this._expira.set(expira);
    this.programarRenovacion(Math.max(expira - Date.now() - 60_000, 10_000));
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
   * Pide un token nuevo. Peticiones concurrentes de la pestaña comparten un único refresco, y entre
   * pestañas se turnan con un candado: el backend rota la cookie y detecta su reuso.
   * Devuelve null si no se pudo (y solo cierra la sesión si el servidor la rechazó).
   */
  refrescar(): Observable<string | null> {
    if (!this.refrescoEnCurso) {
      this.refrescoEnCurso = from(this.conCandado(() => this.refrescarConReintentos())).pipe(
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

  /** Cierra la sesión local sin llamar a la API (refresco rechazado, cuenta eliminada...). */
  limpiar(avisarPestanas = true): void {
    const teniaSesion = this._token() !== null;
    this.marcarSesion(false);
    clearTimeout(this.temporizador);
    this._token.set(null);
    this._usuario.set(null);
    this._expira.set(0);
    if (avisarPestanas && teniaSesion) this.canal?.postMessage({ tipo: 'cerrada' } satisfies MensajeCanal);
  }

  private async refrescarConReintentos(): Promise<string | null> {
    for (let intento = 0; ; intento++) {
      try {
        const s = await firstValueFrom(this.authApi.refrescar());
        this.establecer(s);
        return s.token ?? null;
      } catch (e) {
        const estado = e instanceof HttpErrorResponse ? e.status : 0;
        // El servidor rechazó la sesión (vencida, revocada o cerrada en otro lado): ahí sí se cierra.
        if (estado === 401 || estado === 403) {
          this.limpiar();
          return null;
        }
        // 409: otra pestaña la acaba de renovar; 0/5xx/429: red caída o servidor ocupado. Se reintenta.
        if (intento < 3) {
          await esperar(this.esperaReintentoMs * 2 ** intento);
          continue;
        }
        // Falla pasajera persistente: se conserva la sesión y se reintenta más tarde.
        this.programarRenovacion(30_000);
        return null;
      }
    }
  }

  private conCandado<T>(fn: () => Promise<T>): Promise<T> {
    const locks = typeof navigator === 'undefined' ? undefined : navigator.locks;
    return locks?.request ? locks.request(CANDADO_REFRESCO, fn) : fn();
  }

  private huboSesion(): boolean {
    try {
      return localStorage.getItem(MARCA_SESION) === '1';
    } catch {
      return true; // sin almacenamiento, se intenta siempre
    }
  }

  private marcarSesion(activa: boolean): void {
    try {
      if (activa) localStorage.setItem(MARCA_SESION, '1');
      else localStorage.removeItem(MARCA_SESION);
    } catch {
      // Almacenamiento bloqueado: no pasa nada, se intentará refrescar siempre.
    }
  }

  private programarRenovacion(esperaMs: number): void {
    clearTimeout(this.temporizador);
    // Normalmente un minuto antes de que venza, para no cortar el chat en tiempo real.
    this.temporizador = setTimeout(() => this.refrescar().subscribe(), esperaMs);
  }
}
