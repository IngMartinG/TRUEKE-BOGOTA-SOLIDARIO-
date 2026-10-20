import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { LiveAnnouncer } from '@angular/cdk/a11y';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETIQUETA_ESTADO_SOLICITUD, type ConversacionDto, type MensajeChatDto } from '../../api/tipos';
import { IntercambiosApi } from '../../core/api/intercambios.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { FechaPipe, HacePipe, HoraPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Bloquear } from '../../shared/ui/bloquear';
import { Denunciar } from '../../shared/ui/denunciar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { InsigniaNivel } from '../../shared/ui/insignia-nivel';
import { PuntosEncuentro } from '../../shared/ui/puntos-encuentro';
import { desplazamiento } from '../../shared/movimiento';

const TAMANO = 40;

@Component({
  imports: [FormsModule, RouterLink, Avatar, Icono, InsigniaNivel, EstadoVacio, PuntosEncuentro, Denunciar, Bloquear, HacePipe, HoraPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-4 sm:py-8">
      <div #tarjeta [style.height.px]="alto()" class="tarjeta grid h-[calc(100dvh-10rem)] min-h-[28rem] grid-cols-1 overflow-hidden md:grid-cols-[18rem_1fr] lg:h-[calc(100dvh-9rem)] xl:grid-cols-[20rem_1fr]">
        <!-- Lista de conversaciones -->
        <aside class="min-h-0 flex-col border-r border-borde" [class]="id() ? 'hidden md:flex' : 'flex'" aria-label="Conversaciones">
          <header class="flex items-center justify-between border-b border-borde px-5 py-4">
            <h1 class="text-xl font-extrabold">Mensajes</h1>
            @if (tiempoReal.sinConexion()) {
              <span class="insignia-neutra" title="Reconectando…"><span class="size-2 rounded-full bg-sol-400"></span>Sin conexión</span>
            }
          </header>
          <ul class="min-h-0 flex-1 overflow-y-auto">
            @if (conversaciones.isLoading() && !conversaciones.value()) {
              @for (i of [1, 2, 3]; track i) {
                <li class="flex gap-3 p-4"><div class="esqueleto size-11 rounded-full"></div><div class="flex-1 space-y-2"><div class="esqueleto h-3 w-2/3"></div><div class="esqueleto h-3 w-1/2"></div></div></li>
              }
            }
            @for (c of conversacionesOrdenadas(); track c.id) {
              <li>
                <a [routerLink]="['/mensajes', c.id]" class="flex gap-3 border-l-4 px-4 py-3.5 transition hover:bg-superficie-2"
                  [class]="c.id === id() ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/30' : 'border-transparent'">
                  <span class="relative shrink-0">
                    <app-avatar [nombre]="c.contraparte?.nombre" [foto]="c.contraparte?.fotoUrl" [tamano]="44" [verificado]="!!c.contraparte?.verificado" />
                    @if (estaEnLinea(c)) {
                      <span class="absolute right-0 bottom-0 size-3 rounded-full border-2 border-superficie bg-bosque-500" title="En línea"></span>
                    }
                  </span>
                  <div class="min-w-0 flex-1">
                    <div class="flex items-baseline justify-between gap-2">
                      <p class="truncate font-semibold">{{ c.contraparte?.nombre }}</p>
                      <span class="shrink-0 text-[11px] text-tenue">{{ c.ultimoMensajeUtc | hace }}</span>
                    </div>
                    <p class="truncate text-xs font-medium text-bosque-700 dark:text-bosque-300">{{ c.publicacionTitulo }}</p>
                    <div class="flex items-center justify-between gap-2">
                      @if (tiempoReal.escribiendoEn().has(c.id ?? '')) {
                        <p class="truncate text-sm font-semibold text-bosque-600 italic dark:text-bosque-300">escribiendo…</p>
                      } @else {
                        <p class="truncate text-sm" [class]="c.noLeidos ? 'font-semibold text-tinta' : 'text-tenue'">{{ c.ultimoMensaje || 'Sin mensajes todavía' }}</p>
                      }
                      @if (c.noLeidos) {
                        <span class="grid min-w-5 shrink-0 place-items-center rounded-full bg-bosque-600 px-1.5 text-[11px] font-bold text-white">{{ c.noLeidos }}</span>
                      }
                    </div>
                  </div>
                </a>
              </li>
            } @empty {
              @if (!conversaciones.isLoading()) {
                <li class="p-6 text-center text-sm text-tenue">
                  Las conversaciones se abren cuando se acepta una solicitud.
                  <a routerLink="/intercambios" class="enlace mt-2 block">Ver mis intercambios</a>
                </li>
              }
            }
          </ul>
        </aside>

        <!-- Conversación -->
        <section class="min-h-0 min-w-0 flex-col" [class]="id() ? 'flex' : 'hidden md:flex'" aria-label="Conversación">
          @if (!id()) {
            <div class="grid flex-1 place-items-center">
              <app-estado-vacio icono="mensaje" titulo="Elige una conversación" descripcion="Aquí coordinas la entrega sin compartir tu número ni tu correo." />
            </div>
          } @else {
            <header class="flex items-center gap-3 border-b border-borde px-4 py-3">
              <a routerLink="/mensajes" class="btn-icono md:hidden" aria-label="Volver a conversaciones"><app-icono nombre="izquierda" /></a>
              @if (actual(); as c) {
                <a [routerLink]="['/usuarios', c.contraparte?.id]" class="flex min-w-0 flex-1 items-center gap-3">
                  <app-avatar [nombre]="c.contraparte?.nombre" [foto]="c.contraparte?.fotoUrl" [tamano]="40" [verificado]="!!c.contraparte?.verificado" />
                  <div class="min-w-0">
                    <p class="flex min-w-0 items-center gap-2"><span class="truncate font-semibold">{{ c.contraparte?.nombre }}</span><app-insignia-nivel [nivel]="c.contraparte?.nivel" class="shrink-0" /></p>
                    @if (tiempoReal.escribiendoEn().has(c.id ?? '')) {
                      <p class="truncate text-xs font-semibold text-bosque-600 italic dark:text-bosque-300" aria-live="polite">escribiendo…</p>
                    } @else if (estaEnLinea(c)) {
                      <p class="flex items-center gap-1.5 truncate text-xs font-medium text-bosque-700 dark:text-bosque-300">
                        <span class="size-2 shrink-0 rounded-full bg-bosque-500"></span>en línea · <span class="truncate text-tenue">{{ c.publicacionTitulo }}</span>
                      </p>
                    } @else {
                      <p class="truncate text-xs text-tenue">{{ c.publicacionTitulo }} · {{ estado(c.estadoSolicitud) }}</p>
                    }
                  </div>
                </a>
                <a [routerLink]="['/publicacion', c.publicacionId]" class="btn btn-secundario btn-sm hidden sm:inline-flex md:hidden lg:inline-flex">Ver publicación</a>
                <a routerLink="/intercambios" [queryParams]="{ tab: c.soyDuenio ? 'recibidas' : 'enviadas' }" class="btn btn-primario btn-sm hidden sm:inline-flex">Intercambio</a>
                @if (c.contraparte?.id; as contraparteId) {
                  <app-bloquear [usuarioId]="contraparteId" [nombre]="c.contraparte.nombre ?? 'esta persona'" [bloqueado]="!!c.yoBloquee"
                    (bloqueadoChange)="conversaciones.reload()" clase="btn btn-fantasma btn-sm shrink-0 max-sm:px-2.5" />
                }
              } @else {
                <div class="esqueleto h-10 flex-1"></div>
              }
            </header>

            <div #lista class="min-h-0 flex-1 space-y-1 overflow-y-auto bg-superficie-2/40 px-4 py-4">
              @if (hayMas()) {
                <div class="mb-3 text-center">
                  <button type="button" class="btn btn-secundario btn-sm" (click)="cargarAnteriores()" [disabled]="cargandoAnteriores()">Ver mensajes anteriores</button>
                </div>
              }
              @if (cargando()) {
                <p class="py-10 text-center text-sm text-tenue">Cargando mensajes…</p>
              }
              @for (m of mensajes(); track m.id; let i = $index) {
                @if (i === 0 || dia(m.fechaUtc) !== dia(mensajes()[i - 1]!.fechaUtc)) {
                  <p class="py-3 text-center text-[11px] font-semibold tracking-wide text-tenue uppercase">{{ m.fechaUtc | fecha }}</p>
                }
                <!-- Deslizar a la derecha (táctil) responde el mensaje, como en WhatsApp. touch-action deja el scroll vertical al navegador. -->
                <div class="group relative flex touch-pan-y items-center rounded-2xl transition-colors duration-700" [class.justify-end]="m.esMio"
                  [attr.id]="'msg-' + m.id" [class]="resaltado() === m.id ? 'bg-sol-100 dark:bg-sol-900/30' : ''"
                  (pointerdown)="alTocar($event, m)" (pointermove)="alMover($event)" (pointerup)="alSoltar()" (pointercancel)="cancelarDeslizamiento()">
                  @if (deslizamiento()?.id === m.id) {
                    <span class="absolute left-1 grid size-8 place-items-center rounded-full bg-superficie text-bosque-600 shadow-sm transition-opacity"
                      [style.opacity]="deslizamiento()!.dx / UMBRAL" aria-hidden="true">
                      <app-icono nombre="responder" [tamano]="16" />
                    </span>
                  }
                  @if (m.esMio && puedeResponder(m)) {
                    <button type="button" class="mr-1 shrink-0 self-center rounded-full p-2 text-tenue opacity-0 transition group-hover:opacity-100 focus:opacity-100 hover:bg-superficie-2 hover:text-bosque-600"
                      [class.opacity-100]="accionesDe() === m.id" (click)="responder(m)" aria-label="Responder este mensaje" title="Responder"><app-icono nombre="responder" [tamano]="15" /></button>
                  }
                  <div class="max-w-[80%] rounded-2xl px-3.5 py-2 text-sm shadow-sm"
                    [class]="m.esMio ? 'rounded-br-md bg-bosque-600 text-white' : 'rounded-bl-md bg-superficie text-tinta'"
                    [class.transition-transform]="deslizamiento()?.id !== m.id"
                    [style.transform]="deslizamiento()?.id === m.id ? 'translateX(' + deslizamiento()!.dx + 'px)' : null">
                    @if (m.respuestaA; as cita) {
                      <button type="button" class="mb-1.5 block w-full rounded-lg border-l-4 px-2.5 py-1.5 text-left text-xs"
                        [class]="m.esMio ? 'border-white/70 bg-white/15 text-white/90' : 'border-bosque-500 bg-superficie-2 text-tenue'"
                        (click)="irAMensaje(cita.id)" [attr.aria-label]="'Ir al mensaje citado de ' + autor(cita.esMio)">
                        <span class="block font-semibold" [class]="m.esMio ? 'text-white' : 'text-bosque-700 dark:text-bosque-300'">{{ autor(cita.esMio) }}</span>
                        <span class="line-clamp-2 break-words whitespace-pre-line" [class.italic]="cita.oculto">{{ cita.texto }}</span>
                      </button>
                    }
                    @if (m.oculto) {
                      <p class="italic opacity-70">Mensaje oculto por moderación</p>
                    } @else {
                      <p class="break-words whitespace-pre-line">{{ m.texto }}</p>
                    }
                    <p class="mt-0.5 flex items-center justify-end gap-1 text-[11px]" [class]="m.esMio ? 'text-white/85' : 'text-tenue'">
                      {{ m.fechaUtc | hora }}
                      @if (m.esMio) {
                        <!-- ✓ enviado · ✓✓ entregado · ✓✓ celeste leído (como en WhatsApp) -->
                        <app-icono [nombre]="m.estado === 'Enviado' ? 'check' : 'checkDoble'" [tamano]="15"
                          [class]="m.estado === 'Leido' ? 'text-sol-300' : ''"
                          [etiqueta]="m.estado === 'Leido' ? 'Leído' : m.estado === 'Entregado' ? 'Entregado' : 'Enviado'" />
                      }
                    </p>
                  </div>
                  @if (!m.esMio && !m.oculto) {
                    @if (puedeResponder(m)) {
                      <button type="button" class="ml-1 shrink-0 self-center rounded-full p-2 text-tenue opacity-0 transition group-hover:opacity-100 focus:opacity-100 hover:bg-superficie-2 hover:text-bosque-600"
                        [class.opacity-100]="accionesDe() === m.id" (click)="responder(m)" aria-label="Responder este mensaje" title="Responder"><app-icono nombre="responder" [tamano]="15" /></button>
                    }
                    <button type="button" class="shrink-0 self-center rounded-full p-2 text-tenue opacity-0 transition group-hover:opacity-100 focus:opacity-100 hover:bg-superficie-2 hover:text-tierra-600 dark:hover:text-tierra-500"
                      [class.opacity-100]="accionesDe() === m.id" (click)="denunciar(m)" aria-label="Reportar este mensaje" title="Reportar"><app-icono nombre="bandera" [tamano]="15" /></button>
                  }
                </div>
              } @empty {
                @if (!cargando()) {
                  <div class="py-12 text-center">
                    <p class="font-semibold">¡Rompe el hielo! 👋</p>
                    <p class="mt-1 text-sm text-tenue">Propón un lugar público y un horario para la entrega.</p>
                    @if (actual()?.escribible !== false && !lugaresAbiertos()) {
                      <app-puntos-encuentro variante="chat" class="mt-4 [&_ul]:justify-center" (elegir)="proponerLugar($event)" />
                    }
                  </div>
                }
              }
            </div>

            @if (actual()?.escribible === false) {
              <p class="border-t border-borde px-4 py-4 text-center text-sm text-tenue">
                @if (actual()?.yoBloquee) {
                  Bloqueaste a {{ actual()?.contraparte?.nombre }} · desbloquéalo arriba para volver a escribirse
                } @else if (['Pendiente', 'Aceptada', 'Completada'].includes(actual()?.estadoSolicitud ?? '')) {
                  Ya no puedes enviar mensajes en esta conversación.
                } @else {
                  Esta conversación está cerrada porque el intercambio terminó.
                }
              </p>
            } @else {
              @if (respondiendoA(); as r) {
                <div class="flex items-start gap-2 border-t border-borde bg-superficie-2/60 px-3 pt-2.5 animate-aparecer">
                  <div class="min-w-0 flex-1 rounded-lg border-l-4 border-bosque-500 bg-superficie px-3 py-1.5 text-xs">
                    <p class="font-semibold text-bosque-700 dark:text-bosque-300">Respondiendo a {{ r.esMio ? 'tu mensaje' : actual()?.contraparte?.nombre }}</p>
                    <p class="truncate text-tenue">{{ r.texto }}</p>
                  </div>
                  <button type="button" class="btn-icono" (click)="cancelarRespuesta()" aria-label="Cancelar respuesta"><app-icono nombre="x" [tamano]="16" /></button>
                </div>
              }
              @if (lugaresAbiertos()) {
                <div id="lugares-seguros" class="border-t border-borde bg-superficie-2/60 px-3 pt-2.5">
                  <p class="mb-2 text-xs text-tenue">Elige un tipo de lugar: se escribe la propuesta y puedes editarla antes de enviar.</p>
                  <app-puntos-encuentro variante="chat" (elegir)="proponerLugar($event)" />
                </div>
              }
              <form class="flex items-end gap-2 border-t border-borde p-3" [class.border-t-0]="respondiendoA() || lugaresAbiertos()" (ngSubmit)="enviar()">
                <button type="button" class="btn-icono size-11 shrink-0 aria-expanded:bg-bosque-50 aria-expanded:text-bosque-700 dark:aria-expanded:bg-bosque-900/40 dark:aria-expanded:text-bosque-300"
                  [attr.aria-expanded]="lugaresAbiertos()" aria-controls="lugares-seguros" aria-label="Proponer un lugar seguro" title="Proponer un lugar seguro"
                  (click)="lugaresAbiertos.update((v) => !v)">
                  <app-icono nombre="escudo" [tamano]="20" />
                </button>
                <label for="nuevo-mensaje" class="sr-only">Escribe un mensaje</label>
                <textarea #entrada id="nuevo-mensaje" name="texto" rows="1" class="entrada max-h-32 min-h-11 resize-none rounded-2xl py-2.5" maxlength="1000"
                  [(ngModel)]="texto" (input)="alEscribir()" (keydown.enter)="alPresionarEnter($event)" (keydown.escape)="cancelarRespuesta()" placeholder="Escribe un mensaje…"
                  aria-describedby="aviso-chat"></textarea>
                <button type="submit" class="grid size-11 shrink-0 place-items-center rounded-full bg-bosque-600 text-white transition hover:bg-bosque-700 disabled:opacity-50"
                  [disabled]="!texto().trim() || enviando()" aria-label="Enviar">
                  <app-icono nombre="enviar" [tamano]="18" />
                </button>
              </form>
              <p id="aviso-chat" class="px-4 pb-2 text-[11px] leading-snug text-tenue">
                Por seguridad, no compartas documentos, claves ni datos bancarios por el chat.
                <a routerLink="/terminos" fragment="seccion-10" class="underline hover:text-tinta">Reglas de conducta</a>
              </p>
            }
          }
        </section>
      </div>
    </div>

    @if (aDenunciar(); as idMensaje) {
      <app-denunciar [(abierto)]="denunciaAbierta" tipo="Mensaje" [objetivoId]="idMensaje" />
    }
  `,
})
export default class Mensajes {
  private readonly api = inject(IntercambiosApi);
  private readonly avisos = inject(AvisosService);
  protected readonly tiempoReal = inject(TiempoRealService);
  private readonly anunciador = inject(LiveAnnouncer);

  readonly id = input<string>();
  private readonly lista = viewChild<ElementRef<HTMLElement>>('lista');
  private readonly entrada = viewChild<ElementRef<HTMLTextAreaElement>>('entrada');

  /** Píxeles que hay que deslizar para responder. */
  protected readonly UMBRAL = 64;

  protected readonly conversaciones = rxResource({ stream: () => this.api.conversaciones() });
  protected readonly conversacionesOrdenadas = computed(() =>
    [...(this.conversaciones.value() ?? [])].sort((a, b) => Date.parse(b.ultimoMensajeUtc ?? '') - Date.parse(a.ultimoMensajeUtc ?? '')),
  );
  protected readonly actual = computed(() => this.conversaciones.value()?.find((c) => c.id === this.id()));

  protected readonly mensajes = signal<MensajeChatDto[]>([]);
  protected readonly cargando = signal(false);
  protected readonly cargandoAnteriores = signal(false);
  protected readonly hayMas = signal(false);
  protected readonly texto = signal('');
  protected readonly enviando = signal(false);
  protected readonly aDenunciar = signal<string | null>(null);
  protected readonly denunciaAbierta = signal(false);
  protected readonly respondiendoA = signal<MensajeChatDto | null>(null);
  /** Botones de lugares seguros sobre el cuadro de texto. */
  protected readonly lugaresAbiertos = signal(false);
  protected readonly deslizamiento = signal<{ id: string; dx: number } | null>(null);
  protected readonly resaltado = signal<string | null>(null);
  /** Mensaje tocado en pantalla táctil: muestra sus acciones (con mouse aparecen al pasar por encima). */
  protected readonly accionesDe = signal<string | null>(null);
  /** Alto de la tarjeta del chat: lo que queda bajo el encabezado y las franjas, sin la barra inferior del celular. */
  protected readonly alto = signal<number | null>(null);
  private readonly tarjeta = viewChild<ElementRef<HTMLElement>>('tarjeta');
  private gesto: { mensaje: MensajeChatDto; x: number; y: number; horizontal: boolean | null; vibro: boolean } | null = null;

  constructor() {
    effect((alLimpiar) => {
      const id = this.id() ?? null;
      untracked(() => {
        this.tiempoReal.conversacionAbierta.set(id);
        if (id) void this.abrir(id);
      });
      alLimpiar(() => this.tiempoReal.conversacionAbierta.set(null));
    });

    this.tiempoReal.mensaje$.pipe(takeUntilDestroyed()).subscribe(async (m) => {
      if (m.conversacionId === this.id()) {
        if (!this.mensajes().some((x) => x.id === m.id)) {
          this.mensajes.update((l) => [...l, m]);
          this.bajar();
          // Al lector de pantalla solo se le anuncian los mensajes que llegan, no el historial al abrir el chat
          if (!m.esMio) void this.anunciador.announce(`Mensaje de ${this.actual()?.contraparte?.nombre ?? 'la otra persona'}: ${m.texto}`);
        }
        // Primero se marca como leída y luego se recarga la lista, para no mostrar un "no leído" falso.
        // Solo si la pantalla se ve: con la pestaña oculta el autor ve ✓✓ (entregado), no "leído".
        if (!m.esMio && document.visibilityState === 'visible')
          await firstValueFrom(this.api.marcarLeida(m.conversacionId!)).catch(() => undefined);
      }
      this.conversaciones.reload();
    });
    // Al volver a la pestaña con la conversación abierta, lo que llegó mientras tanto queda leído.
    const alVolver = () => {
      const id = this.id();
      if (document.visibilityState === 'visible' && id && this.mensajes().some((m) => !m.esMio))
        void firstValueFrom(this.api.marcarLeida(id))
          .then(() => this.tiempoReal.sincronizarContadores())
          .catch(() => undefined);
    };
    document.addEventListener('visibilitychange', alVolver);
    const destroy = inject(DestroyRef);
    destroy.onDestroy(() => document.removeEventListener('visibilitychange', alVolver));

    // La tarjeta ocupa justo el espacio visible: así la caja para escribir nunca queda bajo la barra inferior del
    // celular ni depende de cuántas franjas de aviso haya arriba. Se recalcula si cambia la ventana o el contenido de arriba.
    afterNextRender(() => {
      const ajustar = () => {
        const el = this.tarjeta()?.nativeElement;
        if (!el?.parentElement) return;
        const barra = document.querySelector('app-barra-movil nav')?.getBoundingClientRect().height ?? 0;
        const margen = parseFloat(getComputedStyle(el.parentElement).paddingBottom) || 0;
        const arriba = el.getBoundingClientRect().top + window.scrollY;
        this.alto.set(Math.max(320, Math.floor(window.innerHeight - arriba - barra - margen)));
      };
      ajustar();
      const observador = new ResizeObserver(ajustar);
      observador.observe(document.body);
      window.addEventListener('resize', ajustar);
      destroy.onDestroy(() => {
        observador.disconnect();
        window.removeEventListener('resize', ajustar);
      });
    });

    // ✓✓ y ✓✓ de color en mis mensajes, en vivo
    this.tiempoReal.estadoMensajes$.pipe(takeUntilDestroyed()).subscribe((e) => {
      if (e.conversacionId !== this.id()) return;
      const leidos = e.leidosHastaUtc ? Date.parse(e.leidosHastaUtc) : null;
      const entregados = e.entregadosHastaUtc ? Date.parse(e.entregadosHastaUtc) : null;
      this.mensajes.update((l) =>
        l.map((m) => {
          if (!m.esMio || !m.fechaUtc) return m;
          const fecha = Date.parse(m.fechaUtc);
          if (leidos !== null && fecha <= leidos) return { ...m, estado: 'Leido', leido: true };
          if (entregados !== null && fecha <= entregados && m.estado === 'Enviado') return { ...m, estado: 'Entregado' };
          return m;
        }),
      );
    });
    this.tiempoReal.resincronizar$.pipe(takeUntilDestroyed()).subscribe(() => {
      this.conversaciones.reload();
      const id = this.id();
      if (id) void this.abrir(id);
    });
  }

  private async abrir(id: string): Promise<void> {
    this.cargando.set(true);
    this.mensajes.set([]);
    this.respondiendoA.set(null);
    try {
      const lista = await firstValueFrom(this.api.mensajes(id, undefined, TAMANO));
      if (this.id() !== id) return;
      this.mensajes.set(lista);
      this.hayMas.set(lista.length >= TAMANO);
      this.bajar();
      await firstValueFrom(this.api.marcarLeida(id));
      void this.tiempoReal.sincronizarContadores();
      this.conversaciones.reload();
    } catch {
      // El interceptor ya informó el error.
    } finally {
      this.cargando.set(false);
    }
  }

  protected async cargarAnteriores(): Promise<void> {
    const id = this.id();
    const primero = this.mensajes()[0];
    if (!id || !primero?.fechaUtc) return;
    this.cargandoAnteriores.set(true);
    const el = this.lista()?.nativeElement;
    const alturaAntes = el?.scrollHeight ?? 0;
    try {
      const anteriores = await firstValueFrom(this.api.mensajes(id, primero.fechaUtc, TAMANO));
      this.mensajes.update((l) => [...anteriores.filter((a) => !l.some((x) => x.id === a.id)), ...l]);
      this.hayMas.set(anteriores.length >= TAMANO);
      // Mantiene la posición de lectura al anteponer mensajes.
      requestAnimationFrame(() => el && (el.scrollTop = el.scrollHeight - alturaAntes));
    } finally {
      this.cargandoAnteriores.set(false);
    }
  }

  protected async enviar(): Promise<void> {
    const id = this.id();
    const texto = this.texto().trim();
    if (!id || !texto) return;
    this.enviando.set(true);
    try {
      const m = await firstValueFrom(this.api.enviarMensaje(id, texto, this.respondiendoA()?.id ?? null));
      this.texto.set('');
      this.respondiendoA.set(null);
      this.tiempoReal.reiniciarEscribiendo(id);
      if (!this.mensajes().some((x) => x.id === m.id)) this.mensajes.update((l) => [...l, m]);
      this.bajar();
      this.conversaciones.reload();
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }

  protected alPresionarEnter(e: Event): void {
    const k = e as KeyboardEvent;
    if (!k.shiftKey) {
      k.preventDefault();
      void this.enviar();
    }
  }

  protected estaEnLinea(c: ConversacionDto): boolean {
    const id = c.contraparte?.id;
    return (id ? this.tiempoReal.enLinea().get(id) : undefined) ?? !!c.contraparteEnLinea;
  }

  /** Escribe la propuesta del lugar sin enviarla: la persona la ajusta (día, hora) y la envía ella. */
  protected proponerLugar(propuesta: string): void {
    this.texto.set(propuesta);
    this.lugaresAbiertos.set(false);
    requestAnimationFrame(() => {
      const e = this.entrada()?.nativeElement;
      e?.focus();
      e?.setSelectionRange(propuesta.length, propuesta.length);
    });
  }

  protected alEscribir(): void {
    const id = this.id();
    if (id && this.texto().trim()) this.tiempoReal.avisarEscribiendo(id);
  }

  // ---------------- Responder un mensaje en particular ----------------

  protected puedeResponder(m: MensajeChatDto): boolean {
    return !m.oculto && this.actual()?.escribible !== false;
  }

  protected responder(m: MensajeChatDto): void {
    if (!this.puedeResponder(m)) return;
    this.respondiendoA.set(m);
    requestAnimationFrame(() => this.entrada()?.nativeElement.focus());
  }

  protected cancelarRespuesta(): void {
    this.respondiendoA.set(null);
  }

  protected autor(esMio: boolean | undefined): string {
    return esMio ? 'Tú' : (this.actual()?.contraparte?.nombre ?? 'La otra persona');
  }

  /** Lleva al mensaje citado y lo resalta un momento. */
  protected irAMensaje(id: string | undefined): void {
    const el = id ? document.getElementById('msg-' + id) : null;
    if (!el) {
      this.avisos.info('El mensaje original es más antiguo', 'Carga los mensajes anteriores para verlo.');
      return;
    }
    el.scrollIntoView({ behavior: desplazamiento(), block: 'center' });
    this.resaltado.set(id!);
    setTimeout(() => this.resaltado.update((r) => (r === id ? null : r)), 1500);
  }

  // Gesto táctil: solo dedo o lápiz (con mouse se usa el botón, para no estorbar la selección de texto).
  // Un toque muestra las acciones del mensaje (responder, reportar); deslizar a la derecha responde.
  protected alTocar(e: PointerEvent, m: MensajeChatDto): void {
    if (e.pointerType === 'mouse' || !m.id || (e.target as Element | null)?.closest('button')) return;
    this.gesto = { mensaje: m, x: e.clientX, y: e.clientY, horizontal: null, vibro: false };
  }

  protected alMover(e: PointerEvent): void {
    const g = this.gesto;
    if (!g) return;
    const dx = e.clientX - g.x;
    const dy = e.clientY - g.y;
    if (g.horizontal === null && Math.hypot(dx, dy) > 8) {
      g.horizontal = Math.abs(dx) > Math.abs(dy) && dx > 0 && this.puedeResponder(g.mensaje);
      // El gesto sigue aunque el dedo salga de la fila
      if (g.horizontal) (e.currentTarget as Element | null)?.setPointerCapture?.(e.pointerId);
    }
    if (!g.horizontal) {
      if (g.horizontal === false) this.cancelarDeslizamiento();
      return;
    }
    const desplazamiento = Math.min(Math.max(dx, 0), this.UMBRAL * 1.4);
    if (desplazamiento >= this.UMBRAL && !g.vibro) {
      g.vibro = true;
      navigator.vibrate?.(12);
    }
    this.deslizamiento.set({ id: g.mensaje.id!, dx: desplazamiento });
  }

  protected alSoltar(): void {
    const g = this.gesto;
    const dx = this.deslizamiento()?.dx ?? 0;
    this.cancelarDeslizamiento();
    if (g && dx >= this.UMBRAL) this.responder(g.mensaje);
    else if (g && g.horizontal === null) this.accionesDe.update((a) => (a === g.mensaje.id ? null : g.mensaje.id!));
  }

  protected cancelarDeslizamiento(): void {
    this.gesto = null;
    this.deslizamiento.set(null);
  }

  protected denunciar(m: MensajeChatDto): void {
    this.aDenunciar.set(m.id ?? null);
    this.denunciaAbierta.set(true);
  }

  protected estado(e: string | undefined): string {
    return ETIQUETA_ESTADO_SOLICITUD[e ?? '']?.texto ?? e ?? '';
  }

  protected dia(fecha: string | undefined): string {
    return fecha ? new Date(fecha).toDateString() : '';
  }

  private bajar(): void {
    requestAnimationFrame(() => {
      const el = this.lista()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }
}
