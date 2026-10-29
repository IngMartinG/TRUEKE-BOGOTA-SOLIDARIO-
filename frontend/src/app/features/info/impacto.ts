import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { nombreMaterial, PERIODOS_IMPACTO, type ModoDto, type PeriodoImpactoDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { HacePipe, KilosPipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';

const METODOLOGIA = 'https://github.com/IngMartinG/TRUEKE-BOGOTA-SOLIDARIO-/blob/main/docs/METODOLOGIA-IMPACTO.md';

/**
 * Impacto de la comunidad: cifras reales de intercambios que ambas partes confirmaron, sin datos de personas. Kilos y CO₂
 * son estimaciones y se presentan como tal, con la versión de la metodología y la tabla de factores que usa la API.
 */
@Component({
  imports: [RouterLink, Icono, NumeroPipe, HacePipe, KilosPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-b from-bosque-50 to-fondo py-14 dark:from-bosque-950/60">
      <div class="contenedor max-w-3xl text-center">
        <h1 class="text-4xl font-extrabold text-balance sm:text-5xl">Lo que hemos hecho circular</h1>
        <p class="mt-4 text-lg text-tenue">Cada objeto que encuentra un nuevo hogar es un residuo menos. Estas son las cifras reales de la comunidad.</p>
      </div>
    </section>

    <div class="contenedor max-w-4xl pb-16">
      <form class="mb-8 flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end sm:justify-center" aria-label="Filtrar las cifras" (submit)="$event.preventDefault()">
        <fieldset class="grid grid-cols-4 gap-1 rounded-2xl border border-borde bg-superficie p-1 sm:flex sm:rounded-full">
          <legend class="sr-only">Periodo</legend>
          @for (p of periodos; track p.valor) {
            <label class="cursor-pointer rounded-xl px-2 py-1.5 text-center text-sm font-semibold transition sm:rounded-full sm:px-3 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-bosque-500"
              [class]="periodo() === p.valor ? 'bg-bosque-600 text-white' : 'text-tenue hover:text-tinta'">
              <input type="radio" name="periodo" class="sr-only" [value]="p.valor" [checked]="periodo() === p.valor" (change)="periodo.set(p.valor)" />{{ p.etiqueta }}
            </label>
          }
        </fieldset>
        <div class="grid grid-cols-[auto_minmax(0,1fr)] gap-2 sm:flex">
          <label class="sr-only" for="filtro-modo">Modo</label>
          <select id="filtro-modo" class="entrada py-2 text-sm sm:min-w-44" [value]="modo() ?? ''" (change)="modo.set($any($event.target).value || null)">
            <option value="">Todos los modos</option>
            <option value="Trueke">Truekes</option>
            <option value="Donacion">Donaciones</option>
            <option value="Compra">Compras</option>
          </select>
          <label class="sr-only" for="filtro-localidad">Localidad</label>
          <select id="filtro-localidad" class="entrada py-2 text-sm sm:min-w-56" [value]="localidad()" (change)="localidad.set($any($event.target).value)"
            [disabled]="!disponibles().length" [attr.title]="disponibles().length ? null : 'Aún ninguna localidad tiene 3 intercambios'">
            <option value="">Todas las zonas</option>
            @for (l of disponibles(); track l.clave) {
              <option [value]="l.clave">{{ l.localidad }} · {{ l.municipio }}</option>
            }
          </select>
        </div>
      </form>

      @if (recurso.isLoading() && !recurso.value()) {
        <div class="esqueleto mx-auto h-40 max-w-md rounded-tarjeta" aria-hidden="true"></div>
      } @else if (recurso.error()) {
        <p class="text-center text-tenue">No pudimos cargar las cifras. <button type="button" class="enlace" (click)="recurso.reload()">Reintentar</button></p>
      } @else if (i(); as d) {
        <div aria-live="polite">
          @if (d.reservado) {
            <p class="mx-auto max-w-md rounded-tarjeta border border-borde bg-superficie p-6 text-center text-tenue">
              Con estos filtros hay menos de 3 intercambios: no los mostramos para que nadie pueda ser identificado. Prueba otro periodo o modo.
            </p>
          } @else {
            <div class="text-center">
              <p class="font-display text-6xl font-extrabold text-bosque-700 tabular-nums sm:text-7xl dark:text-bosque-300">{{ d.objetosConNuevoHogar | numero }}</p>
              <p class="mt-1 text-lg font-semibold">{{ d.objetosConNuevoHogar === 1 ? 'objeto encontró' : 'objetos encontraron' }} un nuevo hogar</p>
              <p class="mt-1 text-sm text-tenue">
                {{ d.intercambios | numero }} {{ d.intercambios === 1 ? 'intercambio' : 'intercambios' }}
                @if (periodo() !== 'Mes') { · {{ d.esteMes | numero }} este mes }
                · {{ d.personasQueIntercambiaron | numero }} {{ d.personasQueIntercambiaron === 1 ? 'persona' : 'personas' }}
              </p>
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
                <p class="text-sm text-tenue sm:col-span-2">
                  Estimación con la metodología {{ d.versionMetodologia }}.
                  @if (porcentajeDeclarado() !== null) { El {{ porcentajeDeclarado() }} % de los kilos lo declaró quien publicó; el resto es el peso típico de su categoría. }
                  <a routerLink="/impacto" fragment="calculo" class="enlace">Cómo lo calculamos</a>
                </p>
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

            @if (materiales().length) {
              <section class="mt-12" aria-labelledby="titulo-materiales">
                <h2 id="titulo-materiales" class="titulo-seccion">Por material</h2>
                <ul class="mt-5 space-y-3">
                  @for (m of materiales(); track m.material) {
                    <li>
                      <div class="flex flex-wrap justify-between gap-x-3 text-sm">
                        <span class="font-semibold">{{ m.nombre }}<span class="font-normal text-tenue"> · {{ m.objetos | numero }} {{ m.objetos === 1 ? 'objeto' : 'objetos' }}</span></span>
                        <span class="tabular-nums">≈ {{ m.kg | kilos }} <span class="text-tenue">· {{ m.kgCo2 | kilos }} de CO₂</span></span>
                      </div>
                      <div class="mt-1 h-2 overflow-hidden rounded-full bg-superficie-2"><div class="h-full rounded-full bg-bosque-500" [style.width.%]="m.ancho"></div></div>
                    </li>
                  }
                </ul>
              </section>
            }
          }
        </div>

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
            <p class="mt-3 max-w-prose text-tenue">
              Solo cuenta lo que las dos personas confirmaron que se entregó. Cada objeto suma su peso (el que declaró quien lo publicó o,
              si no lo declaró, el típico de su categoría) y el CO₂ que no se emitió al no fabricar uno nuevo: kilos × el factor de su
              material. En un trueke con objetos cuenta cada objeto que cambió de manos. Son factores a la baja, de estudios de ciclo de
              vida: preferimos quedarnos cortos que exagerar.
            </p>
            <div class="mt-5 grid gap-5 md:grid-cols-2">
              <div class="overflow-x-auto rounded-tarjeta border border-borde">
                <table class="w-full text-left text-sm">
                  <caption class="px-4 pt-3 text-left text-xs font-semibold text-tenue">CO₂ evitado por kilo de cada material</caption>
                  <thead class="bg-superficie-2">
                    <tr><th scope="col" class="px-4 py-3 font-semibold">Material</th><th scope="col" class="px-4 py-3 text-right font-semibold">kg de CO₂ por kg</th></tr>
                  </thead>
                  <tbody>
                    @for (f of d.factores; track f.material) {
                      <tr class="border-t border-borde">
                        <th scope="row" class="px-4 py-2.5 font-normal">{{ nombre(f.material) }}</th>
                        <td class="px-4 py-2.5 text-right tabular-nums">{{ f.kgCo2PorKg | numero }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <div class="overflow-x-auto rounded-tarjeta border border-borde">
                <table class="w-full text-left text-sm">
                  <caption class="px-4 pt-3 text-left text-xs font-semibold text-tenue">Si no se declara el peso: objeto típico de cada categoría</caption>
                  <thead class="bg-superficie-2">
                    <tr><th scope="col" class="px-4 py-3 font-semibold">Categoría</th><th scope="col" class="px-4 py-3 text-right font-semibold">Peso</th></tr>
                  </thead>
                  <tbody>
                    @for (c of d.pesosPorCategoria; track c.categoria) {
                      <tr class="border-t border-borde">
                        <th scope="row" class="px-4 py-2.5 font-normal">{{ c.categoria }} <span class="text-tenue">· {{ nombre(c.material).toLowerCase() }}</span></th>
                        <td class="px-4 py-2.5 text-right tabular-nums">{{ c.kgPorObjeto | kilos }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
            <p class="mt-3 text-xs text-tenue">
              Metodología {{ d.versionMetodologia }}. Cada intercambio guarda el factor y la versión con que se calculó: si un factor cambia,
              lo ya contado no se reescribe. <a [href]="metodologia" target="_blank" rel="noopener" class="enlace">Metodología completa y fuentes</a>
            </p>
          </section>
        }

        @if (!d.objetosConNuevoHogar && !d.reservado && !filtrado()) {
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
  protected readonly periodos = PERIODOS_IMPACTO;
  protected readonly metodologia = METODOLOGIA;
  protected readonly nombre = nombreMaterial;
  protected readonly periodo = signal<PeriodoImpactoDto>('Todo');
  protected readonly modo = signal<ModoDto | null>(null);
  /** "municipio|localidad" de la lista de disponibles, o vacío. */
  protected readonly localidad = signal('');
  protected readonly filtrado = computed(() => this.periodo() !== 'Todo' || !!this.modo() || !!this.localidad());

  protected readonly recurso = rxResource({
    params: () => ({ periodo: this.periodo(), modo: this.modo(), localidad: this.localidad() }),
    stream: ({ params: p }) => {
      if (p.periodo === 'Todo' && !p.modo && !p.localidad) return this.catalogo.impacto$;
      const [municipioCodigo, localidad] = p.localidad ? p.localidad.split('|') : [null, null];
      return this.catalogo.impactoFiltrado({ periodo: p.periodo, modo: p.modo, municipioCodigo, localidad });
    },
  });
  protected readonly i = computed(() => this.recurso.value());
  /** Se conserva la lista mientras carga otro filtro, para que el selector no se vacíe. */
  private ultimasDisponibles: { clave: string; localidad: string; municipio: string }[] = [];
  protected readonly disponibles = computed(() => {
    const lista = this.i()?.localidadesDisponibles;
    if (lista) this.ultimasDisponibles = lista.map((l) => ({ clave: `${l.municipioCodigo}|${l.localidad}`, localidad: l.localidad ?? '', municipio: l.municipio ?? '' }));
    return this.ultimasDisponibles;
  });
  protected readonly modos = computed(() => {
    const d = this.i();
    return [
      { texto: 'Truekes', total: d?.truekes ?? 0, icono: 'repeat', color: 'text-bosque-700 dark:text-bosque-300' },
      { texto: 'Donaciones', total: d?.donaciones ?? 0, icono: 'heart', color: 'text-tierra-600 dark:text-tierra-500' },
      { texto: 'Compras', total: d?.compras ?? 0, icono: 'bag', color: 'text-cielo-700 dark:text-cielo-100' },
    ];
  });
  protected readonly materiales = computed(() => {
    const lista = this.i()?.porMaterial ?? [];
    const max = Math.max(1, ...lista.map((m) => m.kg ?? 0));
    return lista.map((m) => ({ material: m.material ?? '', nombre: nombreMaterial(m.material), objetos: m.objetos ?? 0, kg: m.kg ?? 0,
      kgCo2: m.kgCo2 ?? 0, ancho: ((m.kg ?? 0) / max) * 100 }));
  });
  protected readonly porcentajeDeclarado = computed(() => {
    const d = this.i();
    if (!d?.kgReutilizados || !d.kgDeclarados) return null;
    return Math.round((d.kgDeclarados / d.kgReutilizados) * 100);
  });
  protected readonly maxLocalidad = computed(() => Math.max(1, ...(this.i()?.localidadesMasActivas ?? []).map((l) => l.intercambios ?? 0)));
}
