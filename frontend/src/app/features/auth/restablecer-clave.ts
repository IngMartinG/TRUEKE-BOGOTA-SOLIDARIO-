import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/api/auth.api';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CampoClave } from '../../shared/ui/campo-clave';
import { Icono } from '../../shared/ui/icono';
import { claveSegura, coinciden } from '../../shared/validadores';
import { MarcoAuth } from './marco-auth';

@Component({
  imports: [ReactiveFormsModule, RouterLink, MarcoAuth, CampoClave, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-marco-auth titulo="Crea una nueva contraseña" subtitulo="Por seguridad, se cerrarán las sesiones abiertas en otros dispositivos.">
      @if (!token) {
        <p class="rounded-xl bg-tierra-50 p-4 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">
          El enlace no es válido o está incompleto. Solicita uno nuevo.
        </p>
        <a routerLink="/olvide-clave" class="btn btn-primario mt-6 w-full">Solicitar otro enlace</a>
      } @else if (listo()) {
        <div class="rounded-tarjeta border border-bosque-200 bg-bosque-50 p-6 text-center dark:border-bosque-800 dark:bg-bosque-950/50" role="status">
          <span class="mx-auto grid size-14 place-items-center rounded-full bg-bosque-600 text-white"><app-icono nombre="check" [tamano]="28" /></span>
          <h2 class="mt-4 text-lg font-bold">¡Contraseña actualizada!</h2>
          <p class="mt-2 text-sm text-tenue">Ya puedes ingresar con tu nueva contraseña.</p>
          <a routerLink="/ingresar" class="btn btn-primario mt-6">Ingresar</a>
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="guardar()" class="space-y-5" novalidate>
          <app-campo-clave [control]="form.controls.clave" etiqueta="Nueva contraseña" autocompletar="new-password" [mostrarRequisitos]="true" />
          <app-campo-clave [control]="form.controls.confirmacion" etiqueta="Repite la contraseña" idCampo="confirmacion" autocompletar="new-password" />
          @if (error()) {
            <p class="error-campo" role="alert">{{ error() }}</p>
          }
          <button type="submit" class="btn btn-primario btn-lg w-full" [disabled]="enviando()">
            {{ enviando() ? 'Guardando…' : 'Guardar contraseña' }}
          </button>
        </form>
      }
    </app-marco-auth>
  `,
})
export default class RestablecerClave implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(AuthApi);
  private readonly ruta = inject(ActivatedRoute);
  private readonly sesion = inject(SesionService);
  protected token: string | null = null;
  protected readonly listo = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = this.fb.group(
    { clave: ['', [Validators.required, claveSegura]], confirmacion: ['', [Validators.required]] },
    { validators: coinciden('clave', 'confirmacion') },
  );

  ngOnInit(): void {
    this.token = this.ruta.snapshot.queryParamMap.get('token');
    // El token es de un solo uso y sensible: se quita de la barra de direcciones y del historial.
    history.replaceState(history.state, '', '/restablecer-clave');
  }

  protected async guardar(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.token) return;
    this.enviando.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.restablecerClave(this.token, this.form.controls.clave.value));
      this.sesion.limpiar();
      this.listo.set(true);
    } catch (e) {
      this.error.set(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
