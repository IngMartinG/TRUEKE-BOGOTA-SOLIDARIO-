import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { estadoPqr, etiquetaPqr, TIPOS_PQR, type PqrDto, type TipoPqrDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { erroresDeCampos, mensajeDe, problemaDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CaptchaService } from '../../core/terceros/captcha.service';
import { FechaPipe } from '../../shared/pipes';
import { CampoAccesible } from '../../shared/ui/campo-accesible';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { Volver } from '../../shared/ui/volver';
import { nombrePersona, textoSeguro } from '../../shared/validadores';
import { BorradorPqrs, type Borrador } from '../ayuda/borrador-pqrs';

/** Lo que se puede radicar sin cuenta: el retracto y la reversión se piden sobre un pago propio, desde la cuenta. */
const TIPOS_PUBLICOS = TIPOS_PQR.filter((t) => !t.conPago);
const esTipoPublico = (v: string | null | undefined): v is TipoPqrDto => TIPOS_PUBLICOS.some((t) => t.valor === v);
const RE_RADICADO = /^ *[Pp][Qq][Rr]-\d{8}-[0-9A-Fa-f]{6} *$/;

/**
 * PQRS pública: cualquier persona, con o sin cuenta, radica una petición, queja, reclamo, sugerencia o consulta de sus
 * datos, y consulta su estado con el radicado y el correo. Tiene captcha y límite por IP y por correo.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, ErrorCampo, CampoAccesible, Icono, FechaPipe, Volver],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-3xl py-6 sm:py-10">
      <app-volver respaldo="/" class="mb-3" />
      <h1 class="text-3xl font-extrabold sm:text-4xl">PQRS</h1>
      <p class="mt-2 text-tenue">
        Peticiones, quejas, reclamos, sugerencias y consultas sobre tus datos personales. Te damos un número de radicado y
        respondemos por escrito a tu correo: en máximo 10 días hábiles las consultas de datos y en 15 lo demás.
      </p>

      @if (sesion.autenticado()) {
        <p class="mt-5 flex items-start gap-2 rounded-xl bg-cielo-50 p-3 text-sm dark:bg-cielo-700/20" role="note">
          <app-icono nombre="info" [tamano]="18" class="mt-0.5 shrink-0 text-cielo-600" />
          <span>
            Tienes una sesión iniciada: radica desde <a routerLink="/cuenta/soporte" class="enlace">Soporte y PQR</a> para ver tus solicitudes
            en tu cuenta y pedir un retracto o la reversión de un pago.
          </span>
        </p>
      }

      <div class="pestanas mt-6" role="tablist" aria-label="Qué quieres hacer">
        <button type="button" role="tab" id="tab-radicar" aria-controls="panel-radicar" class="pestana" [class.pestana-activa]="vista() === 'radicar'"
          [attr.aria-selected]="vista() === 'radicar'" (click)="vista.set('radicar')">Radicar</button>
        <button type="button" role="tab" id="tab-consultar" aria-controls="panel-consultar" class="pestana" [class.pestana-activa]="vista() === 'consultar'"
          [attr.aria-selected]="vista() === 'consultar'" (click)="vista.set('consultar')">Consultar el estado</button>
      </div>

      @if (vista() === 'radicar') {
        <div id="panel-radicar" role="tabpanel" aria-labelledby="tab-radicar">
          @if (radicada(); as r) {
            <div class="tarjeta mt-6 p-6" role="status">
              <p class="flex items-center gap-2 font-bold"><app-icono nombre="check" [tamano]="20" class="text-bosque-600" />Solicitud radicada</p>
              <p class="mt-2 text-sm">Tu número de radicado es <strong class="font-mono">{{ r.radicado }}</strong>. Te enviamos el acuse a tu correo.</p>
              <p class="mt-1 text-sm text-tenue">Respuesta a más tardar el {{ r.fechaLimiteUtc | fecha }}. Guarda el radicado para consultar el estado.</p>
              <button type="button" class="btn btn-secundario btn-sm mt-4" (click)="radicada.set(null)">Radicar otra</button>
            </div>
          } @else {
            <form [formGroup]="form" (ngSubmit)="radicar()" class="tarjeta mt-6 space-y-5 p-6" novalidate>
              <fieldset>
                <legend class="etiqueta mb-2">¿Qué necesitas?</legend>
                <div class="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  @for (t of tipos; track t.valor) {
                    <label class="flex cursor-pointer items-start gap-3 rounded-2xl border-2 p-3 transition"
                      [class]="tipo() === t.valor ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/50' : 'border-borde hover:border-bosque-200'">
                      <input type="radio" class="sr-only" formControlName="tipo" [value]="t.valor" />
                      <span>
                        <span class="block text-sm font-semibold">{{ t.etiqueta }}</span>
                        <span class="block text-xs text-tenue">{{ t.descripcion }}</span>
                      </span>
                    </label>
                  }
                </div>
              </fieldset>
              <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div class="campo">
                  <label for="pqrs-nombre" class="etiqueta">Nombre y apellido</label>
                  <input id="pqrs-nombre" class="entrada" formControlName="nombre" maxlength="120" autocomplete="name" />
                  <app-error-campo [control]="form.controls.nombre" etiqueta="El nombre" idError="pqrs-nombre-error" />
                </div>
                <div class="campo">
                  <label for="pqrs-correo" class="etiqueta">Correo para la respuesta</label>
                  <input id="pqrs-correo" type="email" class="entrada" formControlName="correo" maxlength="160" autocomplete="email" inputmode="email" />
                  <app-error-campo [control]="form.controls.correo" etiqueta="El correo" idError="pqrs-correo-error" />
                </div>
              </div>
              <div class="campo">
                <label for="pqrs-asunto" class="etiqueta">Asunto</label>
                <input id="pqrs-asunto" class="entrada" formControlName="asunto" maxlength="120" />
                <app-error-campo [control]="form.controls.asunto" etiqueta="El asunto" idError="pqrs-asunto-error" />
              </div>
              <div class="campo">
                <label for="pqrs-descripcion" class="etiqueta">Cuéntanos qué pasó</label>
                <textarea id="pqrs-descripcion" class="entrada min-h-32" formControlName="descripcion" maxlength="2000"></textarea>
                <div class="flex justify-between">
                  <app-error-campo [control]="form.controls.descripcion" etiqueta="La descripción" idError="pqrs-descripcion-error" />
                  <span class="ml-auto ayuda">{{ form.controls.descripcion.value.length }}/2000</span>
                </div>
              </div>
              <p class="text-xs text-tenue">
                Usamos tu nombre y tu correo solo para atender esta solicitud, según la
                <a routerLink="/privacidad" class="enlace">política de tratamiento de datos</a>.
                @if (legal()?.correoContacto; as correo) { También puedes escribirnos a {{ correo }}. }
              </p>
              <div class="flex justify-end">
                <button type="submit" class="btn btn-primario" [disabled]="enviando()">
                  <app-icono nombre="enviar" [tamano]="16" />{{ enviando() ? 'Enviando…' : 'Radicar solicitud' }}
                </button>
              </div>
            </form>
          }
        </div>
      } @else {
        <div id="panel-consultar" role="tabpanel" aria-labelledby="tab-consultar">
          <form [formGroup]="consulta" (ngSubmit)="consultar()" class="tarjeta mt-6 space-y-5 p-6" novalidate>
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div class="campo">
                <label for="pqrs-radicado" class="etiqueta">Radicado</label>
                <input id="pqrs-radicado" class="entrada font-mono uppercase" formControlName="radicado" maxlength="30" placeholder="PQR-20261018-A1B2C3" autocomplete="off" />
                <app-error-campo [control]="consulta.controls.radicado" etiqueta="El radicado" idError="pqrs-radicado-error" />
              </div>
              <div class="campo">
                <label for="pqrs-correo-consulta" class="etiqueta">Correo con el que la radicaste</label>
                <input id="pqrs-correo-consulta" type="email" class="entrada" formControlName="correo" maxlength="160" autocomplete="email" inputmode="email" />
                <app-error-campo [control]="consulta.controls.correo" etiqueta="El correo" idError="pqrs-correo-consulta-error" />
              </div>
            </div>
            @if (errorConsulta()) {
              <p class="error-campo" role="alert">{{ errorConsulta() }}</p>
            }
            <div class="flex justify-end">
              <button type="submit" class="btn btn-primario" [disabled]="consultando()">{{ consultando() ? 'Buscando…' : 'Consultar' }}</button>
            </div>
          </form>

          @if (encontrada(); as p) {
            <article class="tarjeta mt-4 p-5" aria-live="polite">
              <div class="flex flex-wrap items-center justify-between gap-2">
                <p class="font-semibold">{{ p.asunto }}</p>
                <span [class]="estadoDe(p.estado).clase">{{ estadoDe(p.estado).texto }}</span>
              </div>
              <p class="mt-1 text-xs text-tenue">
                {{ etiqueta(p.tipo) }} · Radicado <span class="font-mono">{{ p.radicado }}</span> · {{ p.fechaUtc | fecha }}
                @if (p.estado !== 'Respondida') { · respuesta a más tardar el {{ p.fechaLimiteUtc | fecha }} }
              </p>
              @if (p.respuesta) {
                <div class="mt-3 rounded-xl bg-bosque-50 p-3 text-sm whitespace-pre-line dark:bg-bosque-950/50">
                  <p class="mb-1 text-xs font-semibold text-tenue">Respuesta del equipo · {{ p.fechaRespuestaUtc | fecha }}</p>{{ p.respuesta }}
                </div>
              }
            </article>
          }
        </div>
      }
    </div>
  `,
})
export default class Pqrs {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CuentaApi);
  private readonly captcha = inject(CaptchaService);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);
  protected readonly legal = inject(ConfigService).legal;

  protected readonly tipos = TIPOS_PUBLICOS;
  protected readonly etiqueta = etiquetaPqr;
  protected readonly estadoDe = estadoPqr;
  protected readonly vista = signal<'radicar' | 'consultar'>('radicar');
  protected readonly enviando = signal(false);
  protected readonly consultando = signal(false);
  protected readonly radicada = signal<PqrDto | null>(null);
  protected readonly encontrada = signal<PqrDto | null>(null);
  protected readonly errorConsulta = signal<string | null>(null);

  private readonly ruta = inject(ActivatedRoute);
  protected readonly form = this.fb.group({
    tipo: this.fb.control<TipoPqrDto>(this.tipoDeLaRuta()),
    nombre: [this.sesion.usuario()?.nombreCompleto ?? '', [Validators.required, Validators.minLength(3), Validators.maxLength(120), nombrePersona]],
    correo: [this.sesion.usuario()?.correo ?? '', [Validators.required, Validators.email, Validators.maxLength(160)]],
    asunto: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(120), textoSeguro()]],
    descripcion: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000), textoSeguro(true)]],
  });
  protected readonly tipo = toSignal(this.form.controls.tipo.valueChanges, { initialValue: this.form.controls.tipo.value });

  protected readonly consulta = this.fb.group({
    radicado: ['', [Validators.required, Validators.pattern(RE_RADICADO)]],
    correo: ['', [Validators.required, Validators.email, Validators.maxLength(160)]],
  });

  constructor() {
    // Lo que se habló con el asistente llega en memoria (no en la URL) y se aplica una vez, aunque ya estés aquí.
    const borrador = inject(BorradorPqrs);
    effect(() => {
      if (!borrador.pendiente()) return;
      untracked(() => this.aplicar(borrador.tomar()));
    });
  }

  /** `?tipo=` de un enlace o del asistente; solo los tipos que se pueden radicar sin cuenta. */
  private tipoDeLaRuta(): TipoPqrDto {
    const t = this.ruta.snapshot.queryParamMap.get('tipo');
    return esTipoPublico(t) ? t : 'Peticion';
  }

  private aplicar(b: Borrador | null): void {
    if (!b) return;
    this.vista.set('radicar');
    this.radicada.set(null);
    this.form.patchValue({ tipo: esTipoPublico(b.tipo) ? b.tipo : 'Reclamo', asunto: b.asunto, descripcion: b.descripcion });
  }

  protected async radicar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.enviando.set(true);
    try {
      const v = this.form.getRawValue();
      const captchaToken = await this.captcha.token('pqr');
      const pqr = await firstValueFrom(
        this.api.crearPqrPublica({
          tipo: v.tipo,
          nombre: v.nombre.trim(),
          correo: v.correo.trim(),
          asunto: v.asunto.trim(),
          descripcion: v.descripcion.trim(),
          captchaToken,
        }),
      );
      this.radicada.set(pqr);
      this.form.reset({ tipo: 'Peticion', nombre: v.nombre, correo: v.correo, asunto: '', descripcion: '' });
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }

  protected async consultar(): Promise<void> {
    this.consulta.markAllAsTouched();
    if (this.consulta.invalid) return;
    this.consultando.set(true);
    this.errorConsulta.set(null);
    this.encontrada.set(null);
    try {
      const v = this.consulta.getRawValue();
      const captchaToken = await this.captcha.token('pqr');
      this.encontrada.set(await firstValueFrom(this.api.consultarPqr({ radicado: v.radicado.trim(), correo: v.correo.trim(), captchaToken })));
    } catch (e) {
      if (!aplicarErroresServidor(this.consulta.controls, erroresDeCampos(e)))
        this.errorConsulta.set(problemaDe(e).title ?? 'No pudimos consultar la solicitud. Inténtalo de nuevo.');
    } finally {
      this.consultando.set(false);
    }
  }
}
