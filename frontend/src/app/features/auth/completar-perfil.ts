import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, problemaDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CampoFechaNacimiento } from '../../shared/ui/campo-fecha-nacimiento';
import { aplicarErroresServidor } from '../../shared/ui/error-campo';
import { CasillasLegales } from '../../shared/ui/casillas-legales';
import { EnfocarError } from '../../shared/ui/enfocar-error';
import { Icono } from '../../shared/ui/icono';
import { mayorDeEdad } from '../../shared/validadores';
import { MarcoAuth } from './marco-auth';
import { destinoSeguro } from './redireccion';

/**
 * Lo que una cuenta debe completar antes de seguir: la fecha de nacimiento (cuentas creadas antes de pedirla; la plataforma
 * es solo para mayores de 18 años) y la aceptación de las versiones vigentes de la política de datos y los términos (cuando
 * cambian). Mientras tanto la API solo deja gestionar la cuenta, exportar o eliminar los datos y radicar PQR.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, MarcoAuth, CampoFechaNacimiento, CasillasLegales, EnfocarError, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-marco-auth [titulo]="pideFecha() ? 'Confirma tu mayoría de edad' : 'Actualizamos nuestras condiciones'"
      [subtitulo]="pideFecha()
        ? 'Trueke Bogotá Solidario es solo para personas mayores de 18 años. Registra tu fecha de nacimiento una sola vez para seguir.'
        : 'Cambiamos la política de tratamiento de datos y los términos de uso. Léelos y acéptalos para seguir usando Trueke.'"
      lema="Intercambios entre personas adultas que se hacen responsables de lo que acuerdan.">
      <form [formGroup]="form" (ngSubmit)="guardar()" class="space-y-5" novalidate>
        @if (pideFecha()) {
          <app-campo-fecha-nacimiento [control]="form.controls.fechaNacimiento" />
        }
        @if (pideLegal()) {
          <app-casillas-legales [datos]="form.controls.aceptoPoliticaDatos" [terminos]="form.controls.aceptoTerminos" prefijo="completar" />
        }

        @if (esMenor()) {
          <div class="flex items-start gap-2 rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">
            <app-icono nombre="alerta" [tamano]="18" class="mt-0.5 shrink-0" />
            <span>
              Si eres menor de edad no puedes usar la plataforma. Puedes descargar o eliminar tu cuenta y tus datos en
              <a routerLink="/cuenta/privacidad" class="enlace">Privacidad y datos</a>.
            </span>
          </div>
        }
        @if (error()) {
          <div class="flex items-start gap-2 rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">
            <app-icono nombre="alerta" [tamano]="18" class="mt-0.5 shrink-0" />{{ error() }}
          </div>
        }

        @if (pideFecha()) {
          <p class="text-sm text-tenue">
            La fecha queda guardada para comprobar tu edad y no se puede cambiar después. Lee cómo la tratamos en la
            <a routerLink="/privacidad" class="enlace">política de tratamiento de datos</a>.
          </p>
        }
        <button type="submit" class="btn btn-primario btn-lg w-full" [disabled]="enviando()">
          {{ enviando() ? 'Guardando…' : 'Guardar y continuar' }}
        </button>
        <button type="button" class="btn btn-fantasma w-full" (click)="salir()">Salir de mi cuenta</button>
      </form>
    </app-marco-auth>
  `,
})
export default class CompletarPerfil {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly cuentaApi = inject(CuentaApi);
  private readonly sesion = inject(SesionService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);

  readonly volver = input<string>();
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly esMenor = signal(false);

  /** Se calcula una vez al entrar: al guardar la fecha no debe desaparecer el campo mientras se envía. */
  protected readonly pideFecha = signal(!this.sesion.mayoriaEdadConfirmada());
  protected readonly pideLegal = signal(this.sesion.legalPendiente());

  protected readonly form = this.fb.group({
    fechaNacimiento: ['', this.pideFecha() ? [Validators.required, mayorDeEdad] : []],
    aceptoPoliticaDatos: [false, this.pideLegal() ? [Validators.requiredTrue] : []],
    aceptoTerminos: [false, this.pideLegal() ? [Validators.requiredTrue] : []],
  });

  constructor() {
    // Nada pendiente (otra pestaña lo completó, o llegó aquí por un enlace): no hay nada que hacer.
    effect(() => {
      if (this.sesion.usuario() && this.sesion.mayoriaEdadConfirmada() && !this.sesion.legalPendiente() && !this.enviando())
        void this.router.navigateByUrl(destinoSeguro(this.volver()));
    });
    this.form.controls.fechaNacimiento.statusChanges.pipe(takeUntilDestroyed()).subscribe(() => this.esMenor.set(!!this.form.controls.fechaNacimiento.errors?.['menorDeEdad']));
  }

  protected async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.enviando.set(true);
    this.error.set(null);
    try {
      let usuario = null;
      if (this.pideFecha() && !this.sesion.mayoriaEdadConfirmada())
        usuario = await firstValueFrom(this.cuentaApi.registrarFechaNacimiento({ fechaNacimiento: this.form.controls.fechaNacimiento.value }));
      if (this.pideLegal())
        usuario = await firstValueFrom(this.cuentaApi.aceptarLegal({ aceptoPoliticaDatos: true, aceptoTerminos: true }));
      // El token actual no dice que ya se completó: se pide uno nuevo antes de seguir.
      await firstValueFrom(this.sesion.refrescar());
      if (usuario) this.sesion.actualizarUsuario(usuario);
      this.avisos.exito('Listo, ya puedes seguir usando Trueke.');
      await this.router.navigateByUrl(destinoSeguro(this.volver()));
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) {
        const p = problemaDe(e);
        if (p.title?.includes('mayores de 18')) this.esMenor.set(true);
        else this.error.set(p.title ?? 'No pudimos guardar los datos. Inténtalo de nuevo.');
      }
    } finally {
      this.enviando.set(false);
    }
  }

  protected salir(): void {
    void this.sesion.salir('/');
  }
}
