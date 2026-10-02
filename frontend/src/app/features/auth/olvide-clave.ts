import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/api/auth.api';
import { mensajeDe } from '../../core/http/problema';
import { CaptchaService } from '../../core/terceros/captcha.service';
import { ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { MarcoAuth } from './marco-auth';

@Component({
  imports: [ReactiveFormsModule, RouterLink, MarcoAuth, ErrorCampo, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-marco-auth titulo="¿Olvidaste tu contraseña?" subtitulo="Te enviaremos un enlace para crear una nueva.">
      @if (enviado()) {
        <div class="rounded-tarjeta border border-bosque-200 bg-bosque-50 p-6 text-center dark:border-bosque-800 dark:bg-bosque-950/50" role="status">
          <span class="mx-auto grid size-14 place-items-center rounded-full bg-bosque-600 text-white"><app-icono nombre="correo" [tamano]="26" /></span>
          <h2 class="mt-4 text-lg font-bold">Revisa tu correo</h2>
          <p class="mt-2 text-sm text-tenue">
            Si existe una cuenta con <strong class="text-tinta">{{ form.controls.correo.value }}</strong>, recibirás un enlace válido por tiempo limitado.
            Revisa también la carpeta de spam.
          </p>
          <a routerLink="/ingresar" class="btn btn-primario mt-6">Volver a ingresar</a>
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="enviar()" class="space-y-5" novalidate>
          <div class="campo">
            <label class="etiqueta" for="correo">Correo de tu cuenta</label>
            <input id="correo" type="email" class="entrada" formControlName="correo" autocomplete="email" inputmode="email" aria-describedby="correo-error" />
            <app-error-campo [control]="form.controls.correo" etiqueta="El correo" idError="correo-error" />
          </div>
          @if (error()) {
            <p class="error-campo" role="alert">{{ error() }}</p>
          }
          <button type="submit" class="btn btn-primario btn-lg w-full" [disabled]="enviando()">
            {{ enviando() ? 'Enviando…' : 'Enviar enlace' }}
          </button>
          <p class="text-center text-sm"><a routerLink="/ingresar" class="enlace">Volver a ingresar</a></p>
        </form>
      }
    </app-marco-auth>
  `,
})
export default class OlvideClave {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(AuthApi);
  private readonly captcha = inject(CaptchaService);
  protected readonly enviado = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = this.fb.group({ correo: ['', [Validators.required, Validators.email]] });

  protected async enviar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.enviando.set(true);
    this.error.set(null);
    try {
      const token = await this.captcha.token('olvide_clave');
      await firstValueFrom(this.api.olvideClave(this.form.controls.correo.value.trim(), token));
      this.enviado.set(true);
    } catch (e) {
      this.error.set(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
