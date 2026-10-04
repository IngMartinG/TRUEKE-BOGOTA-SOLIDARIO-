import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ETIQUETA_CONCEPTO } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { descargar } from '../../shared/descargar';
import { CopPipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';

const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

/** Tablero financiero (solo SuperUsuario): lo cobrado, reembolsos, ingreso recurrente y pendientes de facturar. */
@Component({
  imports: [FormsModule, Icono, CopPipe, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Ingresos</h2>
        <p class="text-sm text-tenue">Pagos aprobados por Wompi (fecha de aprobación, UTC). Neto = cobrado − reembolsado.</p>
      </div>
      <div class="flex flex-wrap items-end gap-2">
        <div class="campo">
          <label for="desde" class="etiqueta">Desde</label>
          <input id="desde" type="date" class="entrada" [ngModel]="desde()" (ngModelChange)="desde.set($event)" />
        </div>
        <div class="campo">
          <label for="hasta" class="etiqueta">Hasta</label>
          <input id="hasta" type="date" class="entrada" [ngModel]="hasta()" (ngModelChange)="hasta.set($event)" />
        </div>
        <button type="button" class="btn btn-secundario" (click)="exportar()" [disabled]="exportando()">
          <app-icono nombre="descargar" [tamano]="16" />{{ exportando() ? 'Exportando…' : 'CSV contable' }}
        </button>
      </div>
    </div>

    @if (recurso.value(); as i) {
      <div class="mt-6 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <div class="tarjeta p-5"><p class="text-xs text-tenue">Neto del periodo</p><p class="mt-1 font-display text-2xl font-extrabold">{{ i.netoCop | cop }}</p><p class="text-xs text-tenue">{{ i.pagos | numero }} pagos</p></div>
        <div class="tarjeta p-5"><p class="text-xs text-tenue">Cobrado</p><p class="mt-1 font-display text-2xl font-extrabold">{{ i.aprobadoCop | cop }}</p><p class="text-xs text-tierra-600">−{{ i.reembolsadoCop | cop }} reembolsado</p></div>
        <div class="tarjeta p-5"><p class="text-xs text-tenue">Ingreso recurrente mensual</p><p class="mt-1 font-display text-2xl font-extrabold">{{ i.ingresoRecurrenteMensualCop | cop }}</p><p class="text-xs text-tenue">{{ i.premiumVigentes }} Premium · {{ i.empresaVigentes }} Empresa</p></div>
        <div class="tarjeta p-5"><p class="text-xs text-tenue">Pendientes</p><p class="mt-1 font-display text-2xl font-extrabold">{{ i.facturasPendientes }}</p><p class="text-xs text-tenue">facturas por emitir · {{ i.pqrAbiertas }} PQR abiertas</p></div>
      </div>

      <div class="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[1.4fr_1fr]">
        <section class="tarjeta p-5">
          <h3 class="font-bold">Por mes</h3>
          @if ((i.porMes ?? []).length) {
            <div class="mt-4 flex h-48 items-end gap-2" role="img" aria-label="Ingresos netos por mes">
              @for (m of i.porMes ?? []; track m.anio + '-' + m.mes) {
                <div class="flex flex-1 flex-col items-center gap-1">
                  <div class="w-full rounded-t bg-bosque-500/80" [style.height.%]="altura((m.aprobadoCop ?? 0) - (m.reembolsadoCop ?? 0))"
                    [title]="mes(m.mes, m.anio) + ': ' + ((m.aprobadoCop ?? 0) - (m.reembolsadoCop ?? 0) | cop)"></div>
                  <span class="text-[10px] text-tenue">{{ mes(m.mes, m.anio) }}</span>
                </div>
              }
            </div>
          } @else {
            <p class="mt-4 text-sm text-tenue">Sin pagos en el periodo.</p>
          }
        </section>
        <section class="tarjeta p-5">
          <h3 class="font-bold">Por concepto</h3>
          <ul class="mt-3 divide-y divide-borde text-sm">
            @for (c of i.porConcepto ?? []; track c.concepto) {
              <li class="flex justify-between gap-3 py-2">
                <span>{{ etiquetaConcepto[c.concepto ?? ''] ?? c.concepto }} <span class="text-xs text-tenue">({{ c.pagos }})</span></span>
                <span class="font-semibold">{{ (c.aprobadoCop ?? 0) - (c.reembolsadoCop ?? 0) | cop }}</span>
              </li>
            } @empty {
              <li class="py-2 text-tenue">Sin datos.</li>
            }
          </ul>
        </section>
      </div>
    } @else if (recurso.isLoading()) {
      <div class="esqueleto mt-6 h-64"></div>
    } @else if (recurso.error()) {
      <p class="mt-6 text-sm text-tierra-600">No se pudo cargar el tablero (requiere rol SuperUsuario con sesión 2FA).</p>
    }
  `,
})
export default class Ingresos {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly etiquetaConcepto = ETIQUETA_CONCEPTO;
  protected readonly exportando = signal(false);

  private static fecha(d: Date): string {
    return d.toISOString().slice(0, 10);
  }
  protected readonly hasta = signal(Ingresos.fecha(new Date()));
  protected readonly desde = signal(Ingresos.fecha(new Date(Date.now() - 365 * 86_400_000)));

  /** Fin del día "hasta" incluido. */
  private readonly rango = computed(() => ({
    desde: this.desde() ? `${this.desde()}T00:00:00Z` : null,
    hasta: this.hasta() ? new Date(Date.parse(`${this.hasta()}T00:00:00Z`) + 86_400_000).toISOString() : null,
  }));

  protected readonly recurso = rxResource({
    params: () => this.rango(),
    stream: ({ params }) => this.api.ingresos(params.desde, params.hasta),
  });

  protected mes(m?: number, a?: number): string {
    return `${MESES[(m ?? 1) - 1]} ${String(a ?? '').slice(2)}`;
  }

  protected altura(valor: number): number {
    const max = Math.max(1, ...(this.recurso.value()?.porMes ?? []).map((x) => (x.aprobadoCop ?? 0) - (x.reembolsadoCop ?? 0)));
    return Math.max(2, Math.round((Math.max(0, valor) / max) * 100));
  }

  protected async exportar(): Promise<void> {
    this.exportando.set(true);
    try {
      const r = this.rango();
      descargar(await firstValueFrom(this.api.ingresosCsv(r.desde, r.hasta)), `ingresos-${this.desde()}-a-${this.hasta()}.csv`);
    } catch {
      this.avisos.error('No se pudo exportar el CSV.');
    } finally {
      this.exportando.set(false);
    }
  }
}
