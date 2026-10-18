import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { SesionDto } from '../../api/tipos';
import { AuthApi } from '../../core/api/auth.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, problemaDe } from '../../core/http/problema';
import { codigoInvitacionGuardado, guardarCodigoInvitacion, olvidarCodigoInvitacion } from '../../core/invitacion';
import { SesionService } from '../../core/sesion.service';
import { CaptchaService } from '../../core/terceros/captcha.service';
import { CampoClave } from '../../shared/ui/campo-clave';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { CampoAccesible } from '../../shared/ui/campo-accesible';
import { Icono } from '../../shared/ui/icono';
import { CasillasLegales } from '../../shared/ui/casillas-legales';
import { Modal } from '../../shared/ui/modal';
import { CampoFechaNacimiento } from '../../shared/ui/campo-fecha-nacimiento';
import { mayorDeEdad } from '../../shared/validadores';
import { BotonGoogle } from './boton-google';
import { MarcoAuth } from './marco-auth';
import { destinoSeguro } from './redireccion';

@Component({
  imports: [ReactiveFormsModule, RouterLink, MarcoAuth, CampoClave, ErrorCampo, CampoAccesible, Icono, BotonGoogle, Modal, CampoFechaNacimiento, CasillasLegales],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-marco-auth [conCaptcha]="true"
      [titulo]="pedirCodigo() ? 'Verificación en dos pasos' : 'Hola de nuevo'"
      [subtitulo]="pedirCodigo() ? 'Escribe el código de 6 dígitos de tu app autenticadora o uno de tus códigos de recuperación.' : 'Ingresa para seguir intercambiando con tu comunidad.'"
    >
      <form [formGroup]="form" (ngSubmit)="ingresar()" class="space-y-5" novalidate>
        @if (!pedirCodigo()) {
          <div class="campo">
            <label class="etiqueta" for="correo">Correo electrónico</label>
            <input id="correo" type="email" class="entrada" formControlName="correo" autocomplete="email" inputmode="email" aria-describedby="correo-error" />
            <app-error-campo [control]="form.controls.correo" etiqueta="El correo" idError="correo-error" />
          </div>
          <app-campo-clave [control]="form.controls.clave">
            <a accion routerLink="/olvide-clave" class="text-xs font-semibold text-bosque-700 hover:underline dark:text-bosque-300">¿La olvidaste?</a>
          </app-campo-clave>
        } @else {
          <div class="campo">
            <label class="etiqueta" for="codigo">Código de verificación</label>
            <input id="codigo" class="entrada text-center font-mono text-2xl tracking-[0.4em]" formControlName="codigoDosFactores"
              autocomplete="one-time-code" inputmode="text" maxlength="20" placeholder="000000" aria-describedby="codigo-error" />
            <app-error-campo [control]="form.controls.codigoDosFactores" etiqueta="El código" idError="codigo-error" />
          </div>
        }

        @if (error()) {
          <div class="flex items-start gap-2 rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">
            <app-icono nombre="alerta" [tamano]="18" class="mt-0.5 shrink-0" />{{ error() }}
          </div>
        }

        <button type="submit" class="btn btn-primario btn-lg w-full" [disabled]="enviando()">
          {{ enviando() ? 'Ingresando…' : pedirCodigo() ? 'Verificar e ingresar' : 'Ingresar' }}
        </button>
        @if (pedirCodigo()) {
          <button type="button" class="btn btn-fantasma w-full" (click)="volverAtras()">Volver</button>
        }
      </form>

      @if (!pedirCodigo()) {
        <app-boton-google texto="signin_with" (token)="conGoogle($event, false)" />
        <p class="mt-8 text-center text-sm text-tenue">
          ¿Aún no tienes cuenta? <a routerLink="/registro" [queryParams]="{ volver: volver() }" class="enlace">Créala gratis</a>
        </p>
      }
    </app-marco-auth>

    <app-modal [(abierto)]="consentimientoAbierto" titulo="Crear tu cuenta con Google" subtitulo="Es la primera vez que ingresas con esta cuenta de Google.">
      <app-campo-fecha-nacimiento [control]="fechaGoogle" idCampo="fecha-google" />
      <app-casillas-legales class="mt-4 block" [datos]="aceptaPolitica" [terminos]="aceptaTerminos" prefijo="google" />
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="consentimientoAbierto.set(false)">Cancelar</button>
        <button type="button" class="btn btn-primario" [disabled]="enviando()" (click)="crearConGoogle()">Crear mi cuenta</button>
      </div>
    </app-modal>
  `,
})
export default class Ingresar {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly authApi = inject(AuthApi);
  private readonly sesion = inject(SesionService);
  private readonly captcha = inject(CaptchaService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);

  readonly volver = input<string>();
  /** `?invita=CODIGO`: por si la persona crea su cuenta con Google desde aquí. */
  readonly invita = input<string>();
  protected readonly pedirCodigo = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly consentimientoAbierto = signal(false);
  /** Cuenta nueva con Google: las dos casillas, separadas y sin marcar. */
  protected readonly aceptaPolitica = this.fb.control(false, [Validators.requiredTrue]);
  protected readonly aceptaTerminos = this.fb.control(false, [Validators.requiredTrue]);
  protected idTokenGoogle: string | null = null;
  /** Solo para cuentas nuevas con Google: la plataforma es para mayores de 18 años. */
  protected readonly fechaGoogle = this.fb.control('', [Validators.required, mayorDeEdad]);
  private viaGoogle = false;

  constructor() {
    effect(() => {
      const c = this.invita();
      if (c) guardarCodigoInvitacion(c);
    });
  }

  protected readonly form = this.fb.group({
    correo: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    clave: ['', [Validators.required]],
    codigoDosFactores: [''],
  });

  protected async ingresar(): Promise<void> {
    if (this.viaGoogle && this.pedirCodigo() && this.idTokenGoogle) {
      await this.conGoogle(this.idTokenGoogle, this.aceptaPolitica.value && this.aceptaTerminos.value);
      return;
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.enviando.set(true);
    this.error.set(null);
    try {
      const { correo, clave, codigoDosFactores } = this.form.getRawValue();
      const captchaToken = await this.captcha.token('login');
      const sesion = await firstValueFrom(
        this.authApi.login({ correo: correo.trim(), clave, codigoDosFactores: codigoDosFactores.trim() || null, captchaToken }),
      );
      await this.exito(sesion);
    } catch (e) {
      this.manejarError(e);
    } finally {
      this.enviando.set(false);
    }
  }

  protected async conGoogle(idToken: string, aceptaLegal: boolean): Promise<void> {
    this.idTokenGoogle = idToken;
    this.viaGoogle = true;
    this.enviando.set(true);
    this.error.set(null);
    try {
      const codigo = this.form.controls.codigoDosFactores.value.trim() || null;
      const invita = codigoInvitacionGuardado();
      const fechaNacimiento = aceptaLegal ? this.fechaGoogle.value : null;
      const sesion = await firstValueFrom(
        this.authApi.google({ idToken, aceptoPoliticaDatos: aceptaLegal, aceptoTerminos: aceptaLegal, fechaNacimiento, codigoDosFactores: codigo, ...(invita ? { codigoInvitacion: invita } : {}) }),
      );
      this.consentimientoAbierto.set(false);
      await this.exito(sesion);
    } catch (e) {
      const p = problemaDe(e);
      if (p.status === 400 && p.title?.toLowerCase().includes('política') && !aceptaLegal) {
        this.consentimientoAbierto.set(true);
      } else if (p.status === 400 && aceptaLegal && this.consentimientoAbierto()) {
        // Menor de edad o fecha no válida: se muestra junto al campo y el diálogo sigue abierto.
        this.fechaGoogle.setErrors({ servidor: p.title ?? 'Revisa la fecha de nacimiento.' });
        this.fechaGoogle.markAsTouched();
      } else {
        this.consentimientoAbierto.set(false);
        this.manejarError(e);
      }
    } finally {
      this.enviando.set(false);
    }
  }

  protected async crearConGoogle(): Promise<void> {
    this.fechaGoogle.markAsTouched();
    this.aceptaPolitica.markAsTouched();
    this.aceptaTerminos.markAsTouched();
    if (this.fechaGoogle.invalid || this.aceptaPolitica.invalid || this.aceptaTerminos.invalid || !this.idTokenGoogle) return;
    await this.conGoogle(this.idTokenGoogle, true);
  }

  protected volverAtras(): void {
    this.pedirCodigo.set(false);
    this.viaGoogle = false;
    this.error.set(null);
    this.form.controls.codigoDosFactores.reset('');
  }

  private manejarError(e: unknown): void {
    const p = problemaDe(e);
    if (p.codigo === '2fa_requerido') {
      if (this.pedirCodigo()) this.error.set('El código no es correcto o ya se usó. Inténtalo de nuevo.');
      this.pedirCodigo.set(true);
      this.form.controls.codigoDosFactores.setValidators([Validators.required, Validators.minLength(6)]);
      this.form.controls.codigoDosFactores.updateValueAndValidity();
      return;
    }
    if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) {
      this.error.set(p.title ?? 'No pudimos iniciar sesión.');
    }
  }

  private async exito(sesion: SesionDto): Promise<void> {
    // Con correo y clave no se envió código: solo se descarta tras el login con Google.
    if (this.viaGoogle) olvidarCodigoInvitacion();
    this.sesion.establecer(sesion);
    this.avisos.exito(`¡Hola, ${this.sesion.primerNombre()}!`);
    await this.router.navigateByUrl(destinoSeguro(this.volver()));
  }
}
