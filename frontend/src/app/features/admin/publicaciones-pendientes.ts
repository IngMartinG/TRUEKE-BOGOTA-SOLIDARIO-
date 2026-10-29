import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { infoCondicion, type PublicacionEnRevisionDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { CopPipe, HacePipe, NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

/** Motivos frecuentes: el dueño los lee tal cual y sabe qué corregir. */
const MOTIVOS = [
  'Es un artículo prohibido por los términos (armas, medicamentos, animales vivos, réplicas…).',
  'Incluye teléfono, correo o enlaces: el contacto se hace por el chat de Trueke.',
  'Las fotos o la descripción no muestran el objeto que se entrega.',
] as const;

const KBD = 'rounded-md border border-current/30 px-1.5 py-0.5 font-sans text-xs font-semibold opacity-80';

/**
 * Publicaciones que el evaluador de riesgo (o varias denuncias) dejó esperando. Se revisan de a una con el teclado
 * (A aprueba, R rechaza, flechas para moverse, X marca) y las marcadas se aprueban juntas: lo normal es aprobar.
 */
@Component({
  imports: [FormsModule, Avatar, Icono, Modal, EstadoVacio, CopPipe, HacePipe, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown)': 'tecla($event)' },
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Publicaciones en revisión</h2>
        <p class="mt-1 max-w-prose text-sm text-tenue">
          Solo llegan aquí las de riesgo: primeras publicaciones de una cuenta, contacto o enlaces en el texto, posibles
          artículos prohibidos o varias denuncias. Las demás se publican al instante.
        </p>
      </div>
      @if (recurso.value(); as r) {
        <p class="text-sm text-tenue" aria-live="polite">
          <strong class="text-tinta">{{ pendientesEnVivo() ?? r.total | numero }}</strong> por revisar
          @if (hayNuevas()) {
            · <button type="button" class="enlace" (click)="recargar()">cargar las nuevas</button>
          }
        </p>
      }
    </div>

    @if (actual(); as p) {
      <div class="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <article class="tarjeta overflow-hidden" [attr.aria-label]="'Publicación ' + (indice() + 1) + ' de ' + items().length">
          <div class="grid gap-0 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
            <div class="bg-superficie-2">
              @if (fotoGrande(); as foto) {
                <img [src]="foto" [alt]="'Foto de ' + p.titulo" width="480" height="480" class="aspect-square w-full object-contain" referrerpolicy="no-referrer" />
              } @else {
                <div class="grid aspect-square place-items-center text-sm text-tenue">Sin fotos</div>
              }
              @if ((p.imagenes?.length ?? 0) > 1) {
                <ul class="flex gap-2 p-3" aria-label="Fotos de la publicación">
                  @for (img of p.imagenes; track img; let i = $index) {
                    <li>
                      <button type="button" class="block size-12 overflow-hidden rounded-lg border-2 transition"
                        [class]="i === foto() ? 'border-bosque-500' : 'border-transparent opacity-70 hover:opacity-100'"
                        [attr.aria-label]="'Ver la foto ' + (i + 1)" [attr.aria-current]="i === foto() ? 'true' : null" (click)="foto.set(i)">
                        <img [src]="img" alt="" width="48" height="48" loading="lazy" class="size-full object-cover" referrerpolicy="no-referrer" />
                      </button>
                    </li>
                  }
                </ul>
              }
            </div>

            <div class="flex flex-col gap-4 p-5">
              <div>
                <p class="flex flex-wrap items-center gap-1.5 text-xs">
                  <span class="insignia-neutra">{{ modos[p.modo ?? ''] ?? p.modo }}</span>
                  <span class="insignia-neutra">{{ p.categoria }}</span>
                  <span class="insignia-neutra">{{ condicion(p) }}</span>
                </p>
                <h3 class="mt-2 font-display text-lg font-bold text-balance">{{ p.titulo }}</h3>
                <p class="text-sm text-tenue">
                  {{ p.localidad }}, {{ p.municipio }}
                  @if (p.precioReferenciaCop) {
                    · <strong class="text-tinta">{{ p.precioReferenciaCop | cop }}</strong>
                    @if (p.aceptaTrueke) { · acepta trueke }
                  }
                </p>
              </div>
              <p class="max-h-48 overflow-y-auto text-sm whitespace-pre-line">{{ p.descripcion }}</p>
              @if (p.detalleCondicion) {
                <p class="text-sm"><span class="font-semibold">Estado:</span> {{ p.detalleCondicion }}</p>
              }
            </div>
          </div>
        </article>

        <aside class="flex flex-col gap-4">
          <section class="tarjeta p-5" aria-labelledby="senales">
            <div class="flex items-center justify-between gap-2">
              <h3 id="senales" class="font-semibold">Por qué está aquí</h3>
              <span [class]="nivel(p).clase">{{ nivel(p).texto }}</span>
            </div>
            <ul class="mt-3 space-y-2 text-sm">
              @for (m of p.motivos; track m) {
                <li class="flex gap-2"><app-icono nombre="alerta" [tamano]="16" class="mt-0.5 shrink-0 text-tierra-600 dark:text-tierra-500" />{{ m }}</li>
              }
            </ul>
            <p class="mt-3 text-xs text-tenue">En revisión {{ p.desdeUtc | hace }}</p>
          </section>

          @if (p.autor; as a) {
            <section class="tarjeta flex items-center gap-3 p-4" aria-label="Quién publica">
              @if (a.fotoUrl) {
                <img [src]="a.fotoUrl" alt="" width="44" height="44" class="size-11 shrink-0 rounded-full object-cover" referrerpolicy="no-referrer" />
              } @else {
                <app-avatar [nombre]="a.nombreCompleto" [tamano]="44" />
              }
              <div class="min-w-0">
                <p class="truncate font-semibold">{{ a.nombreCompleto }}</p>
                <p class="text-xs text-tenue">
                  Registrada {{ a.fechaRegistro | hace }} ·
                  @if (a.identidadVerificada) { <span class="text-bosque-700 dark:text-bosque-300">identidad verificada</span> } @else { sin verificar }
                </p>
              </div>
            </section>
          }

          <div class="grid grid-cols-2 gap-2">
            <button type="button" class="btn btn-primario" (click)="aprobar([p])" [disabled]="ocupado()">
              <app-icono nombre="check" [tamano]="16" />Aprobar <kbd [class]="kbd">A</kbd>
            </button>
            <button type="button" class="btn btn-peligro" (click)="abrirRechazo()" [disabled]="ocupado()">
              Rechazar <kbd [class]="kbd">R</kbd>
            </button>
          </div>
          <p class="text-xs text-tenue">
            <kbd [class]="kbd">↑</kbd> <kbd [class]="kbd">↓</kbd> para moverte · <kbd [class]="kbd">X</kbd> marca para aprobar varias juntas.
          </p>
        </aside>
      </div>

      <section class="mt-8" aria-labelledby="fila">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h3 id="fila" class="font-semibold">Fila ({{ items().length }}) <span class="text-sm font-normal text-tenue">· primero las de más riesgo</span></h3>
          <div class="flex items-center gap-2">
            <label class="flex cursor-pointer items-center gap-2 text-sm">
              <input type="checkbox" class="accent-bosque-600" [checked]="todasMarcadas()" (change)="marcarTodas($any($event.target).checked)" />
              Marcar todas
            </label>
            <button type="button" class="btn btn-primario btn-sm" [disabled]="!marcadas().size || ocupado()" (click)="aprobarMarcadas()">
              <app-icono nombre="check" [tamano]="14" />Aprobar marcadas ({{ marcadas().size }})
            </button>
          </div>
        </div>
        <ul class="mt-3 divide-y divide-borde overflow-hidden rounded-2xl border border-borde bg-superficie">
          @for (s of items(); track s.id; let i = $index) {
            <li class="flex items-center gap-3 px-3 py-2 transition" [class]="i === indice() ? 'bg-bosque-50 dark:bg-bosque-900/40' : ''">
              <input type="checkbox" class="accent-bosque-600" [checked]="marcadas().has(s.id!)" (change)="alternar(s.id!)"
                [attr.aria-label]="'Marcar ' + s.titulo" />
              <button type="button" class="flex min-w-0 flex-1 items-center gap-3 text-left" (click)="ir(i)" [attr.aria-current]="i === indice() ? 'true' : null">
                @if (s.imagenes?.[0]; as img) {
                  <img [src]="img" alt="" width="40" height="40" loading="lazy" class="size-10 shrink-0 rounded-lg object-cover" referrerpolicy="no-referrer" />
                } @else {
                  <span class="size-10 shrink-0 rounded-lg bg-superficie-2"></span>
                }
                <span class="min-w-0 flex-1">
                  <span class="block truncate text-sm font-semibold">{{ s.titulo }}</span>
                  <span class="block truncate text-xs text-tenue">{{ s.motivos?.[0] }}</span>
                </span>
                <span [class]="nivel(s).clase">{{ nivel(s).texto }}</span>
              </button>
            </li>
          }
        </ul>
      </section>
    } @else if (!recurso.isLoading()) {
      <app-estado-vacio icono="check" titulo="No hay publicaciones por revisar" />
    }

    <app-modal [(abierto)]="rechazoAbierto" titulo="Rechazar publicación" subtitulo="El dueño leerá el motivo y podrá corregirla: al editarla vuelve a esta fila.">
      <form id="form-rechazo-pub" (ngSubmit)="rechazar()" class="space-y-3">
        <fieldset class="space-y-2">
          <legend class="etiqueta mb-2">Motivo</legend>
          @for (m of motivos; track m) {
            <label class="flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition"
              [class]="motivo() === m ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/40' : 'border-borde hover:bg-superficie-2'">
              <input type="radio" name="motivo-pub" class="accent-bosque-600" [value]="m" [ngModel]="motivo()" (ngModelChange)="motivo.set($event)" />
              <span class="text-sm">{{ m }}</span>
            </label>
          }
        </fieldset>
        <div class="campo">
          <label for="motivo-pub-otro" class="etiqueta">O escribe otro motivo</label>
          <textarea id="motivo-pub-otro" name="otro" class="entrada" maxlength="300" [ngModel]="motivo()" (ngModelChange)="motivo.set($event)"></textarea>
        </div>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="rechazoAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-rechazo-pub" class="btn btn-peligro" [disabled]="motivo().trim().length < 3 || ocupado()">Rechazar</button>
      </div>
    </app-modal>
  `,
})
export default class PublicacionesPendientes {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  private readonly tiempoReal = inject(TiempoRealService);
  protected readonly kbd = KBD;
  protected readonly motivos = MOTIVOS;
  protected readonly modos: Record<string, string> = { Trueke: 'Trueke', Compra: 'Venta', Donacion: 'Donación' };
  protected readonly recurso = rxResource({ stream: () => this.api.publicacionesEnRevision() });
  /** Las ya resueltas salen de la lista al instante, sin esperar a recargar. */
  private readonly resueltas = signal<ReadonlySet<string>>(new Set());
  protected readonly items = computed(() => (this.recurso.value()?.items ?? []).filter((p) => !this.resueltas().has(p.id ?? '')));
  protected readonly indice = signal(0);
  protected readonly actual = computed<PublicacionEnRevisionDto | undefined>(() => {
    const lista = this.items();
    return lista[Math.min(this.indice(), lista.length - 1)];
  });
  protected readonly foto = signal(0);
  protected readonly fotoGrande = computed(() => this.actual()?.imagenes?.[this.foto()] ?? this.actual()?.imagenes?.[0]);
  protected readonly marcadas = signal<ReadonlySet<string>>(new Set());
  protected readonly todasMarcadas = computed(() => this.items().length > 0 && this.items().every((p) => this.marcadas().has(p.id!)));
  protected readonly ocupado = signal(false);
  protected readonly rechazoAbierto = signal(false);
  protected readonly motivo = signal('');
  /** El contador llega en vivo; si sube por encima de lo cargado, se ofrece traer las nuevas. */
  protected readonly pendientesEnVivo = this.tiempoReal.colaModeracion;
  protected readonly hayNuevas = computed(() => (this.pendientesEnVivo() ?? 0) > this.items().length && this.items().length < 100);

  constructor() {
    effect(() => {
      this.actual(); // al cambiar de publicación se vuelve a la primera foto
      this.foto.set(0);
    });
  }

  protected condicion = (p: PublicacionEnRevisionDto) => infoCondicion(p.condicion).etiqueta;

  protected nivel(p: PublicacionEnRevisionDto): { texto: string; clase: string } {
    return (p.puntajeRiesgo ?? 0) >= 100
      ? { texto: 'Riesgo alto', clase: 'insignia bg-tierra-100 text-tierra-700 dark:bg-tierra-700/30 dark:text-tierra-100' }
      : { texto: 'Riesgo medio', clase: 'insignia-sol' };
  }

  protected ir(i: number): void {
    this.indice.set(i);
  }

  protected alternar(id: string): void {
    this.marcadas.update((s) => {
      const n = new Set(s);
      if (!n.delete(id)) n.add(id);
      return n;
    });
  }

  protected marcarTodas(si: boolean): void {
    this.marcadas.set(si ? new Set(this.items().map((p) => p.id!)) : new Set());
  }

  protected recargar(): void {
    this.resueltas.set(new Set());
    this.recurso.reload();
  }

  protected tecla(e: KeyboardEvent): void {
    if (this.rechazoAbierto() || this.ocupado() || e.ctrlKey || e.metaKey || e.altKey) return;
    // Escribiendo en un campo, las letras son texto y no atajos.
    if (e.target instanceof Element && e.target.closest('input, textarea, select, [contenteditable="true"]')) return;
    const p = this.actual();
    if (!p) return;
    switch (e.key.toLowerCase()) {
      case 'a':
        e.preventDefault();
        void this.aprobar([p]);
        break;
      case 'r':
        e.preventDefault();
        this.abrirRechazo();
        break;
      case 'x':
        e.preventDefault();
        this.alternar(p.id!);
        break;
      case 'arrowdown':
      case 'arrowright':
        e.preventDefault();
        this.indice.update((i) => Math.min(i + 1, this.items().length - 1));
        break;
      case 'arrowup':
      case 'arrowleft':
        e.preventDefault();
        this.indice.update((i) => Math.max(i - 1, 0));
        break;
    }
  }

  protected aprobarMarcadas(): Promise<void> {
    return this.aprobar(this.items().filter((p) => this.marcadas().has(p.id!)));
  }

  protected async aprobar(lista: readonly PublicacionEnRevisionDto[]): Promise<void> {
    const ids = lista.map((p) => p.id!);
    if (!ids.length) return;
    await this.resolver(ids, async () => {
      const n = await firstValueFrom(this.api.aprobarPublicaciones(ids));
      return n === 1 ? 'Publicación aprobada' : `${n} publicaciones aprobadas`;
    });
  }

  protected abrirRechazo(): void {
    this.motivo.set(MOTIVOS[0]);
    this.rechazoAbierto.set(true);
  }

  protected async rechazar(): Promise<void> {
    const p = this.actual();
    if (!p) return;
    const motivo = this.motivo().trim();
    this.rechazoAbierto.set(false);
    await this.resolver([p.id!], async () => {
      await firstValueFrom(this.api.rechazarPublicacion(p.id!, motivo));
      return 'Publicación rechazada';
    });
  }

  private async resolver(ids: readonly string[], accion: () => Promise<string>): Promise<void> {
    this.ocupado.set(true);
    try {
      this.avisos.exito(await accion());
      this.resueltas.update((s) => new Set([...s, ...ids]));
      this.marcadas.update((s) => new Set([...s].filter((id) => !ids.includes(id))));
      if (!this.items().length) this.recargar(); // trae las siguientes, si hay más de 100
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.ocupado.set(false);
    }
  }
}
