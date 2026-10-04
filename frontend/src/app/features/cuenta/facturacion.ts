import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ETIQUETA_CONCEPTO, TIPOS_DOCUMENTO, type TipoDocumentoFiscalDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CopPipe, FechaPipe } from '../../shared/pipes';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { SelectorMunicipio } from '../../shared/ui/selector-municipio';

@Component({
  imports: [ReactiveFormsModule, RouterLink, ErrorCampo, Icono, CopPipe, FechaPipe, SelectorMunicipio],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Pagos y facturas</h1>
    <p class="mt-1 text-tenue">Aquí ves lo que has pagado en la plataforma y sus facturas.</p>

    <!-- Para qué sirve: en Colombia toda venta se factura electrónicamente (DIAN), aunque la mayoría de usuarios no paguen nunca -->
    <div class="mt-5 flex gap-3 rounded-2xl border border-agua-500/25 bg-agua-50 p-4 text-sm dark:bg-agua-700/15">
      <app-icono nombre="info" [tamano]="20" class="mt-0.5 shrink-0 text-agua-700 dark:text-agua-100" />
      <div class="space-y-1.5">
        <p class="font-semibold">¿Para qué sirve esta sección?</p>
        <p class="text-tenue">
          Intercambiar, donar y comprar entre personas es gratis y <strong>no genera facturas</strong>. Solo cuando le pagas algo a la
          plataforma (destacar una publicación, verificar tu cuenta, un plan Premium o Empresa, o una recarga de Eco-Puntos) la ley
          colombiana nos obliga a emitir una factura electrónica ante la DIAN, con el IVA incluido.
        </p>
        <p class="text-tenue">
          No necesitas llenar nada: sin datos, la factura sale a nombre de "consumidor final" y es igual de válida.
          Agrega tu cédula o NIT solo si quieres la factura a tu nombre o al de tu empresa (por ejemplo, para descontarla como gasto).
        </p>
      </div>
    </div>

    <section class="mt-8">
      <h2 class="text-lg font-bold">Mis pagos y facturas</h2>
      @if (!facturas.isLoading() && !facturas.value()?.length) {
        <div class="tarjeta mt-3 flex flex-col items-center gap-2 p-8 text-center">
          <app-icono nombre="factura" [tamano]="28" class="text-tenue" />
          <p class="font-semibold">Aún no has hecho pagos</p>
          <p class="max-w-md text-sm text-tenue">Cuando compres un beneficio, aquí verás su factura. Los truekes, compras entre personas y donaciones no aparecen aquí.</p>
          <a routerLink="/eco-puntos" class="btn btn-secundario btn-sm mt-2">Ver beneficios y planes</a>
        </div>
      } @else {
        <div class="tarjeta mt-3 overflow-x-auto">
          <table class="w-full min-w-[40rem] text-left text-sm">
            <thead class="border-b border-borde text-xs text-tenue uppercase">
              <tr><th class="px-4 py-3">Fecha</th><th class="px-4 py-3">Concepto</th><th class="px-4 py-3">Total</th><th class="px-4 py-3">IVA incluido</th><th class="px-4 py-3">Estado</th></tr>
            </thead>
            <tbody class="divide-y divide-borde">
              @for (f of facturas.value() ?? []; track f.id) {
                <tr>
                  <td class="px-4 py-3 text-xs">{{ f.fechaUtc | fecha }}</td>
                  <td class="px-4 py-3">{{ etiquetaConcepto[f.concepto ?? ''] ?? f.concepto }}<span class="block font-mono text-[11px] text-tenue">{{ f.referencia }}</span></td>
                  <td class="px-4 py-3 font-semibold">{{ f.totalCop | cop }}</td>
                  <td class="px-4 py-3">{{ f.ivaCop | cop }}</td>
                  <td class="px-4 py-3">
                    @switch (f.estado) {
                      @case ('Emitida') { <span class="insignia-trueke">Emitida · {{ f.numeroDian }}</span> }
                      @case ('Anulada') { <span class="insignia-neutra">Anulada (reembolsado)</span> }
                      @default { <span class="insignia-sol">En emisión</span> }
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="px-4 py-10 text-center text-tenue">Cargando…</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
      <p class="mt-3 text-xs text-tenue">
        ¿Un problema con un pago? Puedes pedir el retracto (5 días hábiles) o la reversión desde
        <a routerLink="/cuenta/soporte" class="enlace">Soporte</a>.
      </p>
    </section>

    <details class="tarjeta group mt-8" [open]="abrirDatos()">
      <summary class="flex cursor-pointer list-none items-center justify-between gap-3 p-5">
        <span>
          <span class="block font-bold">¿Necesitas la factura a tu nombre o al de tu empresa?</span>
          <span class="block text-sm text-tenue">
            @if (datos.value()?.completos) {
              Tus facturas salen a nombre de <strong>{{ datos.value()?.nombre }}</strong>.
            } @else {
              Opcional. Hoy tus facturas salen a "consumidor final".
            }
          </span>
        </span>
        <app-icono nombre="abajo" [tamano]="20" class="shrink-0 text-tenue transition group-open:rotate-180" />
      </summary>

    <form [formGroup]="form" (ngSubmit)="guardar()" class="space-y-5 border-t border-borde p-5" novalidate>
      <div class="flex flex-wrap items-start justify-between gap-2">
        <h2 class="text-lg font-bold">Datos para tus facturas</h2>
        @if (datos.value()?.completos) {
          <span class="insignia-trueke"><app-icono nombre="check" [tamano]="12" />Completos</span>
        }
      </div>

      <div class="grid grid-cols-1 gap-4 sm:grid-cols-[14rem_1fr]">
        <div class="campo">
          <label for="tipo-doc" class="etiqueta">Tipo de documento</label>
          <select id="tipo-doc" class="entrada" formControlName="tipoDocumento">
            @for (t of tipos; track t.valor) {
              <option [value]="t.valor">{{ t.etiqueta }}</option>
            }
          </select>
        </div>
        <div class="campo">
          <label for="documento" class="etiqueta">Número de documento</label>
          <input id="documento" class="entrada" formControlName="documento" maxlength="20" autocomplete="off"
            [placeholder]="form.controls.tipoDocumento.value === 'NIT' ? '900123456 (calculamos el dígito de verificación)' : ''" />
          <app-error-campo [control]="form.controls.documento" etiqueta="El documento" />
        </div>
      </div>
      <div class="campo">
        <label for="nombre-fact" class="etiqueta">{{ form.controls.tipoDocumento.value === 'NIT' ? 'Razón social' : 'Nombre completo' }}</label>
        <input id="nombre-fact" class="entrada" formControlName="nombre" maxlength="150" autocomplete="name" />
        <app-error-campo [control]="form.controls.nombre" etiqueta="El nombre" />
      </div>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div class="campo">
          <label for="correo-fact" class="etiqueta">Correo para recibir la factura</label>
          <input id="correo-fact" type="email" class="entrada" formControlName="correo" maxlength="160" autocomplete="email" />
          <app-error-campo [control]="form.controls.correo" etiqueta="El correo" />
        </div>
        <div class="campo">
          <label for="direccion-fact" class="etiqueta">Dirección (opcional)</label>
          <input id="direccion-fact" class="entrada" formControlName="direccion" maxlength="150" autocomplete="street-address" />
        </div>
      </div>
      <app-selector-municipio id="fact-ubicacion" formControlName="municipioCodigo" />

      <div class="flex flex-wrap justify-end gap-2">
        @if (datos.value()?.completos) {
          <button type="button" class="btn btn-fantasma" (click)="borrar()" [disabled]="guardando()">Facturar como consumidor final</button>
        }
        <button type="submit" class="btn btn-primario" [disabled]="form.pristine || guardando()">{{ guardando() ? 'Guardando…' : 'Guardar datos' }}</button>
      </div>
    </form>
    </details>
  `,
})
export default class Facturacion {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly sesion = inject(SesionService);
  protected readonly tipos = TIPOS_DOCUMENTO;
  protected readonly etiquetaConcepto = ETIQUETA_CONCEPTO;
  protected readonly guardando = signal(false);

  protected readonly datos = rxResource({ stream: () => this.api.datosFacturacion() });
  protected readonly facturas = rxResource({ stream: () => this.api.facturas() });
  /** Los datos fiscales se muestran abiertos solo a quien ya los tiene, a las empresas o si llega desde el pago (?datos=1). */
  protected readonly abrirDatos = computed(
    () => !!this.datos.value()?.completos || this.sesion.usuario()?.tipoCuenta === 'Empresa' || this.desdePago,
  );
  private readonly desdePago = inject(ActivatedRoute).snapshot.queryParamMap.get('datos') === '1';

  protected readonly form = this.fb.group({
    tipoDocumento: this.fb.control<TipoDocumentoFiscalDto>('CC'),
    documento: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(20)]],
    nombre: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    correo: ['', [Validators.required, Validators.email, Validators.maxLength(160)]],
    direccion: ['', [Validators.maxLength(150)]],
    municipioCodigo: [''],
  });

  constructor() {
    effect(() => {
      const d = this.datos.value();
      const u = this.sesion.usuario();
      untracked(() => {
        if (!this.form.pristine) return;
        this.form.reset({
          tipoDocumento: (d?.tipoDocumento as TipoDocumentoFiscalDto) ?? 'CC',
          documento: d?.documento ?? '',
          nombre: d?.nombre ?? u?.nombreCompleto ?? '',
          correo: d?.correo ?? u?.correo ?? '',
          direccion: d?.direccion ?? '',
          municipioCodigo: d?.municipioCodigo ?? u?.municipioCodigo ?? '',
        });
      });
    });
  }

  protected async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.guardando.set(true);
    try {
      const v = this.form.getRawValue();
      await firstValueFrom(
        this.api.guardarFacturacion({
          tipoDocumento: v.tipoDocumento,
          documento: v.documento.trim(),
          nombre: v.nombre.trim(),
          correo: v.correo.trim(),
          direccion: v.direccion.trim() || null,
          municipioCodigo: v.municipioCodigo || null,
        }),
      );
      this.form.markAsPristine();
      this.datos.reload();
      this.avisos.exito('Datos de facturación guardados');
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.guardando.set(false);
    }
  }

  protected async borrar(): Promise<void> {
    this.guardando.set(true);
    try {
      await firstValueFrom(this.api.borrarFacturacion());
      this.form.markAsPristine();
      this.datos.reload();
      this.avisos.exito('Tus próximas facturas saldrán a consumidor final');
    } finally {
      this.guardando.set(false);
    }
  }
}
