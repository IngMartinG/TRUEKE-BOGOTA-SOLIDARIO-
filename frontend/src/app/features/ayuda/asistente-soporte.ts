import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { TIPOS_PQR, type TipoPqrDto } from '../../api/tipos';
import { ConfigService } from '../../core/config.service';
import { SesionService } from '../../core/sesion.service';
import { Icono } from '../../shared/ui/icono';
import { BorradorPqrs, escalarAPqrs, type TurnoConversacion } from './borrador-pqrs';
import { entradaPorId, type AccionAyuda, type EntradaConocimiento } from './conocimiento';
import { tipoParaEscalar } from './motor';
import { ProveedorRespuestas, type RespuestaAsistente, type TurnoHistorial } from './proveedor-respuestas';

interface Turno extends TurnoConversacion {
  id: number;
  cargando: boolean;
  entrada: EntradaConocimiento | null;
  texto: string;
  relacionadas: EntradaConocimiento[];
  tipoPqr: TipoPqrDto;
  ia?: boolean;
  enlaces?: AccionAyuda[];
  nota?: string;
}

/** Lo que ya se habló, para que la IA entienda preguntas como "¿y cómo lo hago?" (máximo 1.000 caracteres por turno). */
export function historialDe(turnos: readonly Pick<Turno, 'pregunta' | 'texto' | 'cargando'>[]): TurnoHistorial[] {
  return turnos
    .filter((t) => !t.cargando && t.texto.trim())
    .slice(-3)
    .flatMap((t): TurnoHistorial[] => [
      { rol: 'Usuario', texto: t.pregunta.slice(0, 1000) },
      { rol: 'Asistente', texto: t.texto.slice(0, 1000) },
    ]);
}

/** Para la PQRS: la pregunta frecuente con la que respondió la guía, o el comienzo de la respuesta de la IA. */
function respondioCon(r: RespuestaAsistente): string | null {
  if (r.entrada) return r.entrada.pregunta;
  if (!r.ia || !r.texto.trim()) return null;
  const t = r.texto.replace(/\s+/g, ' ').trim();
  return t.length > 160 ? `${t.slice(0, 157)}…` : t;
}

/** Preguntas que se ofrecen de entrada, según la sección donde está la persona. */
const SUGERIDAS: readonly [RegExp, readonly string[]][] = [
  [/^\/(eco-puntos|planes|pagos|cuenta\/facturacion)/, ['eco-puntos', 'pago-no-aplicado', 'retracto', 'renovacion']],
  [/^\/mensajes/, ['chulitos', 'bloquear', 'entregar-seguro', 'reportar']],
  [/^\/intercambios/, ['completar', 'no-concretado', 'entregar-seguro', 'reportar']],
  [/^\/(mis-publicaciones|publicacion)/, ['publicar', 'foto-perfil', 'impulsar-destacar', 'solicitud']],
  [/^\/cuenta/, ['proteger-cuenta', 'mis-datos', 'factura', 'apelar']],
  [/^/, ['costo', 'publicar', 'solicitud', 'eco-puntos', 'pago-no-aplicado', 'mis-datos']],
];

/** Donde el botón taparía la barra de acciones o la caja del chat, no se muestra. */
const SIN_LANZADOR = /^\/(mensajes\/[^/]+|publicar|publicacion\/[^/]+\/editar)(\/|$)/;

/**
 * Asistente de soporte: un panel flotante en todas las páginas que responde con la base de conocimiento, ofrece la
 * acción que resuelve el caso y, si no sirvió o no encontró nada, lleva a crear una PQRS con la conversación ya escrita.
 * Es un <dialog> modal: atrapa el foco, Esc lo cierra y el foco vuelve al botón que lo abrió.
 */
