import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';
import { CuentaApi } from './api/cuenta.api';
import { ConfigService } from './config.service';
import { enPantallaCompleta, esIos } from './pwa.service';
import { SesionService } from './sesion.service';

export type EstadoPush =
  /** El navegador no las soporta (o el modo desarrollo, sin service worker). */
  | 'no-soportado'
  /** iPhone/iPad: Apple solo las permite con la app instalada en la pantalla de inicio. */
  | 'instalar-ios'
  /** El servidor no tiene llaves configuradas. */
  | 'no-disponible'
  /** La persona las bloqueó en el navegador: solo se pueden reactivar desde su configuración. */
  | 'bloqueado'
  | 'activo'
  | 'inactivo';

/**
 * Notificaciones push en ESTE dispositivo. Se activan por dispositivo y con el permiso del navegador (ese es el
 * consentimiento). Al cerrar la sesión el dispositivo se da de baja: la siguiente persona que use el navegador nunca
 * recibe avisos de la cuenta anterior.
 */
@Injectable({ providedIn: 'root' })
export class NotificacionesPushService {
  private readonly swPush = inject(SwPush);
  private readonly config = inject(ConfigService);
  private readonly sesion = inject(SesionService);
  private readonly api = inject(CuentaApi);

  private readonly suscripcion = toSignal(this.swPush.subscription, { initialValue: null });
  private readonly permiso = signal<NotificationPermission>(typeof Notification === 'undefined' ? 'default' : Notification.permission);
  readonly ocupado = signal(false);

  readonly estado = computed<EstadoPush>(() => {
    const soportado = this.swPush.isEnabled && 'PushManager' in window && typeof Notification !== 'undefined';
    if (!soportado) return esIos() && !enPantallaCompleta() ? 'instalar-ios' : 'no-soportado';
    if (!this.config.pushClavePublica()) return 'no-disponible';
    if (this.permiso() === 'denied') return 'bloqueado';
    return this.suscripcion() ? 'activo' : 'inactivo';
  });

  constructor() {
    this.sesion.registrarAntesDeSalir(() => this.darDeBajaEnServidor());
    let antes = this.sesion.autenticado();
    effect(() => {
      const ahora = this.sesion.autenticado();
      untracked(() => {
        // Sesión cerrada (botón Salir, vencida o rechazada): este navegador deja de recibir avisos.
        if (antes && !ahora) void this.swPush.unsubscribe().catch(() => undefined);
        // Sesión abierta con el permiso ya dado: se renueva en el servidor (el navegador pudo rotar las llaves).
        if (!antes && ahora) void this.renovar();
        antes = ahora;
      });
    });
  }

  async activar(): Promise<void> {
    const clave = this.config.pushClavePublica();
    if (!clave || this.ocupado()) return;
    this.ocupado.set(true);
    try {
      const s = await this.swPush.requestSubscription({ serverPublicKey: clave });
      await firstValueFrom(this.api.registrarPush(aSolicitud(s)));
    } finally {
      this.permiso.set(Notification.permission);
      this.ocupado.set(false);
    }
  }

  async desactivar(): Promise<void> {
    if (this.ocupado()) return;
    this.ocupado.set(true);
    try {
      await this.darDeBajaEnServidor();
      await this.swPush.unsubscribe().catch(() => undefined);
    } finally {
      this.ocupado.set(false);
    }
  }

  private async renovar(): Promise<void> {
    if (!this.swPush.isEnabled) return; // sin service worker (desarrollo) "ready" nunca se resuelve
    const s = await navigator.serviceWorker.ready.then((r) => r.pushManager.getSubscription()).catch(() => null);
    if (s && this.config.pushClavePublica()) await firstValueFrom(this.api.registrarPush(aSolicitud(s))).catch(() => undefined);
  }

  private async darDeBajaEnServidor(): Promise<void> {
    const s = this.suscripcion();
    if (s) await firstValueFrom(this.api.bajaPush(s.endpoint)).catch(() => undefined);
  }
}

function aSolicitud(s: PushSubscription) {
  const j = s.toJSON();
  return { endpoint: s.endpoint, p256dh: j.keys?.['p256dh'] ?? '', auth: j.keys?.['auth'] ?? '' };
}
