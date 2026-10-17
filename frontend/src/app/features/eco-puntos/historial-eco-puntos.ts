import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import type { MovimientoEcoPuntosDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { FechaPipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { Paginador } from '../../shared/ui/paginador';

const TAMANO = 10;

/** Cómo se muestra cada motivo de movimiento (el detalle del servidor completa la frase). */
const CONCEPTOS: Record<string, { texto: string; icono: string }> = {
  SaldoInicial: { texto: 'Saldo que ya tenías', icono: 'moneda' },
  Bienvenida: { texto: 'Bienvenida por confirmar tu correo', icono: 'regalo' },
  Intercambio: { texto: 'Intercambio completado', icono: 'apreton' },
  Recarga: { texto: 'Recarga', icono: 'billetera' },
  BonoRecarga: { texto: 'Bono de recarga', icono: 'regalo' },
  CanjeDescuento: { texto: 'Descuento en', icono: 'etiqueta' },
  DevolucionCanje: { texto: 'Devolución del descuento en', icono: 'repeat' },
  Impulsar: { texto: 'Impulsaste', icono: 'cohete' },
  ReembolsoRecarga: { texto: 'Recarga reembolsada', icono: 'billetera' },
};

/** Historial de Eco-Puntos: de dónde vino cada punto y en qué se usó, con el saldo después de cada movimiento. */
@Component({
  selector: 'app-historial-eco-puntos',
  imports: [Icono, Paginador, RouterLink, NumeroPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="contenedor mt-14" aria-labelledby="titulo-historial">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 id="titulo-historial" class="titulo-seccion">Tu historial</h2>
          <p class="mt-1 text-sm text-tenue">De dónde vino cada punto y en qué lo usaste.</p>
        </div>
        @if (saldo() !== null) {
          <p class="text-sm text-tenue">Saldo actual: <strong class="text-tinta tabular-nums">{{ saldo() | numero }} Eco-Puntos</strong></p>
        }
      </div>

      <div class="tarjeta mt-5 overflow-hidden">
        @if (movimientos.isLoading() && !movimientos.value()) {
          <ul aria-hidden="true">
            @for (i of [1, 2, 3]; track i) {
              <li class="flex items-center gap-3 border-b border-borde p-4 last:border-0"><div class="esqueleto size-10 rounded-full"></div><div class="flex-1 space-y-2"><div class="esqueleto h-3 w-1/2"></div><div class="esqueleto h-3 w-1/4"></div></div></li>
            }
          </ul>
        } @else if (movimientos.error()) {
          <div class="flex flex-col items-center gap-3 p-8 text-center text-sm text-tenue">
            <p>No pudimos cargar tu historial.</p>
            <button type="button" class="btn btn-secundario btn-sm" (click)="movimientos.reload()">Reintentar</button>
          </div>
        } @else {
          <ul class="divide-y divide-borde">
            @for (m of movimientos.value()?.items ?? []; track m.id) {
              <li class="flex items-center gap-3 p-4">
                <span class="grid size-10 shrink-0 place-items-center rounded-full"
                  [class]="(m.puntos ?? 0) > 0 ? 'bg-bosque-100 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200' : 'bg-superficie-2 text-tenue'">
                  <app-icono [nombre]="concepto(m).icono" [tamano]="18" />
                </span>
                <div class="min-w-0 flex-1">
                  <p class="truncate text-sm font-semibold">{{ texto(m) }}</p>
                  <p class="text-xs text-tenue">{{ m.fechaUtc | fecha: true }} · saldo {{ m.saldoResultante | numero }}</p>
                </div>
                <p class="shrink-0 font-display text-lg font-bold tabular-nums"
                  [class]="(m.puntos ?? 0) > 0 ? 'text-bosque-700 dark:text-bosque-300' : 'text-tenue'">
                  {{ (m.puntos ?? 0) > 0 ? '+' : '−' }}{{ abs(m.puntos) | numero }}
                </p>
              </li>
            } @empty {
              <li class="p-8 text-center text-sm text-tenue">
                Aún no tienes movimientos. Completa tu primer intercambio para ganar puntos.
                <a routerLink="/explorar" class="enlace mt-2 block">Explorar el catálogo</a>
              </li>
            }
          </ul>
        }
      </div>
      <app-paginador [pagina]="pagina()" [total]="movimientos.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />
    </section>
  `,
})
export class HistorialEcoPuntos {
  private readonly api = inject(CuentaApi);
  readonly saldo = input<number | null>(null);
  protected readonly tamano = TAMANO;
  protected readonly pagina = signal(1);
  protected readonly movimientos = rxResource({
    params: () => this.pagina(),
    stream: ({ params }) => this.api.movimientosPuntos(params, TAMANO),
  });

  protected concepto(m: MovimientoEcoPuntosDto) {
    return CONCEPTOS[m.concepto ?? ''] ?? { texto: m.concepto ?? 'Movimiento', icono: 'moneda' };
  }

  protected texto(m: MovimientoEcoPuntosDto): string {
    const c = this.concepto(m);
    return m.detalle && m.concepto !== 'Recarga' && m.concepto !== 'ReembolsoRecarga' && m.concepto !== 'BonoRecarga' ? `${c.texto} · ${m.detalle}`.replace(' en ·', ' en') : c.texto;
  }

  protected abs(n: number | undefined): number {
    return Math.abs(n ?? 0);
  }
}
