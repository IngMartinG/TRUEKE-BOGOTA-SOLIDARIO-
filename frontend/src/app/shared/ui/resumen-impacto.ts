import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { NumeroPipe } from '../pipes';
import { Icono } from './icono';

/**
 * Resumen del impacto de la comunidad para el inicio. Solo cifras reales de intercambios completados;
 * no se muestra mientras no haya ninguno (nada de presumir ceros) ni si la API falla.
 */
@Component({
  selector: 'app-resumen-impacto',
  imports: [RouterLink, Icono, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (datos(); as d) {
      <section class="contenedor mt-20" aria-labelledby="titulo-impacto">
        <div class="grid grid-cols-1 items-center gap-8 rounded-[2rem] border border-bosque-200 bg-bosque-50 p-8 sm:p-12 lg:grid-cols-[1fr_1.4fr] dark:border-bosque-800 dark:bg-bosque-950/50">
          <div>
            <h2 id="titulo-impacto" class="titulo-seccion">Lo que hemos logrado juntos</h2>
            <p class="mt-3 max-w-md text-tenue">Cada objeto que encuentra un nuevo hogar es un residuo menos. Contamos solo intercambios completados por las dos partes.</p>
            <a routerLink="/impacto" class="btn btn-primario mt-6">Ver el impacto completo <app-icono nombre="flechaDerecha" [tamano]="16" /></a>
          </div>
          <dl class="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div class="rounded-2xl bg-superficie p-5 shadow-suave sm:col-span-3">
              <dt class="text-sm font-semibold text-tenue">{{ d.objetosConNuevoHogar === 1 ? 'Objeto con nuevo hogar' : 'Objetos con nuevo hogar' }}</dt>
              <dd class="mt-1 font-display text-5xl font-extrabold text-bosque-700 tabular-nums sm:text-6xl dark:text-bosque-300">{{ d.objetosConNuevoHogar | numero }}</dd>
            </div>
            <div class="rounded-2xl bg-superficie p-5 sm:col-span-2">
              <dt class="text-sm font-semibold text-tenue">{{ d.personasQueIntercambiaron === 1 ? 'Persona que ha intercambiado' : 'Personas que han intercambiado' }}</dt>
              <dd class="mt-1 font-display text-3xl font-extrabold tabular-nums">{{ d.personasQueIntercambiaron | numero }}</dd>
            </div>
            <div class="rounded-2xl bg-superficie p-5">
              <dt class="text-sm font-semibold text-tenue">Este mes</dt>
              <dd class="mt-1 font-display text-3xl font-extrabold tabular-nums">{{ d.esteMes | numero }}</dd>
            </div>
          </dl>
        </div>
      </section>
    }
  `,
})
export class ResumenImpacto {
  private readonly impacto = toSignal(inject(CatalogoApi).impacto$.pipe(catchError(() => of(null))));
  protected readonly datos = computed(() => {
    const d = this.impacto();
    return d && (d.objetosConNuevoHogar ?? 0) > 0 ? d : null;
  });
}
