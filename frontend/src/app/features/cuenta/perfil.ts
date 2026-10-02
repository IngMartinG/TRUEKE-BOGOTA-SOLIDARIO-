import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LOCALIDADES } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { FechaPipe, NumeroPipe } from '../../shared/pipes';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [ReactiveFormsModule, RouterLink, ErrorCampo, Icono, NumeroPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Mi cuenta</h1>
    <p class="mt-1 text-tenue">Tu información básica y el estado de tu cuenta.</p>

    @if (sesion.usuario(); as u) {
      <div class="mt-6 grid gap-3 sm:grid-cols-4">
        @for (d of [
          { t: 'Eco-Puntos', v: (u.saldoEcoPuntos | numero), i: 'moneda' },
          { t: 'Reputación', v: (u.reputacion ?? 0).toFixed(1), i: 'hoja' },
          { t: 'Truekes', v: u.truekesCompletados ?? 0, i: 'repeat' },
          { t: 'Donaciones', v: u.donacionesRealizadas ?? 0, i: 'heart' }
        ]; track d.t) {
          <div class="tarjeta p-4">
            <app-icono [nombre]="d.i" [tamano]="20" class="text-bosque-600 dark:text-bosque-300" />
            <p class="mt-2 font-display text-2xl font-extrabold">{{ d.v }}</p>
            <p class="text-xs text-tenue">{{ d.t }}</p>
          </div>
        }
      </div>

      <div class="mt-6 grid gap-6 xl:grid-cols-[1fr_20rem]">
        <form [formGroup]="form" (ngSubmit)="guardar()" class="tarjeta space-y-5 p-6" novalidate>
          <h2 class="text-lg font-bold">Datos personales</h2>
          <div class="campo">
            <label for="nombre" class="etiqueta">Nombre completo</label>
            <input id="nombre" class="entrada" formControlName="nombreCompleto" maxlength="120" autocomplete="name" aria-describedby="nombre-error" />
            <app-error-campo [control]="form.controls.nombreCompleto" etiqueta="El nombre" idError="nombre-error" />
          </div>
          <div class="campo">
            <label for="localidad" class="etiqueta">Localidad</label>
            <select id="localidad" class="entrada" formControlName="localidad">
              @for (l of localidades; track l) {
                <option [value]="l">{{ l }}</option>
              }
            </select>
          </div>
          <div class="campo">
            <span class="etiqueta">Correo</span>
            <p class="flex items-center gap-2 rounded-xl bg-superficie-2 px-4 py-2.5 text-sm">
              {{ u.correo }}
              @if (u.correoVerificado) {
                <span class="insignia-trueke ml-auto"><app-icono nombre="check" [tamano]="12" />Verificado</span>
              } @else {
                <a routerLink="/verifica-tu-correo" class="insignia-sol ml-auto">Sin verificar</a>
              }
            </p>
            <p class="ayuda">Nunca se muestra a otras personas.</p>
          </div>
          <div class="flex justify-end">
            <button type="submit" class="btn btn-primario" [disabled]="form.pristine || guardando()">{{ guardando() ? 'Guardando…' : 'Guardar cambios' }}</button>
          </div>
        </form>

        <div class="space-y-4">
          <div class="tarjeta p-5">
            <h2 class="font-bold">Tipo de cuenta</h2>
            <p class="mt-2 flex items-center gap-2 font-display text-xl font-extrabold">
              <app-icono [nombre]="u.tipoCuenta === 'Individual' ? 'usuario' : u.tipoCuenta === 'Empresa' ? 'edificio' : 'corona'" [tamano]="20" class="text-sol-500" />{{ u.tipoCuenta }}
            </p>
            @if (u.planVigenteHasta) {
              <p class="text-sm text-tenue">Vigente hasta el {{ u.planVigenteHasta | fecha }}</p>
            }
            <a routerLink="/eco-puntos" class="btn btn-secundario btn-sm mt-4 w-full">Ver planes</a>
          </div>
          <div class="tarjeta p-5">
            <h2 class="font-bold">Verificación de identidad</h2>
            <p class="mt-2 text-sm text-tenue">
              @switch (u.estadoVerificacion) {
                @case ('Aprobada') { Tu cuenta está verificada. ¡Gracias por generar confianza! }
                @case ('Pendiente') { Tu documento está en revisión. Te avisaremos pronto. }
                @case ('Rechazada') { La verificación fue rechazada. Puedes intentarlo de nuevo con un documento legible. }
                @default { Verifica tu identidad para mostrar la insignia y aparecer en el filtro de cuentas verificadas. }
              }
            </p>
            @if (u.estadoVerificacion !== 'Aprobada' && u.estadoVerificacion !== 'Pendiente') {
              <a routerLink="/eco-puntos" class="btn btn-primario btn-sm mt-4 w-full"><app-icono nombre="verificado" [tamano]="14" />Verificar mi cuenta</a>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export default class Perfil {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);
  protected readonly localidades = LOCALIDADES;
  protected readonly guardando = signal(false);
  protected readonly form = this.fb.group({
    nombreCompleto: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    localidad: ['', Validators.required],
  });

  constructor() {
    // Refresca los datos al entrar (saldo y verificación pueden haber cambiado).
    this.sesion.recargarUsuario();
    effect(() => {
      const u = this.sesion.usuario();
      untracked(() => {
        if (u && this.form.pristine) this.form.reset({ nombreCompleto: u.nombreCompleto ?? '', localidad: u.localidad ?? '' });
      });
    });
  }

  protected async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.guardando.set(true);
    try {
      const v = this.form.getRawValue();
      const u = await firstValueFrom(this.api.actualizarPerfil({ nombreCompleto: v.nombreCompleto.trim(), localidad: v.localidad }));
      this.sesion.actualizarUsuario(u);
      this.form.markAsPristine();
      this.avisos.exito('Perfil actualizado');
    } catch (e) {
      if (!aplicarErroresServidor(this.form.controls, erroresDeCampos(e))) this.avisos.error(mensajeDe(e));
    } finally {
      this.guardando.set(false);
    }
  }
}
