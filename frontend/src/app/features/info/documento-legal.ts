import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icono } from '../../shared/ui/icono';
import { Volver } from '../../shared/ui/volver';

export interface SeccionLegal {
  titulo: string;
  parrafos: string[];
  /** Viñetas después de los párrafos (listas de prohibidos, derechos, encargados…). */
  lista?: string[];
}

/** Ancla estable de una sección para el índice ("#seccion-3"). */
export const anclaSeccion = (i: number) => `seccion-${i + 1}`;

/** Plantilla común para las páginas legales: versión, vigencia, índice con anclas y botón para imprimir o guardar en PDF. */
@Component({
  selector: 'app-documento-legal',
  imports: [Volver, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="contenedor max-w-3xl py-6 sm:py-10">
      <app-volver respaldo="/" class="mb-3 print:hidden" />
      <header class="border-b border-borde pb-6">
        <h1 class="text-3xl font-extrabold sm:text-4xl">{{ titulo() }}</h1>
        @if (subtitulo()) {
          <p class="mt-2 text-sm text-tenue">{{ subtitulo() }}</p>
        }
        @if (version() || vigenteDesde()) {
          <p class="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-sm">
            @if (version()) {
              <span><span class="me-1 text-tenue">Versión</span><strong>{{ version() }}</strong></span>
            }
            @if (vigenteDesde()) {
              <span><span class="me-1 text-tenue">Vigente desde</span><strong>{{ vigenteDesde() }}</strong></span>
            }
          </p>
        }
        <button type="button" class="btn btn-secundario btn-sm mt-4 print:hidden" (click)="imprimir()">
          <app-icono nombre="descargar" [tamano]="16" />Imprimir o guardar en PDF
        </button>
      </header>

      @if (secciones().length > 3) {
        <nav class="mt-6 rounded-tarjeta border border-borde p-4 print:hidden" aria-labelledby="indice-legal">
          <h2 id="indice-legal" class="text-sm font-bold">Contenido</h2>
          <ol class="mt-2 grid gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
            @for (s of secciones(); track s.titulo; let i = $index) {
              <li><a [href]="'#' + ancla(i)" class="enlace" (click)="irA($event, i)">{{ i + 1 }}. {{ s.titulo }}</a></li>
            }
          </ol>
        </nav>
      }

      <div class="mt-8 space-y-8">
        @for (s of secciones(); track s.titulo; let i = $index) {
          <section [id]="ancla(i)" class="scroll-mt-24" [attr.aria-labelledby]="ancla(i) + '-titulo'">
            <h2 [id]="ancla(i) + '-titulo'" class="text-lg font-bold focus:outline-none" tabindex="-1">{{ i + 1 }}. {{ s.titulo }}</h2>
            @for (p of s.parrafos; track $index) {
              <p class="mt-3 leading-relaxed text-tinta/85">{{ p }}</p>
            }
            @if (s.lista?.length) {
              <ul class="mt-3 list-disc space-y-1.5 pl-5 leading-relaxed text-tinta/85">
                @for (item of s.lista; track $index) {
                  <li>{{ item }}</li>
                }
              </ul>
            }
          </section>
        }
      </div>
      <ng-content />
    </article>
  `,
})
export class DocumentoLegal {
  readonly titulo = input.required<string>();
  readonly subtitulo = input('');
  readonly version = input<string | null | undefined>(null);
  readonly vigenteDesde = input<string | null | undefined>(null);
  readonly secciones = input.required<SeccionLegal[]>();

  protected readonly ancla = anclaSeccion;

  /** El enrutador usa la ruta base: el ancla se resuelve a mano para no salir de la página. */
  protected irA(e: Event, i: number): void {
    const destino = document.getElementById(anclaSeccion(i));
    if (!destino) return;
    e.preventDefault();
    const reducir = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    destino.scrollIntoView({ behavior: reducir ? 'auto' : 'smooth', block: 'start' });
    destino.querySelector<HTMLElement>('h2')?.focus({ preventScroll: true });
    history.replaceState(history.state, '', `${location.pathname}${location.search}#${anclaSeccion(i)}`);
  }

  protected imprimir(): void {
    window.print();
  }
}
