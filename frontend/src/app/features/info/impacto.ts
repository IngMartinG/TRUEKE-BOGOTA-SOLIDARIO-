import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { HacePipe, KilosPipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';

/**
 * Impacto de la comunidad: cifras reales de intercambios completados, sin datos de personas. Los kilos y el CO₂ son
 * la única estimación y se presentan como tal, con la tabla de factores que usa la API.
 */
@Component({
  imports: [RouterLink, Icono, NumeroPipe, HacePipe, KilosPipe],
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

        @if (d.objetosConNuevoHogar) {
          <dl class="mt-10 grid gap-4 rounded-[2rem] border border-bosque-200 bg-bosque-50 p-6 sm:grid-cols-2 sm:p-8 dark:border-bosque-800 dark:bg-bosque-950/50">
            <div>
              <dt class="flex items-center gap-2 text-sm font-semibold text-bosque-700 dark:text-bosque-300"><app-icono nombre="reciclar" [tamano]="18" />Peso que no fue a la basura</dt>
              <dd class="mt-2 font-display text-4xl font-extrabold tabular-nums">≈ {{ d.kgReutilizados | kilos }}</dd>
            </div>
            <div>
              <dt class="flex items-center gap-2 text-sm font-semibold text-bosque-700 dark:text-bosque-300"><app-icono nombre="hoja" [tamano]="18" />CO₂ que no se emitió</dt>
              <dd class="mt-2 font-display text-4xl font-extrabold tabular-nums">≈ {{ d.kgCo2Evitado | kilos }}</dd>
            </div>
            <p class="text-sm text-tenue sm:col-span-2">Estimación: lo que pesa un objeto típico de cada categoría y lo que se emite al fabricar uno nuevo. <a routerLink="/impacto" fragment="calculo" class="enlace">Cómo lo calculamos</a></p>
          </dl>
        }

        <dl class="mt-4 grid gap-4 sm:grid-cols-3">
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

        @if (d.factores?.length) {
          <section id="calculo" class="mt-12 scroll-mt-24" aria-labelledby="titulo-calculo">
            <h2 id="titulo-calculo" class="titulo-seccion">Cómo calculamos los kilos y el CO₂</h2>
            <p class="mt-3 max-w-2xl text-tenue">
              Cuando un objeto encuentra un nuevo hogar, no termina en la basura y alguien deja de comprar uno nuevo. Por cada intercambio
              completado sumamos un objeto con el peso y las emisiones de fabricación de uno típico de su categoría. Son promedios a la baja,
              a partir de estudios de ciclo de vida: preferimos quedarnos cortos que exagerar.
            </p>
            <div class="mt-5 overflow-x-auto rounded-tarjeta border border-borde">
              <table class="w-full text-left text-sm">
                <caption class="sr-only">Factores por categoría</caption>
                <thead class="bg-superficie-2">
                  <tr><th scope="col" class="px-4 py-3 font-semibold">Categoría</th><th scope="col" class="px-4 py-3 text-right font-semibold">Peso por objeto</th><th scope="col" class="px-4 py-3 text-right font-semibold">CO₂ por objeto</th></tr>
                </thead>
                <tbody>
                  @for (f of d.factores; track f.categoria) {
                    <tr class="border-t border-borde">
                      <th scope="row" class="px-4 py-3 font-normal">{{ f.categoria }}</th>
                      <td class="px-4 py-3 text-right tabular-nums">{{ f.kgPorObjeto | kilos }}</td>
                      <td class="px-4 py-3 text-right tabular-nums">{{ f.kgCo2PorObjeto | kilos }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <p class="mt-3 text-xs text-tenue">En un trueke contamos solo lo publicado, aunque el objeto ofrecido a cambio también cambie de manos.</p>
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
  // inject() va fuera de stream: stream corre fuera del contexto de inyección (al cargar y al reintentar)
  private readonly catalogo = inject(CatalogoApi);
  protected readonly recurso = rxResource({ stream: () => this.catalogo.impacto$ });
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