@Component({
  selector: 'app-asistente-soporte',
  imports: [FormsModule, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      --salida: cubic-bezier(0.23, 1, 0.32, 1);
    }
    /* Botón: entra con la página y se hunde un poco al tocarlo. */
    .lanzador {
      transition:
        transform 160ms var(--salida),
        opacity 160ms var(--salida),
        box-shadow 200ms ease;
    }
    .lanzador:active {
      transform: scale(0.96);
    }
    .lanzador[data-abierto='true'] {
      opacity: 0;
      transform: scale(0.9);
      pointer-events: none;
    }

    /* Panel: crece desde el botón (esquina inferior derecha) en 220 ms y se va en 150 ms. En celular sube como hoja. */
    dialog {
      opacity: 0;
      transform: translateY(12px) scale(0.98);
      transform-origin: bottom right;
      transition:
        opacity 150ms ease-out,
        transform 150ms ease-out,
        overlay 150ms allow-discrete,
        display 150ms allow-discrete;
    }
    dialog[open] {
      opacity: 1;
      transform: none;
      transition-duration: 220ms;
      transition-timing-function: var(--salida);
    }
    @starting-style {
      dialog[open] {
        opacity: 0;
        transform: translateY(16px) scale(0.98);
      }
    }
    dialog::backdrop {
      background: rgb(10 36 23 / 0.28);
      opacity: 0;
      transition:
        opacity 150ms,
        overlay 150ms allow-discrete,
        display 150ms allow-discrete;
    }
    dialog[open]::backdrop {
      opacity: 1;
    }
    @starting-style {
      dialog[open]::backdrop {
        opacity: 0;
      }
    }
    @media (min-width: 640px) {
      dialog::backdrop {
        background: transparent;
      }
    }

    .chip {
      display: inline-flex;
      align-items: center;
      min-height: 2.25rem;
      padding: 0.375rem 0.875rem;
      border: 1px solid var(--color-borde);
      border-radius: 9999px;
      background: var(--color-superficie);
      font-size: 0.8125rem;
      font-weight: 600;
      line-height: 1.25;
      text-align: left;
      transition:
        background-color 150ms ease,
        border-color 150ms ease,
        transform 120ms var(--salida);
    }
    .chip:active {
      transform: scale(0.97);
    }
    .chip:focus-visible {
      outline: 2px solid var(--color-bosque-500);
      outline-offset: 2px;
    }
    @media (hover: hover) and (pointer: fine) {
      .chip:hover {
        border-color: var(--color-bosque-400);
        background: color-mix(in srgb, var(--color-bosque-500) 10%, var(--color-superficie));
      }
    }
    @media (pointer: coarse) {
      .chip {
        min-height: 2.75rem;
      }
    }

    /* Cada mensaje nuevo aparece en su lugar (sin empujar lo anterior). */
    .burbuja {
      animation: entrar 200ms var(--salida) both;
    }
    @keyframes entrar {
      from {
        opacity: 0;
        transform: translateY(6px);
      }
    }
    .punto {
      animation: latido 1s ease-in-out infinite;
    }
    .punto:nth-child(2) {
      animation-delay: 150ms;
    }
    .punto:nth-child(3) {
      animation-delay: 300ms;
    }
    @keyframes latido {
      50% {
        opacity: 0.25;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      dialog,
      dialog[open] {
        transform: none;
      }
      @starting-style {
        dialog[open] {
          transform: none;
        }
      }
      .burbuja {
        animation-name: aparecer-quieto;
      }
      .punto {
        animation: none;
      }
      @keyframes aparecer-quieto {
        from {
          opacity: 0;
        }
      }
    }
  `,
  template: `
    @if (conLanzador()) {
      <button
        #lanzador
        type="button"
        class="lanzador fixed right-4 bottom-[calc(5.25rem+env(safe-area-inset-bottom))] z-40 flex items-center gap-2 rounded-full bg-bosque-700 py-3 pr-4 pl-3.5 font-semibold text-white shadow-[0_10px_28px_-10px_rgb(10_36_23/0.7)] ring-1 ring-white/10 hover:bg-bosque-800 focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-sol-400 lg:right-6 lg:bottom-6 dark:bg-bosque-600 dark:hover:bg-bosque-500"
        aria-haspopup="dialog"
        [attr.aria-expanded]="abierto()"
        [attr.data-abierto]="abierto()"
        (click)="abrir()"
      >
        <app-icono nombre="soporte" [tamano]="20" />
        <span class="text-sm">Ayuda</span>
      </button>
    }

    <!-- Esc y el foco los maneja <dialog>; el clic solo detecta el fondo. -->
    <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
    <dialog
      #dialogo
      class="m-0 mt-auto h-[min(85dvh,40rem)] max-h-none w-full max-w-none overflow-hidden rounded-t-3xl border border-borde bg-superficie p-0 text-tinta shadow-elevada sm:mt-auto sm:mr-6 sm:mb-6 sm:ml-auto sm:h-[min(80dvh,38rem)] sm:w-[25rem] sm:rounded-3xl"
      aria-labelledby="asistente-titulo"
      aria-describedby="asistente-descripcion"
      (close)="alCerrar()"
      (click)="clicFondo($event)"
    >
      <div class="flex h-full flex-col">
        <header class="flex items-center gap-3 border-b border-borde px-4 py-3">
          <span class="grid size-10 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200">
            <app-icono nombre="brote" [tamano]="22" />
          </span>
          <div class="min-w-0 flex-1">
            <h2 id="asistente-titulo" class="font-bold leading-tight">Asistente de ayuda</h2>
            <p id="asistente-descripcion" class="text-xs text-tenue">
              {{ conIa() ? 'Con inteligencia artificial · no escribas datos personales' : 'Respuestas de la guía de Trueke, al instante' }}
            </p>
          </div>
          <button type="button" class="btn-icono -mr-1" (click)="cerrar()" aria-label="Cerrar el asistente">
            <app-icono nombre="x" />
          </button>
        </header>

        <div #historial class="flex-1 space-y-4 overflow-y-auto overscroll-contain px-4 py-4" role="log" aria-live="polite" aria-relevant="additions" tabindex="-1">
          <div class="burbuja max-w-[88%] rounded-2xl rounded-tl-md bg-superficie-2 px-3.5 py-2.5 text-sm">
            ¡Hola{{ nombre() ? ', ' + nombre() : '' }}! Cuéntame qué necesitas o elige un tema.
          </div>
          @if (!turnos().length) {
            <div class="burbuja" role="group" aria-labelledby="asistente-temas">
              <p id="asistente-temas" class="mb-2 text-xs font-semibold tracking-wide text-tenue uppercase">Temas frecuentes</p>
              <ul class="flex flex-wrap gap-2">
                @for (e of sugeridas(); track e.id) {
                  <li>
                    <button type="button" class="chip" (click)="elegir(e)">{{ e.pregunta }}</button>
                  </li>
                }
              </ul>
            </div>
          }

          @for (t of turnos(); track t.id; let ultimo = $last) {
            <p class="burbuja ml-auto w-fit max-w-[85%] rounded-2xl rounded-tr-md bg-bosque-700 px-3.5 py-2.5 text-sm break-words text-white dark:bg-bosque-600">
              <span class="sr-only">Tú: </span>{{ t.pregunta }}
            </p>
            @if (t.cargando) {
              <p class="burbuja flex w-fit gap-1 rounded-2xl rounded-tl-md bg-superficie-2 px-4 py-3" role="status" aria-label="Buscando la respuesta…">
                <span class="punto size-1.5 rounded-full bg-tenue"></span><span class="punto size-1.5 rounded-full bg-tenue"></span><span class="punto size-1.5 rounded-full bg-tenue"></span>
              </p>
            } @else {
              <div class="burbuja max-w-[92%] space-y-3 rounded-2xl rounded-tl-md bg-superficie-2 px-3.5 py-3 text-sm">
                <span class="sr-only">Asistente: </span>
                @if (t.nota) {
                  <p class="text-xs text-tenue">{{ t.nota }}</p>
                }
                @if (t.ia) {
                  <p class="leading-relaxed break-words whitespace-pre-line">{{ t.texto }}</p>
                  @if (t.enlaces?.length) {
                    <div class="flex flex-wrap gap-2">
                      @for (a of t.enlaces; track a.ruta) {
                        <button type="button" class="btn btn-secundario btn-sm" (click)="irA(a)">
                          {{ a.texto }}<app-icono nombre="derecha" [tamano]="14" />
                        </button>
                      }
                    </div>
                  }
                  @if (t.sirvio === null) {
                    <div class="flex flex-wrap items-center gap-2 border-t border-borde pt-3" role="group" aria-label="¿Te sirvió la respuesta?">
                      <span class="mr-1 text-xs font-semibold text-tenue">¿Te sirvió?</span>
                      <button type="button" class="chip" (click)="valorar(t, true)">Sí</button>
                      <button type="button" class="chip" (click)="valorar(t, false)">No</button>
                    </div>
                  } @else if (t.sirvio) {
                    <p class="border-t border-borde pt-3 text-xs text-tenue">¡Qué bien! Si necesitas algo más, aquí estoy.</p>
                  }
                } @else if (t.entrada; as e) {
                  <p class="font-semibold">{{ e.pregunta }}</p>
                  <p class="leading-relaxed text-tenue">{{ t.texto }}</p>
                  @if (e.accion; as a) {
                    <button type="button" class="btn btn-secundario btn-sm" (click)="irA(a)">
                      {{ a.texto }}<app-icono nombre="derecha" [tamano]="14" />
                    </button>
                  }
                  @if (t.sirvio === null) {
                    <div class="flex flex-wrap items-center gap-2 border-t border-borde pt-3" role="group" [attr.aria-label]="'¿Te sirvió la respuesta a ' + e.pregunta + '?'">
                      <span class="mr-1 text-xs font-semibold text-tenue">¿Te sirvió?</span>
                      <button type="button" class="chip" (click)="valorar(t, true)">Sí</button>
                      <button type="button" class="chip" (click)="valorar(t, false)">No</button>
                    </div>
                  } @else if (t.sirvio) {
                    <p class="border-t border-borde pt-3 text-xs text-tenue">¡Qué bien! Si necesitas algo más, aquí estoy.</p>
                  }
                } @else {
                  <p class="leading-relaxed">No encontré una respuesta para eso en la guía. Puedes escribirlo de otra forma o pedirle al equipo que lo revise.</p>
                }
                @if (t.relacionadas.length && (t.sirvio === false || (!t.entrada && !t.ia))) {
                  <div>
                    <p class="mb-1.5 text-xs font-semibold text-tenue">Quizá buscabas</p>
                    <ul class="flex flex-wrap gap-2">
                      @for (r of t.relacionadas; track r.id) {
                        <li><button type="button" class="chip" (click)="elegir(r)">{{ r.pregunta }}</button></li>
                      }
                    </ul>
                  </div>
                }
              </div>
              @if (ultimo && (t.sirvio === false || (!t.entrada && !t.ia))) {
                <div class="burbuja rounded-2xl border border-sol-500/40 bg-sol-50 p-4 text-sm dark:bg-sol-600/10">
                  <p class="flex items-center gap-2 font-semibold"><app-icono nombre="soporte" [tamano]="18" class="text-sol-600 dark:text-sol-300" />Lo revisa una persona del equipo</p>
                  <p class="mt-1 text-tenue">
                    Crea una PQRS ({{ etiquetaTipo(t.tipoPqr) }}) y te respondemos por escrito. Llevamos esta conversación para que no tengas que repetirla.
                  </p>
                  <button type="button" class="btn btn-primario btn-sm mt-3" (click)="crearPqrs(t.tipoPqr)">
                    <app-icono nombre="enviar" [tamano]="14" />Crear PQRS
                  </button>
                </div>
              }
            }
          }
        </div>

        <form class="flex items-center gap-2 border-t border-borde p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))] sm:pb-3" (ngSubmit)="preguntar()">
          <label for="asistente-pregunta" class="sr-only">Escribe tu pregunta</label>
          <input
            #entrada
            id="asistente-pregunta"
            class="entrada flex-1 rounded-full"
            name="pregunta"
            placeholder="Ej.: no veo mi recarga…"
            autocomplete="off"
            enterkeyhint="send"
            maxlength="300"
            [ngModel]="texto()"
            (ngModelChange)="texto.set($event)"
          />
          <button type="submit" class="btn btn-primario size-11 shrink-0 rounded-full p-0" [disabled]="!texto().trim()" aria-label="Enviar pregunta">
            <app-icono nombre="enviar" [tamano]="18" />
          </button>
        </form>
      </div>
    </dialog>
  `,
})
export class AsistenteSoporte {
  private readonly proveedor = inject(ProveedorRespuestas);
  private readonly borrador = inject(BorradorPqrs);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionService);
  private readonly config = inject(ConfigService);
  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');
  private readonly lanzador = viewChild<ElementRef<HTMLButtonElement>>('lanzador');
  private readonly historial = viewChild.required<ElementRef<HTMLElement>>('historial');
  private readonly entrada = viewChild.required<ElementRef<HTMLInputElement>>('entrada');

  protected readonly abierto = signal(false);
  protected readonly texto = signal('');
  protected readonly turnos = signal<Turno[]>([]);
  private siguiente = 0;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects.split(/[?#]/)[0]),
    ),
    { initialValue: this.router.url.split(/[?#]/)[0] },
  );
  protected readonly conLanzador = computed(() => !SIN_LANZADOR.test(this.url()));
  protected readonly sugeridas = computed(() => {
    const ids = SUGERIDAS.find(([re]) => re.test(this.url()))?.[1] ?? [];
    return ids.map(entradaPorId).filter((e): e is EntradaConocimiento => !!e);
  });
  protected readonly conIa = computed(() => this.config.asistenteIa() && this.sesion.autenticado() && this.sesion.correoVerificado());
  protected readonly nombre = computed(() => this.sesion.usuario()?.nombreCompleto?.split(' ')[0] ?? '');

  protected abrir(): void {
    const d = this.dialogo().nativeElement;
    if (d.open) return;
    d.showModal();
    this.abierto.set(true);
    // Con mouse se escribe de una vez; en el celular no se abre el teclado encima de los temas sugeridos.
    if (matchMedia('(pointer: fine)').matches) this.entrada().nativeElement.focus();
    else this.historial().nativeElement.focus();
  }

  protected cerrar(): void {
    this.dialogo().nativeElement.close();
  }

  protected alCerrar(): void {
    this.abierto.set(false);
    this.lanzador()?.nativeElement.focus();
  }

  protected clicFondo(e: MouseEvent): void {
    if (e.target === this.dialogo().nativeElement) this.cerrar();
  }

  protected preguntar(): void {
    const pregunta = this.texto().trim();
    if (!pregunta) return;
    this.texto.set('');
    const historial = historialDe(this.turnos());
    void this.responder(pregunta, () => this.proveedor.responder(pregunta, historial));
  }

  protected elegir(e: EntradaConocimiento): void {
    void this.responder(e.pregunta, () => this.proveedor.responderEntrada(e));
  }

  private async responder(pregunta: string, obtener: () => Promise<RespuestaAsistente>): Promise<void> {
    const id = this.siguiente++;
    this.turnos.update((ts) => [
      ...ts,
      { id, pregunta, cargando: true, entrada: null, texto: '', relacionadas: [], tipoPqr: 'Peticion', respondio: null, sirvio: null },
    ]);
    this.bajar();
    const r = await obtener();
    this.turnos.update((ts) =>
      ts.map((t) => (t.id === id ? { ...t, ...r, cargando: false, respondio: respondioCon(r) } : t)),
    );
    this.bajar();
  }

  protected valorar(t: Turno, sirvio: boolean): void {
    this.turnos.update((ts) => ts.map((x) => (x.id === t.id ? { ...x, sirvio } : x)));
    this.bajar();
  }

  protected irA(a: AccionAyuda): void {
    this.cerrar();
    if (a.conSesion && !this.sesion.autenticado()) {
      void this.router.navigate(['/ingresar'], { queryParams: { volver: a.ruta } });
      return;
    }
    void this.router.navigate([a.ruta], { fragment: a.fragmento });
  }

  /** Con sesión (y correo confirmado) va a Soporte de la cuenta; si no, a la PQRS pública. El texto viaja en memoria. */
  protected crearPqrs(tipo: TipoPqrDto): void {
    const conSesion = this.sesion.autenticado() && this.sesion.correoVerificado();
    const e = escalarAPqrs(this.turnos(), tipo, conSesion);
    this.borrador.guardar(e.borrador);
    this.cerrar();
    void this.router.navigate([e.ruta], { queryParams: { tipo: e.tipo } });
  }

  protected etiquetaTipo(tipo: TipoPqrDto): string {
    const conSesion = this.sesion.autenticado() && this.sesion.correoVerificado();
    const final = tipoParaEscalar(tipo, conSesion);
    return (TIPOS_PQR.find((t) => t.valor === final)?.etiqueta ?? 'Petición').toLowerCase();
  }

  /**
   * Lleva la vista al último mensaje después de que se pinta. Sin desplazamiento suave: los botones no se mueven bajo el
   * dedo mientras se tocan, y la entrada de cada burbuja ya da la continuidad.
   */
  private bajar(): void {
    setTimeout(() => {
      const h = this.historial().nativeElement;
      h.scrollTop = h.scrollHeight;
    });
  }
}
