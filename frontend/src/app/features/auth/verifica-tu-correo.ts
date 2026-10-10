import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/api/auth.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor flex min-h-[60vh] max-w-lg flex-col items-center justify-center py-16 text-center">
      <div class="relative">
        <div class="absolute inset-0 scale-150 rounded-full bg-sol-300/30 blur-2xl"></div>
        <span class="relative grid size-24 place-items-center rounded-[2rem] bg-gradient-to-br from-sol-300 to-sol-500 text-bosque-950 shadow-elevada">
          <app-icono nombre="correo" [tamano]="44" [grosor]="1.6" />
        </span>
      </div>
      <h1 class="mt-8 text-3xl font-extrabold">Confirma tu correo</h1>
      <p class="mt-3 text-tenue">
        Enviamos un enlace a <strong class="text-tinta">{{ sesion.usuario()?.correo }}</strong>. Ábrelo para activar tu cuenta.
        Hasta entonces solo puedes explorar el catálogo, como cualquier visitante.
      </p>
      <ul class="mt-6 space-y-2 text-left text-sm text-tenue">
        <li class="flex gap-2"><app-icono nombre="check" [tamano]="16" class="mt-0.5 text-bosque-600" />Revisa la carpeta de spam o promociones.</li>
        <li class="flex gap-2"><app-icono nombre="check" [tamano]="16" class="mt-0.5 text-bosque-600" />El enlace vence después de un tiempo; puedes pedir otro.</li>
      </ul>
      <div class="mt-8 flex flex-wrap justify-center gap-3">
        <button type="button" class="btn btn-primario" (click)="comprobar()" [disabled]="comprobando()">
          <app-icono nombre="refrescar" [tamano]="16" /> Ya lo confirmé
        </button>
        <button type="button" class="btn btn-secundario" (click)="reenviar()" [disabled]="enviando() || esperando() > 0">
          {{ esperando() > 0 ? 'Reenviar en ' + esperando() + ' s' : 'Reenviar el correo' }}
        </button>
      </div>
      <a routerLink="/explorar" class="enlace mt-6 text-sm">Seguir explorando mientras tanto</a>
      <p class="mt-4 text-sm text-tenue">
        ¿Escribiste mal el correo?
        <button type="button" class="enlace" (click)="sesion.salir('/registro')">Cierra sesión y regístrate de nuevo</button>
      </p>
    </div>
  `,
})
export default class VerificaTuCorreo {
  private readonly authApi = inject(AuthApi);
  private readonly cuentaApi = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  protected readonly sesion = inject(SesionService);
  protected readonly enviando = signal(false);
  protected readonly comprobando = signal(false);
  protected readonly esperando = signal(0);

  constructor() {
    effect(() => {
      if (this.sesion.correoVerificado()) void this.router.navigateByUrl('/publicar');
    });
  }

  protected async reenviar(): Promise<void> {
    this.enviando.set(true);
    try {
      await firstValueFrom(this.authApi.reenviarVerificacion());
      this.avisos.exito('Correo enviado', 'Revisa tu bandeja de entrada.');
      this.esperando.set(60);
      const t = setInterval(() => {
        this.esperando.update((s) => s - 1);
        if (this.esperando() <= 0) clearInterval(t);
      }, 1000);
    } catch {
      // El interceptor ya mostró el error (incluido el límite de envíos por hora).
    } finally {
      this.enviando.set(false);
    }
  }

  protected async comprobar(): Promise<void> {
    this.comprobando.set(true);
    try {
      const u = await firstValueFrom(this.cuentaApi.yo());
      this.sesion.actualizarUsuario(u);
      if (!u.correoVerificado) this.avisos.info('Aún no vemos la confirmación', 'Abre el enlace del correo y vuelve a intentarlo.');
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.comprobando.set(false);
    }
  }
}
