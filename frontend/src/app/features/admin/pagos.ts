import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import type { EstadoPagoDto, PagoAdminDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { CopPipe, FechaPipe } from '../../shared/pipes';
import { Modal } from '../../shared/ui/modal';
import { Paginador } from '../../shared/ui/paginador';

const TAMANO = 20;

@Component({
  imports: [FormsModule, Modal, Paginador, CopPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Pagos</h2>
        <p class="text-sm text-tenue">"Requiere revisión": se cobró pero no se pudo aplicar el beneficio. Reembolsa en el panel de Wompi y regístralo aquí.</p>
      </div>
      <select class="entrada w-56" [ngModel]="estado()" (ngModelChange)="estado.set($event); pagina.set(1)" aria-label="Filtrar por estado">
        @for (e of estados; track e.valor) {
          <option [ngValue]="e.valor">{{ e.texto }}</option>
        }
      </select>
    </div>

    <div class="tarjeta mt-6 overflow-x-auto">
      <table class="w-full min-w-[46rem] text-left text-sm">
        <thead class="border-b border-borde text-xs text-tenue uppercase">
          <tr><th class="px-4 py-3">Referencia</th><th class="px-4 py-3">Concepto</th><th class="px-4 py-3">Monto</th><th class="px-4 py-3">Estado</th><th class="px-4 py-3">Fecha</th><th class="px-4 py-3"></th></tr>
        </thead>
        <tbody class="divide-y divide-borde">
          @for (p of recurso.value()?.items ?? []; track p.referencia) {
            <tr>
              <td class="px-4 py-3 font-mono text-xs">{{ p.referencia }}</td>
              <td class="px-4 py-3">{{ p.concepto }}</td>
              <td class="px-4 py-3">{{ p.montoCop | cop }}@if (p.puntosCanjeados) {<span class="block text-xs text-tenue">{{ p.puntosCanjeados }} pts canjeados</span>}</td>
              <td class="px-4 py-3"><span class="insignia-neutra">{{ p.estado }}</span>@if (p.notaInterna) {<span class="mt-1 block text-xs text-tenue">{{ p.notaInterna }}</span>}</td>
              <td class="px-4 py-3 text-xs">{{ p.fechaUtc | fecha: true }}</td>
              <td class="px-4 py-3 text-right">
                @if (p.estado === 'RequiereRevision') {
                  <button type="button" class="btn btn-secundario btn-sm" (click)="abrir(p)">Registrar reembolso</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="px-4 py-10 text-center text-tenue">{{ recurso.isLoading() ? 'Cargando…' : 'No hay pagos en este estado.' }}</td></tr>
          }
        </tbody>
      </table>
    </div>
    <app-paginador [pagina]="pagina()" [total]="recurso.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />

    <app-modal [(abierto)]="abierto" titulo="Registrar reembolso" [subtitulo]="'Referencia ' + (seleccionado()?.referencia ?? '')">
      <form id="form-reembolso" (ngSubmit)="reembolsar()" class="campo">
        <label for="nota-reembolso" class="etiqueta">Nota interna (id del reembolso en Wompi, motivo…)</label>
        <textarea id="nota-reembolso" name="nota" class="entrada" maxlength="300" [(ngModel)]="nota"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-reembolso" class="btn btn-primario" [disabled]="nota().trim().length < 3">Registrar</button>
      </div>
    </app-modal>
  `,
})
export default class Pagos {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly tamano = TAMANO;
  protected readonly estados: { valor: EstadoPagoDto | null; texto: string }[] = [
    { valor: 'RequiereRevision', texto: 'Requiere revisión' },
    { valor: 'Pendiente', texto: 'Pendientes' },
    { valor: 'Aprobado', texto: 'Aprobados' },
    { valor: 'Rechazado', texto: 'Rechazados' },
    { valor: 'Reembolsado', texto: 'Reembolsados' },
    { valor: null, texto: 'Todos' },
  ];
  protected readonly estado = signal<EstadoPagoDto | null>('RequiereRevision');
  protected readonly pagina = signal(1);
  protected readonly recurso = rxResource({
    params: () => ({ estado: this.estado(), pagina: this.pagina() }),
    stream: ({ params }) => this.api.pagos(params.estado, params.pagina, TAMANO),
  });
  protected readonly abierto = signal(false);
  protected readonly seleccionado = signal<PagoAdminDto | null>(null);
  protected readonly nota = signal('');

  protected abrir(p: PagoAdminDto): void {
    this.seleccionado.set(p);
    this.nota.set('');
    this.abierto.set(true);
  }

  protected async reembolsar(): Promise<void> {
    const p = this.seleccionado();
    if (!p?.referencia) return;
    try {
      await firstValueFrom(this.api.marcarReembolsado(p.referencia, this.nota().trim()));
      this.abierto.set(false);
      this.avisos.exito('Reembolso registrado');
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }
}
