import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { TIPOS_DOCUMENTO, type TipoDocumentoFiscalDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { FechaPipe } from '../../shared/pipes';
import { direccion, documentoSegunTipo, filtrarNumerico, telefono } from '../../shared/validadores';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { CampoAccesible } from '../../shared/ui/campo-accesible';
import { EnfocarError } from '../../shared/ui/enfocar-error';
import { Icono } from '../../shared/ui/icono';
import { SelectorMunicipio } from '../../shared/ui/selector-municipio';
import { destinoSeguro } from '../auth/redireccion';

/**
 * Registro de vendedor (Ley 1480, art. 53): quien vende por un portal de contacto debe quedar registrado con su nombre,
 * documento, dirección de notificaciones y teléfono. Se pide una sola vez, antes de la primera publicación en modo Compra
 * (trueke y donación no lo piden). La API lo guarda cifrado y nunca lo muestra a otros usuarios.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, ErrorCampo, CampoAccesible, EnfocarError, Icono, SelectorMunicipio, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Registro de vendedor</h1>
    <p class="mt-1 text-tenue">Lo pedimos una sola vez, antes de tu primera venta. Truekes y donaciones no lo necesitan.</p>

    <div class="mt-5 flex gap-3 rounded-2xl border border-agua-500/25 bg-agua-50 p-4 text-sm dark:bg-agua-700/15">
      <app-icono nombre="candado" [tamano]="20" class="mt-0.5 shrink-0 text-agua-700 dark:text-agua-100" />
      <div class="space-y-1.5">
        <p class="font-semibold">¿Por qué lo pedimos y quién lo ve?</p>
        <p class="text-tenue">
          El Estatuto del Consumidor (Ley 1480, art. 53) nos exige llevar un registro de quienes venden en la plataforma. Guardamos
          tu documento, tu dirección y tu teléfono <strong>cifrados</strong>. Nadie de la comunidad los ve: solo los entregamos a quien
          te compró y presenta un reclamo, o a una autoridad que los pida. Lee más en la
          <a routerLink="/privacidad" class="enlace">política de tratamiento de datos</a>.
        </p>
      </div>
    </div>

    @if (registro.value(); as r) {
      <p class="mt-5 flex items-center gap-2 text-sm">
        <span class="insignia-trueke"><app-icono nombre="check" [tamano]="12" />Registrado</span>
        <span class="text-tenue">desde el {{ r.registradoUtc | fecha }}. Si algo cambió, actualízalo aquí.</span>
      </p>
    }

    <form [formGroup]="form" (ngSubmit)="guardar()" class="tarjeta mt-5 space-y-5 p-5" novalidate>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-[14rem_1fr]">
        <div class="campo">
          <label for="vend-tipo" class="etiqueta">Tipo de documento</label>
          <select id="vend-tipo" class="entrada" formControlName="tipoDocumento">
            @for (t of tipos; track t.valor) {
              <option [value]="t.valor">{{ t.etiqueta }}</option>
            }
          </select>
        </div>
        <div class="campo">
          <label for="vend-documento" class="etiqueta">Número de documento</label>
          <input id="vend-documento" class="entrada" formControlName="documento" maxlength="20" autocomplete="off" spellcheck="false"
            [attr.inputmode]="documentoNumerico() ? 'numeric' : null" (input)="documentoNumerico() && filtrar($event, form.controls.documento)" />
          <app-error-campo [control]="form.controls.documento" etiqueta="El documento" idError="vend-documento-error" />
        </div>
      </div>
      <div class="campo">
        <label for="vend-nombre" class="etiqueta">{{ form.controls.tipoDocumento.value === 'NIT' ? 'Razón social' : 'Nombre como aparece en el documento' }}</label>
        <input id="vend-nombre" class="entrada" formControlName="nombre" maxlength="150"
          [attr.autocomplete]="form.controls.tipoDocumento.value === 'NIT' ? 'organization' : 'name'" />
        <app-error-campo [control]="form.controls.nombre" etiqueta="El nombre" idError="vend-nombre-error" />
      </div>
      <div class="campo">
        <label for="vend-direccion" class="etiqueta">Dirección de notificaciones</label>
        <input id="vend-direccion" class="entrada" formControlName="direccion" maxlength="150" autocomplete="street-address"
          aria-describedby="vend-direccion-ayuda" />
        <p id="vend-direccion-ayuda" class="ayuda">Donde puedas recibir una notificación física, por ejemplo Calle 45 # 12-30, apto 301.</p>
        <app-error-campo [control]="form.controls.direccion" etiqueta="La dirección" idError="vend-direccion-error" />
      </div>
      <app-selector-municipio id="vend-ubicacion" formControlName="municipioCodigo" />
      <div class="campo">
        <label for="vend-telefono" class="etiqueta">Teléfono</label>
        <input id="vend-telefono" type="tel" inputmode="tel" class="entrada" formControlName="telefono" maxlength="20" autocomplete="tel-national"
          aria-describedby="vend-telefono-ayuda" />
        <p id="vend-telefono-ayuda" class="ayuda">Celular (300 123 4567) o fijo con indicativo (601 234 5678).</p>
        <app-error-campo [control]="form.controls.telefono" etiqueta="El teléfono" idError="vend-telefono-error" />
      </div>

      <div class="flex flex-wrap justify-end gap-2">
        <button type="submit" class="btn btn-primario" [disabled]="guardando()">
          {{ guardando() ? 'Guardando…' : volver() ? 'Guardar y volver a la publicación' : 'Guardar' }}
        </button>
      </div>
    </form>
  `,
})
export default class Vendedor {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly sesion = inject(SesionService);
  private readonly router = inject(Router);
  protected readonly tipos = TIPOS_DOCUMENTO;
  protected readonly filtrar = filtrarNumerico;
  protected readonly guardando = signal(false);
  /** ?volver=/publicar cuando la API pidió el registro al publicar en modo Compra (el borrador de la publicación se conserva). */
  readonly volver = input<string>();

  protected readonly registro = rxResource({ stream: () => this.api.miRegistroVendedor() });

  protected readonly form = this.fb.group({
    tipoDocumento: this.fb.control<TipoDocumentoFiscalDto>('CC'),
    documento: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(20)]],
    nombre: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    direccion: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(150), direccion]],
    municipioCodigo: ['', [Validators.required]],
    telefono: ['', [Validators.required, telefono]],
  }, { validators: documentoSegunTipo('tipoDocumento', 'documento', 'nombre') });
  private readonly tipoDocumento = toSignal(this.form.controls.tipoDocumento.valueChanges, { initialValue: this.form.controls.tipoDocumento.value });
  /** CC y NIT son solo números: teclado numérico en el celular y se limpia lo que no sea dígito al escribir o pegar. */
  protected readonly documentoNumerico = computed(() => this.tipoDocumento() === 'CC' || this.tipoDocumento() === 'NIT');

  constructor() {
    effect(() => {
      const r = this.registro.value();
      const u = this.sesion.usuario();
      untracked(() => {
        if (!this.form.pristine) return;
        this.form.reset({
          tipoDocumento: (r?.tipoDocumento as TipoDocumentoFiscalDto) ?? 'CC',
          documento: r?.documento ?? '',
          nombre: r?.nombre ?? u?.nombreCompleto ?? '',
          direccion: r?.direccion ?? '',
          municipioCodigo: r?.municipioCodigo ?? u?.municipioCodigo ?? '',
          telefono: r?.telefono ?? '',
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
      const usuario = await firstValueFrom(this.api.registrarVendedor({
        tipoDocumento: v.tipoDocumento,
        documento: v.documento.trim(),
        nombre: v.nombre.trim(),
        direccion: v.direccion.trim(),
        municipioCodigo: v.municipioCodigo,
        telefono: v.telefono.trim(),
      }));
      this.sesion.actualizarUsuario(usuario);
      this.form.markAsPristine();
      this.avisos.exito('Registro de vendedor guardado', this.volver() ? 'Ya puedes publicar tu venta.' : undefined);
      if (this.volver()) await this.router.navigateByUrl(destinoSeguro(this.volver()));
      else this.registro.reload();
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.guardando.set(false);
    }
  }
}
