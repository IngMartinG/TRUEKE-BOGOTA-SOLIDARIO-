import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormsModule, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { SesionDto } from '../../api/tipos';
import { AuthApi } from '../../core/api/auth.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, problemaDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CaptchaService } from '../../core/terceros/captcha.service';
import { CampoClave } from '../../shared/ui/campo-clave';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { BotonGoogle } from './boton-google';
import { MarcoAuth } from './marco-auth';
import { destinoSeguro } from './redireccion';

@Component({
  imports: [ReactiveFormsModule, FormsModule, RouterLink, MarcoAuth, CampoClave, ErrorCampo, Icono, BotonGoogle, Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-marco-auth
      [titulo]="pedirCodigo() ? 'Verificación en dos pasos' : 'Hola de nuevo'"
      [subtitulo]="pedirCodigo() ? 'Escribe el código de 6 dígitos de tu app autenticadora o uno de tus códigos de recuperación.' : 'Ingresa para seguir intercambiando con tu comunidad.'"
    >
      <form [formGroup]="form" (ngSubmit)="ingresar()" class="space-y-5" novalidate>
        @if (!pedirCodigo()) {
          <div class="campo">
            <label class="etiqueta" for="correo">Correo electrónico</label>
            <input id="correo" type="email" class="entrada" formControlName="correo" autocomplete="email" inputmode="email"
              [attr.aria-invalid]="form.controls.correo.invalid && form.controls.correo.touched" aria-describedby="correo-error" />
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
      <label class="flex cursor-pointer items-start gap-3">
        <input type="checkbox" class="casilla mt-0.5" [(ngModel)]="aceptaPolitica" />
        <span class="text-sm">
          Autorizo el tratamiento de mis datos personales según la
          <a routerLink="/privacidad" target="_blank" class="enlace">política de tratamiento de datos</a> (Ley 1581 de 2012).
        </span>
      </label>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="consentimientoAbierto.set(false)">Cancelar</button>
        <button type="button" class="btn btn-primario" [disabled]="!aceptaPolitica() || enviando()" (click)="conGoogle(idTokenGoogle!, true)">Crear mi cuenta</button>
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
  protected readonly pedirCodigo = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly consentimientoAbierto = signal(false);
  protected readonly aceptaPolitica = signal(false);
  protected idTokenGoogle: string | null = null;
  private viaGoogle = false;

  protected readonly form = this.fb.group({
    correo: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    clave: ['', [Validators.required]],
    codigoDosFactores: [''],
  });

  protected async ingresar(): Promise<void> {
    if (this.viaGoogle && this.pedirCodigo() && this.idTokenGoogle) {
      await this.conGoogle(this.idTokenGoogle, this.aceptaPolitica());
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

  protected async conGoogle(idToken: string, aceptoPoliticaDatos: boolean): Promise<void> {
    this.idTokenGoogle = idToken;
    this.viaGoogle = true;
    this.enviando.set(true);
    this.error.set(null);
    try {
      const codigo = this.form.controls.codigoDosFactores.value.trim() || null;
      const sesion = await firstValueFrom(this.authApi.google({ idToken, aceptoPoliticaDatos, codigoDosFactores: codigo }));
      this.consentimientoAbierto.set(false);
      await this.exito(sesion);
    } catch (e) {
      const p = problemaDe(e);
      if (p.status === 400 && p.title?.toLowerCase().includes('política') && !aceptoPoliticaDatos) {
        this.consentimientoAbierto.set(true);
      } else {
        this.consentimientoAbierto.set(false);
        this.manejarError(e);
      }
    } finally {
      this.enviando.set(false);
    }
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
    this.sesion.establecer(sesion);
    this.avisos.exito(`¡Hola, ${this.sesion.primerNombre()}!`);
    await this.router.navigateByUrl(destinoSeguro(this.volver()));
  }
}
