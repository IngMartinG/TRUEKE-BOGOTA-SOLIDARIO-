import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, input, model, signal, viewChild } from '@angular/core';
import { miniatura, urlPublica, usarOriginal } from '../imagenes';
import { Icono } from './icono';

/**
 * Visor de fotos a pantalla completa sobre <dialog> nativo (atrapa el foco, Esc cierra y devuelve el foco).
 * Flechas y teclado para pasar fotos, deslizar en el celular, y tocar la foto (o el botón) para verla a tamaño real.
 */
@Component({
  selector: 'app-visor-imagenes',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    dialog::backdrop { background: rgb(4 12 8 / 0.92); }
    dialog[open] { animation: aparecer 0.18s ease-out; }
    @keyframes aparecer { from { opacity: 0; } }
    @media (prefers-reduced-motion: reduce) { dialog[open] { animation: none; } }
  `,
  template: `
    <!-- <dialog> es focusable y atrapa el foco; las flechas del teclado solo aceleran lo que hacen los botones. -->
    <!-- eslint-disable-next-line @angular-eslint/template/interactive-supports-focus -->
    <dialog
      #dialogo
      class="m-0 h-dvh max-h-none w-screen max-w-none bg-transparent p-0 text-white"
      [attr.aria-label]="'Fotos de ' + titulo()"
      (close)="abierto.set(false)"
      (keydown)="teclado($event)"
    >
      <div class="flex h-full flex-col">
        <header class="flex items-center justify-between gap-3 px-4 py-3">
          <p class="truncate text-sm font-semibold">{{ titulo() }}@if (imagenes().length > 1) {<span class="ml-2 font-normal text-white/70">{{ indice() + 1 }} / {{ imagenes().length }}</span>}</p>
          <div class="flex shrink-0 gap-1">
            <button type="button" class="grid size-10 place-items-center rounded-full hover:bg-white/15" (click)="ampliada.set(!ampliada())"
              [attr.aria-label]="ampliada() ? 'Ajustar a la pantalla' : 'Ver a tamaño real'" [attr.aria-pressed]="ampliada()">
              <app-icono [nombre]="ampliada() ? 'cuadricula' : 'mira'" />
            </button>
            <button type="button" class="grid size-10 place-items-center rounded-full hover:bg-white/15" (click)="abierto.set(false)" aria-label="Cerrar">
              <app-icono nombre="x" />
            </button>
          </div>
        </header>

        <!-- Deslizar cambia de foto; el teclado y los botones cubren lo mismo sin gestos. -->
        <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
        <div class="relative min-h-0 flex-1" [class.overflow-auto]="ampliada()" [class.cursor-zoom-out]="ampliada()"
          (pointerdown)="inicio($event)" (pointerup)="fin($event)" (click)="ampliada.set(!ampliada())">
          <div class="flex min-h-full min-w-full items-center justify-center" [class.p-0]="ampliada()" [class.px-2]="!ampliada()">
            <img [src]="actual()" [alt]="titulo() + ' — foto ' + (indice() + 1)" draggable="false" referrerpolicy="no-referrer"
              class="select-none" [class]="ampliada() ? 'max-w-none' : 'max-h-full max-w-full cursor-zoom-in object-contain'" />
          </div>
          @if (imagenes().length > 1 && !ampliada()) {
            <button type="button" class="absolute top-1/2 left-2 grid size-12 -translate-y-1/2 place-items-center rounded-full bg-black/50 hover:bg-black/70"
              (click)="mover(-1, $event)" aria-label="Foto anterior"><app-icono nombre="izquierda" /></button>
            <button type="button" class="absolute top-1/2 right-2 grid size-12 -translate-y-1/2 place-items-center rounded-full bg-black/50 hover:bg-black/70"
              (click)="mover(1, $event)" aria-label="Foto siguiente"><app-icono nombre="derecha" /></button>
          }
        </div>

        @if (imagenes().length > 1) {
          <nav class="flex justify-center gap-2 overflow-x-auto px-4 py-3" aria-label="Miniaturas">
            @for (img of imagenes(); track img; let i = $index) {
              <button type="button" class="size-14 shrink-0 overflow-hidden rounded-lg border-2 transition"
                [class]="i === indice() ? 'border-white' : 'border-transparent opacity-60 hover:opacity-100'"
                (click)="ir(i)" [attr.aria-label]="'Ver foto ' + (i + 1)" [attr.aria-current]="i === indice()">
                <img [src]="url(miniatura(img))" alt="" class="size-full object-cover" loading="lazy" referrerpolicy="no-referrer" (error)="usarOriginal($event, img)" />
              </button>
            }
          </nav>
        }
      </div>
    </dialog>
  `,
})
export class VisorImagenes {
  readonly imagenes = input.required<readonly string[]>();
  readonly titulo = input('');
  readonly abierto = model(false);
  readonly indice = model(0);

  protected readonly ampliada = signal(false);
  protected readonly actual = computed(() => this.url(this.imagenes()[this.indice()] ?? ''));
  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');
  private xInicio: number | null = null;

  constructor() {
    effect(() => {
      const d = this.dialogo().nativeElement;
      if (this.abierto() && !d.open) d.showModal();
      else if (!this.abierto() && d.open) d.close();
      if (!this.abierto()) this.ampliada.set(false);
    });
  }

  protected url(img: string): string {
    return urlPublica(img);
  }

  protected readonly miniatura = miniatura;
  protected readonly usarOriginal = usarOriginal;

  protected ir(i: number): void {
    this.indice.set(i);
    this.ampliada.set(false);
  }

  protected mover(delta: number, e?: Event): void {
    e?.stopPropagation(); // que el clic en la flecha no active el zoom
    const n = this.imagenes().length;
    if (n > 1) this.ir((this.indice() + delta + n) % n);
  }

  protected teclado(e: KeyboardEvent): void {
    if (e.key === 'ArrowLeft') this.mover(-1);
    else if (e.key === 'ArrowRight') this.mover(1);
  }

  protected inicio(e: PointerEvent): void {
    this.xInicio = this.ampliada() ? null : e.clientX;
  }

  protected fin(e: PointerEvent): void {
    if (this.xInicio === null) return;
    const dx = e.clientX - this.xInicio;
    this.xInicio = null;
    if (Math.abs(dx) > 50) {
      e.preventDefault();
      this.mover(dx < 0 ? 1 : -1);
    }
  }
}
