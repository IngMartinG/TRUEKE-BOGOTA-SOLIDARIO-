import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import type { HubConnection } from '@microsoft/signalr';
import { firstValueFrom, Subject } from 'rxjs';
import type { MensajeChatDto, NotificacionDto } from '../api/tipos';
import { CuentaApi } from './api/cuenta.api';
import { IntercambiosApi } from './api/intercambios.api';
import { AvisosService } from './avisos.service';
import { hubUrl } from './entorno';
import { SesionService } from './sesion.service';

export interface EstadoMensajes {
  conversacionId: string;
  entregadosHastaUtc?: string | null;
  leidosHastaUtc?: string | null;
}

/** Notificaciones que cambian el saldo o la reputación: tras recibirlas se recarga el perfil. */
const AFECTAN_PERFIL = new Set([
  'IntercambioCompletado',
  'PagoAprobado',
  'PagoReembolsado',
  'PlanPorVencer',
  'VerificacionAprobada',
  'VerificacionRechazada',
  'CalificacionRecibida',
]);

/**
 * Conexión SignalR con `/hubs/notificaciones` (eventos "notificacion" y "mensaje").
 * Se abre al iniciar sesión y se cierra al salir. Al reconectar se resincroniza lo pendiente.
 */
@Injectable({ providedIn: 'root' })
export class TiempoRealService {
  private readonly sesion = inject(SesionService);
  private readonly cuentaApi = inject(CuentaApi);
  private readonly intercambiosApi = inject(IntercambiosApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private conexion: HubConnection | null = null;
  private conectando = false;

  readonly notificacionesNoLeidas = signal(0);
  readonly mensajesNoLeidos = signal(0);
  readonly conectado = signal(false);
  /** Solo true tras varios segundos sin conexión: los cortes breves (renovar el token) no se muestran. */
  readonly sinConexion = signal(false);
  private temporizadorSinConexion: ReturnType<typeof setTimeout> | undefined;
  private reintento: ReturnType<typeof setTimeout> | undefined;
  /** Conversación visible en pantalla: sus mensajes no suman al contador ni generan aviso. */
  readonly conversacionAbierta = signal<string | null>(null);

  readonly notificacion$ = new Subject<NotificacionDto>();
  readonly mensaje$ = new Subject<MensajeChatDto>();
  /** Mis mensajes de una conversación ya llegaron (✓✓) o ya se leyeron (✓✓ de color) hasta esas fechas. */
  readonly estadoMensajes$ = new Subject<EstadoMensajes>();
  /** Conversaciones donde la otra persona está escribiendo ahora mismo. */
  readonly escribiendoEn = signal<ReadonlySet<string>>(new Set());
  /** "En línea" de las contrapartes (llega por evento; el valor inicial viene en cada conversación). */
  readonly enLinea = signal<ReadonlyMap<string, boolean>>(new Map());
  private readonly temporizadoresEscribiendo = new Map<string, ReturnType<typeof setTimeout>>();
  private ultimoAvisoEscribiendo = new Map<string, number>();
  /** Emite tras reconectar: las pantallas abiertas recargan sus datos. */
  readonly resincronizar$ = new Subject<void>();

  constructor() {
    effect(() => {
      const autenticado = this.sesion.autenticado();
      untracked(() => (autenticado ? void this.conectar() : void this.desconectar()));
    });
    effect(() => {
      const conectado = this.conectado();
      untracked(() => {
        clearTimeout(this.temporizadorSinConexion);
        if (conectado || !this.sesion.autenticado()) this.sinConexion.set(false);
        else this.temporizadorSinConexion = setTimeout(() => this.sinConexion.set(!this.conectado() && this.sesion.autenticado()), 5000);
      });
    });
    // Al volver a la pestaña (o recuperar internet) se reconecta enseguida, sin esperar el próximo reintento.
    const alVolver = () => {
      if (document.visibilityState !== 'visible' || !this.sesion.autenticado() || this.conexion) return;
      clearTimeout(this.reintento);
      void this.conectar();
    };
    document.addEventListener('visibilitychange', alVolver);
    window.addEventListener('online', alVolver);
  }

  async sincronizarContadores(): Promise<void> {
    if (!this.sesion.autenticado()) return;
    try {
      const [total, conversaciones] = await Promise.all([
        firstValueFrom(this.cuentaApi.totalNoLeidas()),
        firstValueFrom(this.intercambiosApi.conversaciones()),
      ]);
      this.notificacionesNoLeidas.set(total ?? 0);
      this.mensajesNoLeidos.set(conversaciones.reduce((s, c) => s + (c.noLeidos ?? 0), 0));
    } catch {
      // Los contadores se reintentan en la próxima reconexión o navegación.
    }
  }

  private async conectar(): Promise<void> {
    if (this.conexion || this.conectando) return;
    this.conectando = true;
    // SignalR se descarga solo cuando hay sesión: no pesa en la carga inicial del catálogo.
    const { HubConnectionBuilder, HttpTransportType, LogLevel } = await import('@microsoft/signalr').finally(
      () => (this.conectando = false),
    );
    if (this.conexion || !this.sesion.autenticado()) return;
    const conexion = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        // El servidor cierra el hub cuando vence el JWT: al (re)conectar se usa uno que dure.
        accessTokenFactory: async () =>
          (this.sesion.token() === null || this.sesion.venceEn() < 60_000 ? await firstValueFrom(this.sesion.refrescar()) : null) ??
          this.sesion.token() ??
          '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .configureLogging(LogLevel.None)
      .build();

    conexion.on('notificacion', (n: NotificacionDto) => this.alRecibirNotificacion(n));
    conexion.on('mensaje', (m: MensajeChatDto) => this.alRecibirMensaje(m));
    conexion.on('estadoMensajes', (e: EstadoMensajes) => this.estadoMensajes$.next(e));
    conexion.on('escribiendo', (e: { conversacionId: string }) => this.alEscribir(e.conversacionId));
    conexion.on('presencia', (p: { usuarioId: string; enLinea: boolean }) =>
      this.enLinea.update((m) => new Map(m).set(p.usuarioId, p.enLinea)),
    );
    conexion.onreconnecting(() => this.conectado.set(false));
    conexion.onreconnected(() => {
      this.conectado.set(true);
      void this.sincronizarContadores();
      this.resincronizar$.next();
    });
    conexion.onclose(() => {
      this.conectado.set(false);
      // El servidor cierra la conexión cuando vence el JWT con que se abrió: se reabre con el nuevo.
      if (this.conexion === conexion && this.sesion.autenticado()) {
        this.conexion = null;
        clearTimeout(this.reintento);
        this.reintento = setTimeout(() => void this.conectar(), 1500);
      }
    });

    this.conexion = conexion;
    void this.sincronizarContadores();
    try {
      await conexion.start();
      this.conectado.set(true);
      // Las pantallas cargaron sus datos por HTTP antes de que existiera esta conexión: lo que llegó en ese lapso
      // (un mensaje, una solicitud) no se recibió en vivo. Se resincroniza igual que tras una reconexión.
      void this.sincronizarContadores();
      this.resincronizar$.next();
    } catch {
      if (this.conexion === conexion) {
        this.conexion = null;
        if (this.sesion.autenticado()) {
          clearTimeout(this.reintento);
          this.reintento = setTimeout(() => void this.conectar(), 10_000);
        }
      }
    }
  }

  private async desconectar(): Promise<void> {
    const conexion = this.conexion;
    this.conexion = null;
    this.notificacionesNoLeidas.set(0);
    this.mensajesNoLeidos.set(0);
    this.conectado.set(false);
    if (conexion && String(conexion.state) !== 'Disconnected') await conexion.stop();
  }

  private alRecibirNotificacion(n: NotificacionDto): void {
    this.notificacionesNoLeidas.update((v) => v + 1);
    this.notificacion$.next(n);
    if (n.tipo && AFECTAN_PERFIL.has(n.tipo)) this.sesion.recargarUsuario();
    if (n.tipo === 'IntercambioCompletado') this.avisos.puntos('¡Intercambio completado!', n.mensaje);
    else this.avisos.info('Nueva notificación', n.mensaje);
  }

  /** Avisa que estoy escribiendo (como mucho cada 3 s por conversación). */
  avisarEscribiendo(conversacionId: string): void {
    const ahora = Date.now();
    if (ahora - (this.ultimoAvisoEscribiendo.get(conversacionId) ?? 0) < 3000) return;
    this.ultimoAvisoEscribiendo.set(conversacionId, ahora);
    if (String(this.conexion?.state) === 'Connected') void this.conexion!.invoke('Escribiendo', conversacionId).catch(() => undefined);
  }

  /** Al enviar, el "escribiendo…" de la otra pantalla debe poder volver a mostrarse enseguida. */
  reiniciarEscribiendo(conversacionId: string): void {
    this.ultimoAvisoEscribiendo.delete(conversacionId);
  }

  private alEscribir(conversacionId: string): void {
    this.escribiendoEn.update((s) => new Set(s).add(conversacionId));
    clearTimeout(this.temporizadoresEscribiendo.get(conversacionId));
    this.temporizadoresEscribiendo.set(conversacionId, setTimeout(() => this.dejarDeEscribir(conversacionId), 4000));
  }

  private dejarDeEscribir(conversacionId: string): void {
    clearTimeout(this.temporizadoresEscribiendo.get(conversacionId));
    this.temporizadoresEscribiendo.delete(conversacionId);
    this.escribiendoEn.update((s) => {
      const n = new Set(s);
      n.delete(conversacionId);
      return n;
    });
  }

  private alRecibirMensaje(m: MensajeChatDto): void {
    // Llegó el mensaje: ya no está "escribiendo", y el autor ve ✓✓ (entregado).
    if (!m.esMio && m.conversacionId) {
      this.dejarDeEscribir(m.conversacionId);
      void this.conexion?.invoke('Recibido', m.conversacionId).catch(() => undefined);
    }
    this.mensaje$.next(m);
    if (m.esMio || m.conversacionId === this.conversacionAbierta()) return;
    this.mensajesNoLeidos.update((v) => v + 1);
    if (!this.router.url.startsWith('/mensajes')) this.avisos.info('Nuevo mensaje', m.texto?.slice(0, 80));
  }
}
