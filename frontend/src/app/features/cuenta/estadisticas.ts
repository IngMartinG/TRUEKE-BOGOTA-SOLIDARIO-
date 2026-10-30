import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { NegociosApi } from '../../core/api/negocios.api';
import { SesionService } from '../../core/sesion.service';
import { FechaPipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';

/**
 * Rendimiento de toda la cuenta en los últimos 30 días (planes Premium y Negocio): vistas, personas que guardaron tus
 * publicaciones, solicitudes recibidas e intercambios, con la serie diaria y lo más visto.
 */
@Component({
  imports: [RouterLink, Icono, NumeroPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Estadísticas de la cuenta</h1>
    <p class="mt-1 text-sm text-tenue">Los últimos 30 días de todas tus publicaciones.</p>

    @if (!conPlan()) {
      <div class="mt-6 rounded-tarjeta bg-sol-50 p-6 dark:bg-sol-500/10">
        <p class="font-semibold">Las estadísticas de toda la cuenta son parte de los planes Premium y Negocio.</p>
        <p class="mt-1 text-sm text-tenue">Mira cuántas personas ven tus publicaciones, cuántas te escriben y qué es lo que más interesa.</p>
        <a routerLink="/planes" class="btn btn-sol btn-sm mt-4"><app-icono nombre="corona" [tamano]="14" />Ver planes</a>
      </div>
    } @else if (recurso.error()) {
      <p class="mt-6 text-tenue">No pudimos cargar las estadísticas. <button type="button" class="enlace" (click)="recurso.reload()">Reintentar</button></p>
    } @else if (recurso.value(); as s) {
      <dl class="mt-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
        @for (c of cifras(); track c.texto) {
          <div class="tarjeta p-4">
            <dt class="flex items-center gap-2 text-sm text-tenue"><app-icono [nombre]="c.icono" [tamano]="16" />{{ c.texto }}</dt>
            <dd class="mt-1 font-display text-3xl font-extrabold tabular-nums">{{ c.valor | numero }}</dd>
          </div>
        }
      </dl>

      <section class="tarjeta mt-4 p-5" aria-labelledby="titulo-serie">
        <div class="flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="titulo-serie" class="font-bold">Vistas por día</h2>
          <p class="text-xs text-tenue">{{ s.vistasTotales | numero }} vistas desde que publicas</p>
        </div>
        <div class="mt-4 flex h-40 items-end gap-0.5" role="img" [attr.aria-label]="'Vistas diarias: ' + s.vistas30Dias + ' en 30 días'">
          @for (d of s.serie ?? []; track d.fecha) {
            <div class="flex-1 rounded-t bg-bosque-500/80" [style.height.%]="altura(d.vistas ?? 0)" [title]="(d.fecha | fecha) + ': ' + d.vistas + ' vistas'"></div>
          }
        </div>
      </section>

      @if (s.masVistas?.length) {
        <section class="tarjeta mt-4 p-5" aria-labelledby="titulo-top">
          <h2 id="titulo-top" class="font-bold">Lo más visto</h2>
          <ol class="mt-3 divide-y divide-borde">
            @for (p of s.masVistas; track p.id; let i = $index) {
              <li class="flex items-center gap-3 py-2.5 text-sm">
                <span class="w-5 text-tenue tabular-nums">{{ i + 1 }}</span>
                <a [routerLink]="['/publicacion', p.id]" class="min-w-0 flex-1 truncate font-semibold hover:underline">{{ p.titulo }}</a>
                <span class="tabular-nums text-tenue">{{ p.vistas | numero }} vistas</span>
              </li>
            }
          </ol>
        </section>
      }
    } @else {
      <div class="esqueleto mt-6 h-64 rounded-tarjeta" aria-hidden="true"></div>
    }
  `,
})
export default class Estadisticas {
  private readonly api = inject(NegociosApi);
  private readonly sesion = inject(SesionService);
  protected readonly conPlan = computed(() => {
    const u = this.sesion.usuario();
    return !!u?.planVigenteHasta && u.tipoCuenta !== 'Individual';
  });
  protected readonly recurso = rxResource({
    params: () => this.conPlan(),
    stream: ({ params: conPlan }) => (conPlan ? this.api.estadisticas() : of(undefined)),
  });
  protected readonly cifras = computed(() => {
    const s = this.recurso.value();
    return [
      { texto: 'Vistas', valor: s?.vistas30Dias ?? 0, icono: 'ojo' },
      { texto: 'Solicitudes recibidas', valor: s?.solicitudes30Dias ?? 0, icono: 'mensaje' },
      { texto: 'Intercambios', valor: s?.intercambios30Dias ?? 0, icono: 'repeat' },
      { texto: 'Guardadas por otros', valor: s?.favoritos ?? 0, icono: 'heart' },
    ];
  });
  private readonly maximo = computed(() => Math.max(1, ...(this.recurso.value()?.serie ?? []).map((d) => d.vistas ?? 0)));
  protected altura = (v: number) => Math.max(2, (v / this.maximo()) * 100);
}
