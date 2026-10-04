import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { BotonGoogle } from '../auth/boton-google';
import { Avatar } from '../../shared/ui/avatar';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { descargar } from '../../shared/descargar';

@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, BotonGoogle, Avatar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Privacidad y datos</h1>
    <p class="mt-1 text-tenue">Tus derechos según la Ley 1581 de 2012 (Habeas Data), a un clic.</p>

    <section class="tarjeta mt-6 flex flex-wrap items-center justify-between gap-4 p-6">
      <div class="flex gap-4">
        <span class="grid size-12 shrink-0 place-items-center rounded-2xl bg-agua-100 text-agua-700"><app-icono nombre="descargar" [tamano]="24" /></span>
        <div>
          <h2 class="text-lg font-bold">Descargar mis datos</h2>
          <p class="mt-1 max-w-lg text-sm text-tenue">Un archivo JSON con tu perfil, publicaciones, solicitudes, mensajes, pagos, notificaciones y calificaciones.</p>
        </div>
      </div>
      <button type="button" class="btn btn-secundario" (click)="exportar()" [disabled]="exportando()">{{ exportando() ? 'Preparando…' : 'Descargar' }}</button>
    </section>

    <section class="tarjeta mt-6 p-6">
      <h2 class="text-lg font-bold">Personas bloqueadas</h2>
      <p class="mt-1 text-sm text-tenue">No pueden escribirte ni solicitar tus publicaciones, y tú tampoco a ellas. No se les avisa.</p>
      <ul class="mt-4 divide-y divide-borde">
        @for (b of bloqueados.value() ?? []; track b.perfil?.id) {
          <li class="flex items-center gap-3 py-3">
            <app-avatar [nombre]="b.perfil?.nombre" [foto]="b.perfil?.fotoUrl" [tamano]="36" />
            <a [routerLink]="['/usuarios', b.perfil?.id]" class="min-w-0 flex-1 truncate font-medium hover:underline">{{ b.perfil?.nombre }}</a>
            <button type="button" class="btn btn-secundario btn-sm" (click)="desbloquear(b.perfil?.id)">Desbloquear</button>
          </li>
        } @empty {
          <li class="py-3 text-sm text-tenue">{{ bloqueados.isLoading() ? 'Cargando…' : 'No has bloqueado a nadie.' }}</li>
        }
      </ul>
    </section>

    <section class="tarjeta mt-6 p-6">
      <h2 class="text-lg font-bold">Autorización de tratamiento de datos</h2>
      <p class="mt-1 text-sm text-tenue">
        Aceptaste la versión {{ config.config().versionPoliticaDatos ?? 'vigente' }} de la política.
        <a routerLink="/privacidad" class="enlace">Leer la política</a>
      </p>
    </section>

    <section class="mt-6 rounded-tarjeta border-2 border-tierra-500/40 bg-tierra-50/50 p-6 dark:bg-tierra-700/10">
      <div class="flex gap-4">
        <span class="grid size-12 shrink-0 place-items-center rounded-2xl bg-tierra-100 text-tierra-600"><app-icono nombre="basura" [tamano]="24" /></span>
        <div>
          <h2 class="text-lg font-bold text-tierra-700 dark:text-tierra-100">Eliminar mi cuenta</h2>
          <p class="mt-1 max-w-lg text-sm text-tenue">
            Se borrarán tus datos personales de forma permanente y perderás tus Eco-Puntos. Tus publicaciones dejarán de estar visibles. Esta acción no se puede deshacer.
          </p>
          <button type="button" class="btn btn-peligro mt-4" (click)="eliminarAbierto.set(true)">Eliminar mi cuenta</button>
        </div>
      </div>
    </section>

    <app-modal [(abierto)]="eliminarAbierto" titulo="Eliminar tu cuenta definitivamente" subtitulo="Te recomendamos descargar tus datos antes.">
      <form id="form-eliminar" (ngSubmit)="eliminar()" class="space-y-4">
        <div class="campo">
          <label for="confirmacion" class="etiqueta">Escribe <strong>ELIMINAR</strong> para confirmar</label>
          <input id="confirmacion" name="confirmacion" class="entrada font-mono uppercase" autocomplete="off" [(ngModel)]="confirmacion" />
        </div>
        @if (tieneClave()) {
          <div class="campo">
            <label for="clave-eliminar" class="etiqueta">Tu contraseña</label>
            <input id="clave-eliminar" name="clave" type="password" class="entrada" autocomplete="current-password" [(ngModel)]="clave" />
          </div>
        } @else {
          <p class="text-sm text-tenue">Tu cuenta usa Google: confirma tu identidad con el botón.</p>
          @if (googleToken()) {
            <p class="insignia-trueke"><app-icono nombre="check" [tamano]="12" />Identidad confirmada con Google</p>
          } @else {
            <app-boton-google texto="continue_with" (token)="googleToken.set($event)" />
          }
        }
        @if (error()) {
          <p class="error-campo" role="alert">{{ error() }}</p>
        }
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="eliminarAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-eliminar" class="btn btn-peligro" [disabled]="!puedeEliminar() || eliminando()">
          {{ eliminando() ? 'Eliminando…' : 'Eliminar para siempre' }}
        </button>
      </div>
    </app-modal>
  `,
})
export default class Privacidad {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionService);
  protected readonly config = inject(ConfigService);

  protected readonly exportando = signal(false);
  protected readonly eliminarAbierto = signal(false);
  protected readonly eliminando = signal(false);
  protected readonly confirmacion = signal('');
  protected readonly clave = signal('');
  protected readonly googleToken = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly tieneClave = computed(() => this.sesion.usuario()?.tieneClave !== false);
  protected readonly puedeEliminar = computed(
    () => this.confirmacion().trim().toUpperCase() === 'ELIMINAR' && (this.tieneClave() ? !!this.clave() : !!this.googleToken()),
  );

  protected readonly bloqueados = rxResource({ stream: () => this.api.bloqueados() });

  protected async desbloquear(id: string | undefined): Promise<void> {
    if (!id) return;
    try {
      await firstValueFrom(this.api.desbloquear(id));
      this.avisos.exito('Persona desbloqueada');
      this.bloqueados.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected async exportar(): Promise<void> {
    this.exportando.set(true);
    try {
      const datos = await firstValueFrom(this.api.misDatos());
      const fecha = new Date().toISOString().slice(0, 10);
      descargar(new Blob([JSON.stringify(datos, null, 2)], { type: 'application/json' }), `trueke-mis-datos-${fecha}.json`);
      this.avisos.exito('Tus datos se descargaron');
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.exportando.set(false);
    }
  }

  protected async eliminar(): Promise<void> {
    if (!this.puedeEliminar()) return;
    this.eliminando.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.api.eliminarCuenta({
          confirmacion: 'ELIMINAR',
          clave: this.tieneClave() ? this.clave() : null,
          googleIdToken: this.tieneClave() ? null : this.googleToken(),
        }),
      );
      this.sesion.limpiar();
      this.eliminarAbierto.set(false);
      this.avisos.info('Tu cuenta fue eliminada', 'Gracias por haber sido parte de la comunidad.');
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.error.set(mensajeDe(e));
    } finally {
      this.eliminando.set(false);
    }
  }
}
