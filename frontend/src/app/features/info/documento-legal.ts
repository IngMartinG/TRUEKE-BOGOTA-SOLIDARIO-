import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface SeccionLegal {
  titulo: string;
  parrafos: string[];
}

/** Plantilla común para las páginas legales. */
@Component({
  selector: 'app-documento-legal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="contenedor max-w-3xl py-10 sm:py-14">
      <header class="border-b border-borde pb-6">
        <h1 class="text-3xl font-extrabold sm:text-4xl">{{ titulo() }}</h1>
        <p class="mt-2 text-sm text-tenue">{{ subtitulo() }}</p>
      </header>
      <div class="mt-8 space-y-8">
        @for (s of secciones(); track s.titulo; let i = $index) {
          <section>
            <h2 class="text-lg font-bold">{{ i + 1 }}. {{ s.titulo }}</h2>
            @for (p of s.parrafos; track $index) {
              <p class="mt-3 leading-relaxed text-tinta/85">{{ p }}</p>
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
  readonly secciones = input.required<SeccionLegal[]>();
}
