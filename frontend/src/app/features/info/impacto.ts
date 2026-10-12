import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { HacePipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';

/** Impacto de la comunidad: cifras reales de intercambios completados (sin estimaciones ni datos de personas). */
@Component({
  imports: [RouterLink, Icono, NumeroPipe, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-b from-bosque-50 to-fondo py-14 dark:from-bosque-950/60">
      <div class="contenedor max-w-3xl text-center">
        <h1 class="text-4xl font-extrabold sm:text-5xl">Lo que hemos hecho circular</h1>
        <p class="mt-4 text-lg text-tenue">Cada objeto que encuentra un nuevo hogar es un residuo menos. Estas son las cifras reales de la comunidad.</p>
      </div>
    </section>

    <div class="contenedor max-w-4xl pb-16">
      @if (recurso.isLoading() && !recurso.value()) {
        <div class="esqueleto mx-auto h-40 max-w-md rounded-tarjeta" aria-hidden="true"></div>
      } @else if (recurso.error()) {
        <p class="text-center text-tenue">No pudimos cargar las cifras. <button type="button" class="enlace" (click)="recurso.reload()">Reintentar</button></p>
      } @else if (i(); as d) {
        <div class="text-center">
          <p class="font-display text-6xl font-extrabold text-bosque-700 tabular-nums sm:text-7xl dark:text-bosque-300">{{ d.objetosConNuevoHogar | numero }}</p>
          <p class="mt-1 text-lg font-semibold">{{ d.objetosConNuevoHogar === 1 ? 'objeto encontró' : 'objetos encontraron' }} un nuevo hogar</p>
          <p class="mt-1 text-sm text-tenue">{{ d.esteMes | numero }} este mes · {{ d.personasQueIntercambiaron | numero }} {{ d.personasQueIntercambiaron === 1 ? 'persona ha' : 'personas han' }} intercambiado</p>
        </div>

        <dl class="mt-10 grid gap-4 sm:grid-cols-3">
          @for (m of modos(); track m.texto) {
            <div class="tarjeta p-5">
              <dt class="flex items-center gap-2 text-sm font-semibold" [class]="m.color"><app-icono [nombre]="m.icono" [tamano]="18" />{{ m.texto }}</dt>
              <dd class="mt-2 font-display text-3xl font-extrabold tabular-nums">{{ m.total | numero }}</dd>
            </div>
          }
        </dl>

        @if (d.localidadesMasActivas?.length) {
          <section class="mt-12" aria-labelledby="titulo-localidades">
            <h2 id="titulo-localidades" class="titulo-seccion">Localidades más activas</h2>
            <ul class="mt-5 space-y-3">
              @for (l of d.localidadesMasActivas; track $index) {
                <li>
                  <div class="flex justify-between text-sm"><span class="font-semibold">{{ l.localidad }}<span class="font-normal text-tenue"> · {{ l.municipio }}</span></span><span class="tabular-nums">{{ l.intercambios | numero }}</span></div>
                  <div class="mt-1 h-2 overflow-hidden rounded-full bg-superficie-2"><div class="h-full rounded-full bg-bosque-500" [style.width.%]="(l.intercambios ?? 0) / maxLocalidad() * 100"></div></div>
                </li>
              }
            </ul>
          </section>
        }

        @if (!d.objetosConNuevoHogar) {
          <p class="mt-10 text-center text-tenue">La comunidad está empezando: ¡el primer intercambio puede ser el tuyo!</p>
        }
        <div class="mt-12 flex flex-wrap justify-center gap-3">
          <a routerLink="/publicar" class="btn btn-primario"><app-icono nombre="mas" [tamano]="18" />Publicar algo</a>
          <a routerLink="/explorar" class="btn btn-secundario">Explorar</a>
        </div>
        <p class="mt-8 text-center text-xs text-tenue">
          Solo contamos intercambios completados (ambas partes confirmaron la entrega). Una localidad aparece desde 3 intercambios, para que nadie pueda ser identificado. Actualizado {{ d.calculadoUtc | hace }}.
        </p>
      }
    </div>
  `,
})
export default class Impacto {
  protected readonly recurso = rxResource({ stream: () => inject(CatalogoApi).impacto() });
  protected readonly i = computed(() => this.recurso.value());
  protected readonly modos = computed(() => {
    const d = this.i();
    return [
      { texto: 'Truekes', total: d?.truekes ?? 0, icono: 'repeat', color: 'text-bosque-700 dark:text-bosque-300' },
      { texto: 'Donaciones', total: d?.donaciones ?? 0, icono: 'heart', color: 'text-tierra-600 dark:text-tierra-500' },
      { texto: 'Compras', total: d?.compras ?? 0, icono: 'bag', color: 'text-cielo-700 dark:text-cielo-100' },
    ];
  });
  protected readonly maxLocalidad = computed(() => Math.max(1, ...(this.i()?.localidadesMasActivas ?? []).map((l) => l.intercambios ?? 0)));
}
