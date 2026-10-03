import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { etiquetaPqr, TIPOS_PQR, type TipoPqrDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { FechaPipe } from '../../shared/pipes';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [ReactiveFormsModule, ErrorCampo, Icono, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Soporte y PQR</h1>
    <p class="mt-1 text-tenue">
      Peticiones, quejas, reclamos y sugerencias. Te entregamos un número de radicado y respondemos por escrito en máximo 15 días hábiles.
    </p>

    <form [formGroup]="form" (ngSubmit)="enviar()" class="tarjeta mt-6 space-y-5 p-6" novalidate>
      <fieldset>
        <legend class="etiqueta mb-2">¿Qué necesitas?</legend>
        <div class="grid gap-2 sm:grid-cols-2">
          @for (t of tipos; track t.valor) {
            <label class="flex cursor-pointer items-start gap-3 rounded-2xl border-2 p-3 transition"
              [class]="tipo() === t.valor ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/50' : 'border-borde hover:border-bosque-200'">
              <input type="radio" class="sr-only" formControlName="tipo" [value]="t.valor" [attr.aria-label]="t.etiqueta" />
              <span>
                <span class="block text-sm font-semibold">{{ t.etiqueta }}</span>
                <span class="block text-xs text-tenue">{{ t.descripcion }}</span>
              </span>
            </label>
          }
        </div>
      </fieldset>

      @if (conPago()) {
        <div class="campo animate-aparecer">
          <label for="pago-ref" class="etiqueta">Referencia del pago</label>
          <input id="pago-ref" class="entrada font-mono" formControlName="pagoReferencia" maxlength="100" placeholder="TRK-…" />
          <p class="ayuda">La encuentras en Cuenta → Facturación o en el correo de confirmación del pago.</p>
          <app-error-campo [control]="form.controls.pagoReferencia" etiqueta="La referencia" />
        </div>
      }
      <div class="campo">
        <label for="asunto" class="etiqueta">Asunto</label>
        <input id="asunto" class="entrada" formControlName="asunto" maxlength="120" />
        <app-error-campo [control]="form.controls.asunto" etiqueta="El asunto" />
      </div>
      <div class="campo">
        <label for="descripcion-pqr" class="etiqueta">Cuéntanos qué pasó</label>
        <textarea id="descripcion-pqr" class="entrada min-h-32" formControlName="descripcion" maxlength="2000"></textarea>
        <div class="flex justify-between">
          <app-error-campo [control]="form.controls.descripcion" etiqueta="La descripción" />
          <span class="ml-auto ayuda">{{ form.controls.descripcion.value.length }}/2000</span>
        </div>
      </div>
      <div class="flex justify-end">
        <button type="submit" class="btn btn-primario" [disabled]="enviando()"><app-icono nombre="enviar" [tamano]="16" />{{ enviando() ? 'Enviando…' : 'Radicar solicitud' }}</button>
      </div>
    </form>

    <section class="mt-8">
      <h2 class="text-lg font-bold">Mis solicitudes</h2>
      <ul class="mt-3 space-y-3">
        @for (p of mias.value() ?? []; track p.id) {
          <li class="tarjeta p-5">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <p class="font-semibold">{{ p.asunto }}</p>
              <span [class]="p.estado === 'Respondida' ? 'insignia-trueke' : 'insignia-sol'">{{ p.estado === 'Respondida' ? 'Respondida' : 'En trámite' }}</span>
            </div>
            <p class="mt-1 text-xs text-tenue">{{ etiqueta(p.tipo) }} · Radicado <span class="font-mono">{{ p.radicado }}</span> · {{ p.fechaUtc | fecha }}
              @if (p.estado !== 'Respondida') { · respuesta a más tardar el {{ p.fechaLimiteUtc | fecha }} }
            </p>
            @if (p.respuesta) {
              <div class="mt-3 rounded-xl bg-bosque-50 p-3 text-sm whitespace-pre-line dark:bg-bosque-950/50">
                <p class="mb-1 text-xs font-semibold text-tenue">Respuesta del equipo · {{ p.fechaRespuestaUtc | fecha }}</p>{{ p.respuesta }}
              </div>
            }
          </li>
        } @empty {
          <li class="py-6 text-center text-sm text-tenue">{{ mias.isLoading() ? 'Cargando…' : 'No has enviado solicitudes.' }}</li>
        }
      </ul>
    </section>
  `,
})
export default class Soporte {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly ruta = inject(ActivatedRoute);
  protected readonly tipos = TIPOS_PQR;
  protected readonly etiqueta = etiquetaPqr;
  protected readonly enviando = signal(false);
  protected readonly mias = rxResource({ stream: () => this.api.misPqr() });

  protected readonly form = this.fb.group({
    tipo: this.fb.control<TipoPqrDto>((this.ruta.snapshot.queryParamMap.get('tipo') as TipoPqrDto) || 'Peticion'),
    pagoReferencia: [this.ruta.snapshot.queryParamMap.get('pago') ?? ''],
    asunto: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(120)]],
    descripcion: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]],
  });
  protected readonly tipo = toSignal(this.form.controls.tipo.valueChanges, { initialValue: this.form.controls.tipo.value });
  protected readonly conPago = computed(() => TIPOS_PQR.find((t) => t.valor === this.tipo())?.conPago ?? false);

  protected async enviar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.conPago() && !this.form.controls.pagoReferencia.value.trim()) {
      this.form.controls.pagoReferencia.setErrors({ required: true });
      return;
    }
    if (this.form.invalid) return;
    this.enviando.set(true);
    try {
      const v = this.form.getRawValue();
      const pqr = await firstValueFrom(
        this.api.crearPqr({
          tipo: v.tipo,
          asunto: v.asunto.trim(),
          descripcion: v.descripcion.trim(),
          pagoReferencia: this.conPago() ? v.pagoReferencia.trim() : null,
        }),
      );
      this.avisos.exito(`Solicitud radicada: ${pqr.radicado}`, 'Te enviamos el acuse a tu correo.');
      this.form.reset({ tipo: 'Peticion', pagoReferencia: '', asunto: '', descripcion: '' });
      this.mias.reload();
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
