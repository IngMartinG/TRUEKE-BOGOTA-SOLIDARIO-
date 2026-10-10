import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ETIQUETA_CONCEPTO, TIPOS_DOCUMENTO, type EstadoFacturaDto, type FacturaAdminDto, type TipoDocumentoFiscalDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { descargar } from '../../shared/descargar';
import { CopPipe, FechaPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { Paginador } from '../../shared/ui/paginador';
import { SelectorMunicipio } from '../../shared/ui/selector-municipio';

const TAMANO = 20;

/**
 * Cola de facturación electrónica (modo Manual): cada pago aprobado deja una factura "Pendiente". El equipo la emite en el
 * portal de la DIAN o en su proveedor tecnológico (con los datos del CSV) y registra aquí el número y el CUFE.
 */
@Component({
  imports: [FormsModule, ReactiveFormsModule, Icono, Modal, Paginador, SelectorMunicipio, CopPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Facturas electrónicas</h2>
        <p class="text-sm text-tenue">Emite cada factura pendiente en tu sistema de facturación (DIAN o proveedor) y registra su número y CUFE.</p>
      </div>
      <div class="flex w-full flex-wrap gap-2 sm:w-auto">
        <select class="entrada w-full sm:w-44" [ngModel]="estado()" (ngModelChange)="estado.set($event); pagina.set(1)" aria-label="Filtrar por estado">
          <option value="Pendiente">Pendientes</option>
          <option value="Emitida">Emitidas</option>
          <option value="Anulada">Anuladas</option>
          <option value="Reemplazada">Reemplazadas</option>
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
              <td class="px-4 py-3">
                {{ f.compradorNombre }}<span class="block text-xs text-tenue">{{ f.compradorTipoDocumento ?? 'Consumidor final' }} {{ f.compradorDocumento }} · {{ f.compradorCorreo }}</span>
                <!-- Evidencia de lo que eligió la persona al pagar -->
                <span class="mt-1 block text-[11px] text-tenue">
                  @if (f.elegidaANombre === true) {
                    Eligió factura a su nombre el {{ f.fechaEleccionUtc | fecha: true }}
                  } @else if (f.elegidaANombre === false) {
                    Eligió y aceptó consumidor final el {{ f.fechaEleccionUtc | fecha: true }}
                  } @else {
                    Pago anterior a la elección de factura
                  }
                </span>
                @if (f.motivoCorreccion) {
                  <span class="mt-1 block text-[11px] text-agua-700 dark:text-agua-100">Corregida el {{ f.fechaCorreccionUtc | fecha: true }}: {{ f.motivoCorreccion }}</span>
                }
              </td>
              <td class="px-4 py-3">{{ etiquetaConcepto[f.concepto ?? ''] ?? f.concepto }}</td>
              <td class="px-4 py-3">{{ f.baseCop | cop }}</td>
              <td class="px-4 py-3">{{ f.ivaCop | cop }}</td>
              <td class="px-4 py-3 font-semibold">{{ f.totalCop | cop }}</td>
              <td class="px-4 py-3 text-right">
                <div class="flex flex-col items-end gap-1.5">
                  @if (f.estado === 'Pendiente') {
                    <button type="button" class="btn btn-primario btn-sm" (click)="abrir(f)">Registrar emisión</button>
                  } @else if (f.estado === 'Emitida' || f.estado === 'Reemplazada') {
                    <span class="text-xs">{{ f.numeroDian }}</span>
                    @if (f.requiereNotaCredito) {<span class="insignia-donacion">Requiere nota crédito</span>}
                  } @else {
                    <span class="text-xs text-tenue">{{ f.notaInterna }}</span>
                  }
                  @if (esSuper() && (f.estado === 'Pendiente' || f.estado === 'Emitida')) {
                    <button type="button" class="btn btn-fantasma btn-sm" (click)="abrirCorreccion(f)">
                      <app-icono nombre="editar" [tamano]="14" />{{ f.estado === 'Emitida' ? 'Reemplazar factura' : 'Corregir comprador' }}
                    </button>
                  }
                </div>
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

    <!-- Solo SuperUsuario: factura a nombre de la persona cuando lo pide (se equivocó o hubo un error) -->
    <app-modal [(abierto)]="correccionAbierta" [titulo]="aCorregir()?.estado === 'Emitida' ? 'Reemplazar factura emitida' : 'Corregir comprador'"
      [subtitulo]="aCorregir()?.referencia ?? ''" ancho="sm:max-w-xl">
      <form id="form-correccion" [formGroup]="correccion" (ngSubmit)="corregir()" class="space-y-4">
        @if (aCorregir()?.estado === 'Emitida') {
          <p class="rounded-xl bg-sol-50 p-3 text-sm dark:bg-sol-500/10">
            Esta factura ({{ aCorregir()?.numeroDian }}) ya se emitió: quedará <strong>reemplazada</strong> y marcada para emitir una
            <strong>nota crédito</strong> en la DIAN. Se creará una factura nueva, pendiente, con estos datos.
          </p>
        }
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-[10rem_1fr]">
          <div class="campo">
            <label for="corr-tipo" class="etiqueta">Tipo de documento</label>
            <select id="corr-tipo" class="entrada" formControlName="tipoDocumento">
              @for (t of tipos; track t.valor) {<option [value]="t.valor">{{ t.etiqueta }}</option>}
            </select>
          </div>
          <div class="campo">
            <label for="corr-doc" class="etiqueta">Número</label>
            <input id="corr-doc" class="entrada" formControlName="documento" maxlength="20" />
          </div>
        </div>
        <div class="campo">
          <label for="corr-nombre" class="etiqueta">Nombre completo o razón social</label>
          <input id="corr-nombre" class="entrada" formControlName="nombre" maxlength="150" />
        </div>
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div class="campo">
            <label for="corr-correo" class="etiqueta">Correo</label>
            <input id="corr-correo" type="email" class="entrada" formControlName="correo" maxlength="160" />
          </div>
          <div class="campo">
            <label for="corr-dir" class="etiqueta">Dirección</label>
            <input id="corr-dir" class="entrada" formControlName="direccion" maxlength="150" />
          </div>
        </div>
        <app-selector-municipio id="corr-municipio" formControlName="municipioCodigo" />
        <div class="campo">
          <label for="corr-motivo" class="etiqueta">Motivo (queda en la auditoría)</label>
          <textarea id="corr-motivo" class="entrada" formControlName="motivo" maxlength="300" placeholder="Ej: la persona pidió la factura a su nombre por la PQR-2026-0012"></textarea>
        </div>
        @if (errorCorreccion()) {<p class="error-campo" role="alert">{{ errorCorreccion() }}</p>}
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="correccionAbierta.set(false)">Cancelar</button>
        <button type="submit" form="form-correccion" class="btn btn-primario" [disabled]="correccion.invalid || corrigiendo()">
          {{ aCorregir()?.estado === 'Emitida' ? 'Reemplazar factura' : 'Guardar corrección' }}
        </button>
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

  // ---- Corrección del comprador (solo SuperUsuario)
  protected readonly esSuper = inject(SesionService).esSuperUsuario;
  protected readonly tipos = TIPOS_DOCUMENTO;
  protected readonly correccionAbierta = signal(false);
  protected readonly aCorregir = signal<FacturaAdminDto | null>(null);
  protected readonly corrigiendo = signal(false);
  protected readonly errorCorreccion = signal<string | null>(null);
  protected readonly correccion = inject(NonNullableFormBuilder).group({
    tipoDocumento: ['CC' as TipoDocumentoFiscalDto, Validators.required],
    documento: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(20)]],
    nombre: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    correo: ['', [Validators.required, Validators.email]],
    direccion: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(150)]],
    municipioCodigo: ['', Validators.required],
    motivo: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(300)]],
  });

  protected abrirCorreccion(f: FacturaAdminDto): void {
    this.aCorregir.set(f);
    this.errorCorreccion.set(null);
    // Parte de lo que ya tiene la factura (si era a nombre de alguien); el motivo siempre se escribe de nuevo.
    const aNombre = !!f.compradorTipoDocumento;
    this.correccion.reset({
      tipoDocumento: (f.compradorTipoDocumento as TipoDocumentoFiscalDto | undefined) ?? 'CC',
      documento: aNombre ? (f.compradorDocumento ?? '') : '',
      nombre: aNombre ? (f.compradorNombre ?? '') : '',
      correo: f.compradorCorreo ?? '',
      direccion: f.compradorDireccion ?? '',
      municipioCodigo: '',
      motivo: '',
    });
    this.correccionAbierta.set(true);
  }

  protected async corregir(): Promise<void> {
    const f = this.aCorregir();
    if (!f?.id || this.correccion.invalid) return;
    const { motivo, ...comprador } = this.correccion.getRawValue();
    this.corrigiendo.set(true);
    try {
      const vigente = await firstValueFrom(this.api.corregirCompradorFactura(f.id, { comprador, motivo: motivo.trim() }));
      this.correccionAbierta.set(false);
      this.avisos.exito(
        vigente.id === f.id ? 'Factura corregida' : 'Factura reemplazada',
        vigente.id === f.id ? 'Ya puedes emitirla con los datos nuevos.' : 'La original quedó para nota crédito y la nueva está en Pendientes.',
      );
      this.recurso.reload();
    } catch (e) {
      this.errorCorreccion.set(mensajeDe(e));
    } finally {
      this.corrigiendo.set(false);
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
