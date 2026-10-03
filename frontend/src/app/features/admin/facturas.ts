import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ETIQUETA_CONCEPTO, type EstadoFacturaDto, type FacturaAdminDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { descargar } from '../../shared/descargar';
import { CopPipe, FechaPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { Paginador } from '../../shared/ui/paginador';

const TAMANO = 20;

/**
 * Cola de facturación electrónica (modo Manual): cada pago aprobado deja una factura "Pendiente". El equipo la emite en el
 * portal de la DIAN o en su proveedor tecnológico (con los datos del CSV) y registra aquí el número y el CUFE.
 */
@Component({
  imports: [FormsModule, Icono, Modal, Paginador, CopPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Facturas electrónicas</h2>
        <p class="text-sm text-tenue">Emite cada factura pendiente en tu sistema de facturación (DIAN o proveedor) y registra su número y CUFE.</p>
      </div>
      <div class="flex gap-2">
        <select class="entrada w-44" [ngModel]="estado()" (ngModelChange)="estado.set($event); pagina.set(1)" aria-label="Filtrar por estado">
          <option value="Pendiente">Pendientes</option>
          <option value="Emitida">Emitidas</option>
          <option value="Anulada">Anuladas</option>
        </select>
        <button type="button" class="btn btn-secundario" (click)="exportar()"><app-icono nombre="descargar" [tamano]="16" />CSV</button>
      </div>
    </div>

    <div class="tarjeta mt-6 overflow-x-auto">
      <table class="w-full min-w-[54rem] text-left text-sm">
        <thead class="border-b border-borde text-xs text-tenue uppercase">
          <tr><th class="px-4 py-3">Fecha</th><th class="px-4 py-3">Comprador</th><th class="px-4 py-3">Concepto</th><th class="px-4 py-3">Base</th><th class="px-4 py-3">IVA</th><th class="px-4 py-3">Total</th><th class="px-4 py-3"></th></tr>
        </thead>
        <tbody class="divide-y divide-borde">
          @for (f of recurso.value()?.items ?? []; track f.id) {
            <tr>
              <td class="px-4 py-3 text-xs">{{ f.fechaUtc | fecha: true }}<span class="block font-mono text-[11px] text-tenue">{{ f.referencia }}</span></td>
              <td class="px-4 py-3">{{ f.compradorNombre }}<span class="block text-xs text-tenue">{{ f.compradorTipoDocumento ?? 'Consumidor final' }} {{ f.compradorDocumento }} · {{ f.compradorCorreo }}</span></td>
              <td class="px-4 py-3">{{ etiquetaConcepto[f.concepto ?? ''] ?? f.concepto }}</td>
              <td class="px-4 py-3">{{ f.baseCop | cop }}</td>
              <td class="px-4 py-3">{{ f.ivaCop | cop }}</td>
              <td class="px-4 py-3 font-semibold">{{ f.totalCop | cop }}</td>
              <td class="px-4 py-3 text-right">
                @if (f.estado === 'Pendiente') {
                  <button type="button" class="btn btn-primario btn-sm" (click)="abrir(f)">Registrar emisión</button>
                } @else if (f.estado === 'Emitida') {
                  <span class="text-xs">{{ f.numeroDian }}</span>
                  @if (f.requiereNotaCredito) {<span class="insignia-donacion mt-1 block">Requiere nota crédito</span>}
                } @else {
                  <span class="text-xs text-tenue">{{ f.notaInterna }}</span>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="px-4 py-10 text-center text-tenue">{{ recurso.isLoading() ? 'Cargando…' : 'No hay facturas en este estado.' }}</td></tr>
          }
        </tbody>
      </table>
    </div>
    <app-paginador [pagina]="pagina()" [total]="recurso.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />

    <app-modal [(abierto)]="abierto" titulo="Registrar factura emitida" [subtitulo]="(seleccionada()?.compradorNombre ?? '') + ' · ' + (seleccionada()?.referencia ?? '')">
      <form id="form-factura" (ngSubmit)="registrar()" class="space-y-4">
        <div class="campo">
          <label for="numero-dian" class="etiqueta">Número de la factura (prefijo y consecutivo)</label>
          <input id="numero-dian" name="numero" class="entrada" maxlength="50" placeholder="FE-1024" [(ngModel)]="numero" />
        </div>
        <div class="campo">
          <label for="cufe" class="etiqueta">CUFE</label>
          <input id="cufe" name="cufe" class="entrada font-mono text-xs" maxlength="120" [(ngModel)]="cufe" />
        </div>
        @if (error()) {<p class="error-campo" role="alert">{{ error() }}</p>}
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-factura" class="btn btn-primario" [disabled]="!numero().trim() || cufe().trim().length < 10">Registrar</button>
      </div>
    </app-modal>
  `,
})
export default class Facturas {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly tamano = TAMANO;
  protected readonly etiquetaConcepto = ETIQUETA_CONCEPTO;
  protected readonly estado = signal<EstadoFacturaDto>('Pendiente');
  protected readonly pagina = signal(1);
  protected readonly recurso = rxResource({
    params: () => ({ estado: this.estado(), pagina: this.pagina() }),
    stream: ({ params }) => this.api.facturas(params.estado, params.pagina, TAMANO),
  });
  protected readonly abierto = signal(false);
  protected readonly seleccionada = signal<FacturaAdminDto | null>(null);
  protected readonly numero = signal('');
  protected readonly cufe = signal('');
  protected readonly error = signal<string | null>(null);

  protected abrir(f: FacturaAdminDto): void {
    this.seleccionada.set(f);
    this.numero.set('');
    this.cufe.set('');
    this.error.set(null);
    this.abierto.set(true);
  }

  protected async registrar(): Promise<void> {
    const f = this.seleccionada();
    if (!f?.id) return;
    try {
      await firstValueFrom(this.api.marcarFacturaEmitida(f.id, this.numero().trim(), this.cufe().trim()));
      this.abierto.set(false);
      this.avisos.exito('Factura registrada', 'El usuario recibió una notificación.');
      this.recurso.reload();
    } catch (e) {
      this.error.set(mensajeDe(e));
    }
  }

  protected async exportar(): Promise<void> {
    try {
      descargar(await firstValueFrom(this.api.facturasCsv(this.estado())), `facturas-${this.estado().toLowerCase()}.csv`);
    } catch {
      this.avisos.error('No se pudo exportar el CSV.');
    }
  }
}
