import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/api/auth.api';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { Icono } from '../../shared/ui/icono';

/** Destino del enlace del correo de verificación: confirma el token y lo borra de la URL. */
@Component({
  imports: [RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor flex min-h-[60vh] max-w-md flex-col items-center justify-center py-16 text-center">
      @switch (estado()) {
        @case ('verificando') {
          <span class="size-14 animate-spin rounded-full border-4 border-bosque-200 border-t-bosque-600" aria-hidden="true"></span>
          <h1 class="mt-6 text-2xl font-bold">Confirmando tu correo…</h1>
        }
        @case ('ok') {
          <span class="grid size-20 place-items-center rounded-full bg-bosque-600 text-white shadow-elevada"><app-icono nombre="check" [tamano]="40" /></span>
          <h1 class="mt-6 text-2xl font-bold">¡Correo confirmado!</h1>
          <p class="mt-2 text-tenue">Ya puedes publicar, solicitar intercambios y chatear con la comunidad.</p>
          <div class="mt-8 flex flex-wrap justify-center gap-3">
            <a routerLink="/publicar" class="btn btn-primario">Publicar algo</a>
            <a routerLink="/explorar" class="btn btn-secundario">Explorar</a>
          </div>
        }
        @default {
          <span class="grid size-20 place-items-center rounded-full bg-tierra-100 text-tierra-600"><app-icono nombre="alerta" [tamano]="36" /></span>
          <h1 class="mt-6 text-2xl font-bold">No pudimos confirmar tu correo</h1>
          <p class="mt-2 text-tenue">{{ error() }}</p>
          <a [routerLink]="sesion.autenticado() ? '/verifica-tu-correo' : '/ingresar'" class="btn btn-primario mt-8">
            {{ sesion.autenticado() ? 'Pedir un enlace nuevo' : 'Ingresar para pedir otro enlace' }}
          </a>
        }
      }
    </div>
  `,
})
export default class VerificarCorreo implements OnInit {
  private readonly api = inject(AuthApi);
  private readonly ruta = inject(ActivatedRoute);
  protected readonly sesion = inject(SesionService);
  protected readonly estado = signal<'verificando' | 'ok' | 'error'>('verificando');
  protected readonly error = signal('');

  async ngOnInit(): Promise<void> {
    const token = this.ruta.snapshot.queryParamMap.get('token');
    history.replaceState(history.state, '', '/verificar-correo');
    if (!token) {
      this.error.set('El enlace está incompleto.');
      this.estado.set('error');
      return;
    }
    try {
      await firstValueFrom(this.api.verificarCorreo(token));
      this.estado.set('ok');
      this.sesion.recargarUsuario();
    } catch (e) {
      this.error.set(mensajeDe(e));
      this.estado.set('error');
    }
  }
}
