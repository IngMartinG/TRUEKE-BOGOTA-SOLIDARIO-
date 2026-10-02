import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LOCALIDADES, type SesionDto } from '../../api/tipos';
import { AuthApi } from '../../core/api/auth.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, problemaDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CaptchaService } from '../../core/terceros/captcha.service';
import { CampoClave } from '../../shared/ui/campo-clave';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { claveSegura } from '../../shared/validadores';
import { BotonGoogle } from './boton-google';
import { MarcoAuth } from './marco-auth';
import { destinoSeguro } from './redireccion';

@Component({
  imports: [ReactiveFormsModule, RouterLink, MarcoAuth, CampoClave, ErrorCampo, Icono, BotonGoogle],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-marco-auth titulo="Crea tu cuenta" subtitulo="Únete gratis y recibe Eco-Puntos de bienvenida." lema="Cada objeto que circula es un residuo menos en Doña Juana.">
      <form [formGroup]="form" (ngSubmit)="registrar()" class="space-y-5" novalidate>
        <div class="campo">
          <label class="etiqueta" for="nombre">Nombre completo</label>
          <input id="nombre" class="entrada" formControlName="nombreCompleto" autocomplete="name" maxlength="120"
            [attr.aria-invalid]="form.controls.nombreCompleto.invalid && form.controls.nombreCompleto.touched" aria-describedby="nombre-error" />
          <p class="ayuda">En público se muestra abreviado (por ejemplo, "María R.").</p>
          <app-error-campo [control]="form.controls.nombreCompleto" etiqueta="El nombre" idError="nombre-error" />
        </div>
        <div class="campo">
          <label class="etiqueta" for="localidad">Localidad donde vives</label>
          <select id="localidad" class="entrada" formControlName="localidad" aria-describedby="localidad-error">
            <option value="" disabled>Elige tu localidad</option>
            @for (l of localidades; track l) {
              <option [value]="l">{{ l }}</option>
            }
          </select>
          <app-error-campo [control]="form.controls.localidad" etiqueta="La localidad" idError="localidad-error" />
        </div>
        <div class="campo">
          <label class="etiqueta" for="correo">Correo electrónico</label>
          <input id="correo" type="email" class="entrada" formControlName="correo" autocomplete="email" inputmode="email" maxlength="254"
            [attr.aria-invalid]="form.controls.correo.invalid && form.controls.correo.touched" aria-describedby="correo-error" />
          <app-error-campo [control]="form.controls.correo" etiqueta="El correo" idError="correo-error" />
        </div>
        <app-campo-clave [control]="form.controls.clave" autocompletar="new-password" [mostrarRequisitos]="true" />

        <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-borde p-3 transition hover:bg-superficie-2"
          [class.!border-tierra-500]="form.controls.aceptoPoliticaDatos.invalid && form.controls.aceptoPoliticaDatos.touched">
          <input type="checkbox" class="casilla mt-0.5" formControlName="aceptoPoliticaDatos" aria-describedby="politica-error" />
          <span class="text-sm">
            Autorizo el tratamiento de mis datos personales según la
            <a routerLink="/privacidad" target="_blank" class="enlace">política de tratamiento de datos</a> y acepto los
            <a routerLink="/terminos" target="_blank" class="enlace">términos de uso</a>.
          </span>
        </label>
        <app-error-campo [control]="form.controls.aceptoPoliticaDatos" idError="politica-error" />

        @if (error()) {
          <div class="flex items-start gap-2 rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">
            <app-icono nombre="alerta" [tamano]="18" class="mt-0.5 shrink-0" />{{ error() }}
          </div>
        }

        <button type="submit" class="btn btn-primario btn-lg w-full" [disabled]="enviando()">
          {{ enviando() ? 'Creando tu cuenta…' : 'Crear mi cuenta' }}
        </button>
      </form>

      <app-boton-google texto="signup_with" [deshabilitado]="!aceptoPolitica()" (token)="conGoogle($event)" />
      @if (!aceptoPolitica()) {
        <p class="mt-2 text-center text-xs text-tenue">Marca la autorización de datos para continuar con Google.</p>
      }

      <p class="mt-8 text-center text-sm text-tenue">
        ¿Ya tienes cuenta? <a routerLink="/ingresar" [queryParams]="{ volver: volver() }" class="enlace">Ingresa</a>
      </p>
    </app-marco-auth>
  `,
})
export default class Registro {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly authApi = inject(AuthApi);
  private readonly sesion = inject(SesionService);
  private readonly captcha = inject(CaptchaService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);

  readonly volver = input<string>();
  protected readonly localidades = LOCALIDADES;
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    nombreCompleto: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    localidad: ['', [Validators.required]],
    correo: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    clave: ['', [Validators.required, claveSegura]],
    aceptoPoliticaDatos: [false, [Validators.requiredTrue]],
  });
  protected readonly aceptoPolitica = toSignal(this.form.controls.aceptoPoliticaDatos.valueChanges, { initialValue: false });

  protected async registrar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.enviando.set(true);
    this.error.set(null);
    try {
      const v = this.form.getRawValue();
      const captchaToken = await this.captcha.token('registro');
      const sesion = await firstValueFrom(
        this.authApi.registrar({
          nombreCompleto: v.nombreCompleto.trim(),
          localidad: v.localidad,
          correo: v.correo.trim(),
          clave: v.clave,
          aceptoPoliticaDatos: true,
          captchaToken,
        }),
      );
      await this.exito(sesion, true);
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) {
        this.error.set(problemaDe(e).title ?? 'No pudimos crear tu cuenta.');
      }
    } finally {
      this.enviando.set(false);
    }
  }

  protected async conGoogle(idToken: string): Promise<void> {
    this.enviando.set(true);
    this.error.set(null);
    try {
      const sesion = await firstValueFrom(this.authApi.google({ idToken, aceptoPoliticaDatos: true }));
      await this.exito(sesion, false);
    } catch (e) {
      const p = problemaDe(e);
      this.error.set(
        p.codigo === '2fa_requerido'
          ? 'Esta cuenta ya existe y tiene verificación en dos pasos. Ingresa desde la página de inicio de sesión.'
          : (p.title ?? 'No pudimos continuar con Google.'),
      );
    } finally {
      this.enviando.set(false);
    }
  }

  private async exito(sesion: SesionDto, porCorreo: boolean): Promise<void> {
    this.sesion.establecer(sesion);
    this.avisos.puntos(`¡Te damos la bienvenida, ${this.sesion.primerNombre()}!`, 'Ya tienes tus Eco-Puntos de bienvenida.');
    await this.router.navigateByUrl(
      porCorreo && !this.sesion.correoVerificado() ? '/verifica-tu-correo' : destinoSeguro(this.volver()),
    );
  }
}
