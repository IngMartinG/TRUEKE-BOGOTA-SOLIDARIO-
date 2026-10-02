import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETIQUETA_ESTADO_SOLICITUD, type MensajeChatDto } from '../../api/tipos';
import { IntercambiosApi } from '../../core/api/intercambios.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { FechaPipe, HacePipe, HoraPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Denunciar } from '../../shared/ui/denunciar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';

const TAMANO = 40;

@Component({
  imports: [FormsModule, RouterLink, Avatar, Icono, EstadoVacio, Denunciar, HacePipe, HoraPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-4 sm:py-8">
      <div class="tarjeta grid h-[calc(100dvh-10rem)] min-h-[28rem] overflow-hidden sm:h-[calc(100dvh-9rem)] md:grid-cols-[20rem_1fr]">
        <!-- Lista de conversaciones -->
        <aside class="min-h-0 flex-col border-r border-borde" [class]="id() ? 'hidden md:flex' : 'flex'" aria-label="Conversaciones">
          <header class="flex items-center justify-between border-b border-borde px-5 py-4">
            <h1 class="text-xl font-extrabold">Mensajes</h1>
            @if (!tiempoReal.conectado()) {
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
                  <app-avatar [nombre]="c.contraparte?.nombre" [tamano]="44" [verificado]="!!c.contraparte?.verificado" />
                  <div class="min-w-0 flex-1">
                    <div class="flex items-baseline justify-between gap-2">
                      <p class="truncate font-semibold">{{ c.contraparte?.nombre }}</p>
                      <span class="shrink-0 text-[11px] text-tenue">{{ c.ultimoMensajeUtc | hace }}</span>
                    </div>
                    <p class="truncate text-xs font-medium text-bosque-700 dark:text-bosque-300">{{ c.publicacionTitulo }}</p>
                    <div class="flex items-center justify-between gap-2">
                      <p class="truncate text-sm" [class]="c.noLeidos ? 'font-semibold text-tinta' : 'text-tenue'">{{ c.ultimoMensaje || 'Sin mensajes todavía' }}</p>
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
        <section class="min-h-0 flex-col" [class]="id() ? 'flex' : 'hidden md:flex'" aria-live="polite">
          @if (!id()) {
            <div class="grid flex-1 place-items-center">
              <app-estado-vacio icono="mensaje" titulo="Elige una conversación" descripcion="Aquí coordinas la entrega sin compartir tu número ni tu correo." />
            </div>
          } @else {
            <header class="flex items-center gap-3 border-b border-borde px-4 py-3">
              <a routerLink="/mensajes" class="btn-icono md:hidden" aria-label="Volver a conversaciones"><app-icono nombre="izquierda" /></a>
              @if (actual(); as c) {
                <a [routerLink]="['/usuarios', c.contraparte?.id]" class="flex min-w-0 flex-1 items-center gap-3">
                  <app-avatar [nombre]="c.contraparte?.nombre" [tamano]="40" [verificado]="!!c.contraparte?.verificado" />
                  <div class="min-w-0">
                    <p class="truncate font-semibold">{{ c.contraparte?.nombre }}</p>
                    <p class="truncate text-xs text-tenue">{{ c.publicacionTitulo }} · {{ estado(c.estadoSolicitud) }}</p>
                  </div>
                </a>
                <a [routerLink]="['/publicacion', c.publicacionId]" class="btn btn-secundario btn-sm hidden sm:inline-flex">Ver publicación</a>
                <a routerLink="/intercambios" [queryParams]="{ tab: c.soyDuenio ? 'recibidas' : 'enviadas' }" class="btn btn-primario btn-sm hidden sm:inline-flex">Intercambio</a>
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
                <div class="group flex" [class.justify-end]="m.esMio">
                  <div class="max-w-[80%] rounded-2xl px-3.5 py-2 text-sm shadow-sm"
                    [class]="m.esMio ? 'rounded-br-md bg-bosque-600 text-white' : 'rounded-bl-md bg-superficie text-tinta'">
                    @if (m.oculto) {
                      <p class="italic opacity-70">Mensaje oculto por moderación</p>
                    } @else {
                      <p class="break-words whitespace-pre-line">{{ m.texto }}</p>
                    }
                    <p class="mt-0.5 flex items-center justify-end gap-1 text-[10px]" [class]="m.esMio ? 'text-white/70' : 'text-tenue'">
                      {{ m.fechaUtc | hora }}
                      @if (m.esMio) {
                        <app-icono [nombre]="m.leido ? 'checkCirculo' : 'check'" [tamano]="11" [etiqueta]="m.leido ? 'Leído' : 'Enviado'" />
                      }
                    </p>
                  </div>
                  @if (!m.esMio && !m.oculto) {
                    <button type="button" class="ml-1 self-center text-[11px] text-tenue opacity-0 transition group-hover:opacity-100 focus:opacity-100 hover:text-tierra-600" (click)="denunciar(m)">Reportar</button>
                  }
                </div>
              } @empty {
                @if (!cargando()) {
                  <div class="py-12 text-center">
                    <p class="font-semibold">¡Rompe el hielo! 👋</p>
                    <p class="mt-1 text-sm text-tenue">Propón un lugar público y un horario para la entrega.</p>
                  </div>
                }
              }
            </div>

            @if (actual()?.escribible === false) {
              <p class="border-t border-borde px-4 py-4 text-center text-sm text-tenue">Esta conversación está cerrada porque el intercambio terminó.</p>
            } @else {
              <form class="flex items-end gap-2 border-t border-borde p-3" (ngSubmit)="enviar()">
                <label for="nuevo-mensaje" class="sr-only">Escribe un mensaje</label>
                <textarea id="nuevo-mensaje" name="texto" rows="1" class="entrada max-h-32 min-h-11 resize-none rounded-2xl py-2.5" maxlength="1000"
                  [(ngModel)]="texto" (keydown.enter)="alPresionarEnter($event)" placeholder="Escribe un mensaje…"></textarea>
                <button type="submit" class="grid size-11 shrink-0 place-items-center rounded-full bg-bosque-600 text-white transition hover:bg-bosque-700 disabled:opacity-50"
                  [disabled]="!texto().trim() || enviando()" aria-label="Enviar">
                  <app-icono nombre="enviar" [tamano]="18" />
                </button>
              </form>
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

  readonly id = input<string>();
  private readonly lista = viewChild<ElementRef<HTMLElement>>('lista');

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

  constructor() {
    effect((alLimpiar) => {
      const id = this.id() ?? null;
      untracked(() => {
        this.tiempoReal.conversacionAbierta.set(id);
        if (id) void this.abrir(id);
      });
      alLimpiar(() => this.tiempoReal.conversacionAbierta.set(null));
    });

    this.tiempoReal.mensaje$.pipe(takeUntilDestroyed()).subscribe((m) => {
      if (m.conversacionId === this.id()) {
        if (!this.mensajes().some((x) => x.id === m.id)) {
          this.mensajes.update((l) => [...l, m]);
          this.bajar();
        }
        if (!m.esMio) void firstValueFrom(this.api.marcarLeida(m.conversacionId!)).catch(() => {});
      }
      this.conversaciones.reload();
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
      const m = await firstValueFrom(this.api.enviarMensaje(id, texto));
      this.texto.set('');
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
