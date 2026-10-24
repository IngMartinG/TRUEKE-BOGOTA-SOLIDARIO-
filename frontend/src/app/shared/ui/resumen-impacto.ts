import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { KilosPipe, NumeroPipe } from '../pipes';
import { Icono } from './icono';

/**
 * Desde cuántos objetos con nuevo hogar se presume el impacto en el inicio. Por debajo, una cifra pequeña
 * ("3 objetos") hace ver vacía la comunidad; la página /impacto siempre muestra los datos reales.
 */
export const UMBRAL_IMPACTO = 25;

/** El impacto que vale la pena mostrar en el inicio, o null si no llega al umbral o la API falló. */
export function impactoVisible(api: CatalogoApi) {
  const impacto = toSignal(api.impacto$.pipe(catchError(() => of(null))));
  return computed(() => {
    const d = impacto();
    return d && (d.objetosConNuevoHogar ?? 0) >= UMBRAL_IMPACTO ? d : null;
  });
}

/**
 * Resumen del impacto de la comunidad para el inicio. Solo cifras reales de intercambios completados;
 * no se muestra por debajo de UMBRAL_IMPACTO (nada de presumir ceros) ni si la API falla.
 */
@Component({
  selector: 'app-resumen-impacto',
  imports: [RouterLink, Icono, NumeroPipe, KilosPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (datos(); as d) {
      <section class="contenedor mt-20" aria-labelledby="titulo-impacto">
        <div class="grid grid-cols-1 items-center gap-8 rounded-[2rem] border border-bosque-200 bg-bosque-50 p-8 sm:p-12 lg:grid-cols-[1fr_1.4fr] dark:border-bosque-800 dark:bg-bosque-950/50">
          <div>
            <h2 id="titulo-impacto" class="titulo-seccion">Lo que hemos logrado juntos</h2>
            <p class="mt-3 max-w-md text-tenue">Cada objeto que encuentra un nuevo hogar es un residuo menos y uno que no hay que fabricar. Contamos solo intercambios completados por las dos partes.</p>
            <a routerLink="/impacto" class="btn btn-primario mt-6">Ver el impacto completo <app-icono nombre="flechaDerecha" [tamano]="16" /></a>
          </div>
          <div>
            <dl class="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <div class="rounded-2xl bg-superficie p-5 shadow-suave sm:col-span-3">
                <dt class="text-sm font-semibold text-tenue">{{ d.objetosConNuevoHogar === 1 ? 'Objeto con nuevo hogar' : 'Objetos con nuevo hogar' }}</dt>
                <dd class="mt-1 flex flex-wrap items-baseline gap-x-3 font-display text-5xl font-extrabold text-bosque-700 tabular-nums sm:text-6xl dark:text-bosque-300">
                  {{ d.objetosConNuevoHogar | numero }}
                  <!-- "0 este mes" a comienzo de mes resta más de lo que suma: sin intercambios en el mes, no sale -->
                  @if (d.esteMes) {
                    <span class="font-sans text-base font-semibold text-tenue">{{ d.esteMes | numero }} este mes</span>
                  }
                </dd>
              </div>
              <div class="rounded-2xl bg-superficie p-5">
                <dt class="text-sm font-semibold text-tenue">Peso que no fue a la basura</dt>
                <dd class="mt-1 font-display text-3xl font-extrabold tabular-nums">≈ {{ d.kgReutilizados | kilos }}</dd>
              </div>
              <div class="rounded-2xl bg-superficie p-5">
                <dt class="text-sm font-semibold text-tenue">CO₂ que no se emitió</dt>
                <dd class="mt-1 font-display text-3xl font-extrabold tabular-nums">≈ {{ d.kgCo2Evitado | kilos }}</dd>
              </div>
              <div class="rounded-2xl bg-superficie p-5">
                <dt class="text-sm font-semibold text-tenue">{{ d.personasQueIntercambiaron === 1 ? 'Persona que ha intercambiado' : 'Personas que han intercambiado' }}</dt>
                <dd class="mt-1 font-display text-3xl font-extrabold tabular-nums">{{ d.personasQueIntercambiaron | numero }}</dd>
              </div>
            </dl>
            <p class="mt-3 text-xs text-tenue">Kilos y CO₂ son una estimación según la categoría de cada objeto. <a routerLink="/impacto" fragment="calculo" class="enlace">Cómo lo calculamos</a></p>
          </div>
        </div>
      </section>
    }
  `,
})
export class ResumenImpacto {
  protected readonly datos = impactoVisible(inject(CatalogoApi));
}
