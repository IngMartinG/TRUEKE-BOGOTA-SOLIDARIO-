import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { RolDto, UsuarioAdminDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { SesionService } from '../../core/sesion.service';
import { FechaPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { Paginador } from '../../shared/ui/paginador';

const TAMANO = 20;

@Component({
  imports: [FormsModule, RouterLink, Avatar, Icono, Modal, Paginador, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-bold">Usuarios</h2>
      <form class="flex flex-wrap items-center gap-2" (ngSubmit)="buscar()">
        <input type="search" name="texto" class="entrada w-64" placeholder="Nombre o correo" [(ngModel)]="textoEntrada" aria-label="Buscar usuarios" />
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" class="casilla" name="susp" [(ngModel)]="soloSuspendidos" (ngModelChange)="pagina.set(1)" />Solo suspendidos</label>
        <button type="submit" class="btn btn-primario btn-sm"><app-icono nombre="buscar" [tamano]="14" />Buscar</button>
      </form>
    </div>

    <div class="tarjeta mt-6 overflow-x-auto">
      <table class="w-full min-w-[48rem] text-left text-sm">
        <thead class="border-b border-borde text-xs text-tenue uppercase">
          <tr><th class="px-4 py-3">Usuario</th><th class="px-4 py-3">Rol</th><th class="px-4 py-3">Estado</th><th class="px-4 py-3">Reputación</th><th class="px-4 py-3 text-right">Acciones</th></tr>
        </thead>
        <tbody class="divide-y divide-borde">
          @for (u of recurso.value()?.items ?? []; track u.id) {
            <tr class="align-top">
              <td class="px-4 py-3">
                <a [routerLink]="['/usuarios', u.id]" class="flex items-center gap-3">
                  <app-avatar [nombre]="u.nombreCompleto" [tamano]="36" />
                  <span class="min-w-0">
                    <span class="block font-semibold">{{ u.nombreCompleto }}</span>
                    <span class="block text-xs text-tenue">{{ u.correo }} · {{ u.localidad }}</span>
                    <span class="block text-xs text-tenue">Desde {{ u.fechaRegistro | fecha }}</span>
                  </span>
                </a>
              </td>
              <td class="px-4 py-3">
                @if (sesion.esSuperUsuario() && u.id !== sesion.usuario()?.id) {
                  <select class="entrada py-1.5 text-xs" [ngModel]="u.rol" (ngModelChange)="cambiarRol(u, $event)" [attr.aria-label]="'Rol de ' + u.nombreCompleto">
                    @for (r of roles; track r) {
                      <option [value]="r">{{ r }}</option>
                    }
                  </select>
                } @else {
                  <span class="insignia-neutra">{{ u.rol }}</span>
                }
                @if (u.dosFactoresActivo) {
                  <span class="mt-1 block text-xs text-bosque-700 dark:text-bosque-300">2FA activa</span>
                }
              </td>
              <td class="px-4 py-3">
                @if (u.suspendido) {
                  <span class="insignia bg-tierra-100 text-tierra-700">Suspendido</span>
                  <p class="mt-1 text-xs text-tenue">{{ u.suspendidoHasta ? 'Hasta ' + (u.suspendidoHasta | fecha) : 'Indefinidamente' }}</p>
                  @if (u.motivoSuspension) {
                    <p class="text-xs text-tenue">{{ u.motivoSuspension }}</p>
                  }
                } @else {
                  <span class="insignia-trueke">Activo</span>
                }
                <p class="mt-1 text-xs text-tenue">{{ u.correoVerificado ? 'Correo verificado' : 'Correo sin verificar' }} · {{ u.estadoVerificacion }}</p>
              </td>
              <td class="px-4 py-3">
                <p class="font-semibold">{{ (u.reputacion ?? 0).toFixed(1) }}</p>
                @if (u.calificacionPromedio) {
                  <p class="text-xs text-tenue">★ {{ u.calificacionPromedio.toFixed(1) }} ({{ u.totalCalificaciones }})</p>
                }
              </td>
              <td class="px-4 py-3 text-right">
                @if (u.id !== sesion.usuario()?.id) {
                  @if (u.suspendido) {
                    <button type="button" class="btn btn-secundario btn-sm" (click)="reactivar(u)">Reactivar</button>
                  } @else {
                    <button type="button" class="btn btn-peligro btn-sm" (click)="abrirSuspender(u)"><app-icono nombre="ban" [tamano]="14" />Suspender</button>
                  }
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="5" class="px-4 py-10 text-center text-tenue">{{ recurso.isLoading() ? 'Cargando…' : 'No hay usuarios que coincidan.' }}</td></tr>
          }
        </tbody>
      </table>
    </div>
    <app-paginador [pagina]="pagina()" [total]="recurso.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />

    <app-modal [(abierto)]="suspenderAbierto" titulo="Suspender cuenta" [subtitulo]="seleccionado()?.nombreCompleto ?? ''">
      <form id="form-suspender" (ngSubmit)="suspender()" class="space-y-4">
        <div class="campo">
          <label for="motivo-susp" class="etiqueta">Motivo (lo verá la persona)</label>
          <textarea id="motivo-susp" name="motivo" class="entrada" maxlength="300" required [(ngModel)]="motivo"></textarea>
        </div>
        <div class="campo">
          <label for="dias" class="etiqueta">Duración</label>
          <select id="dias" name="dias" class="entrada" [(ngModel)]="dias">
            <option [ngValue]="1">1 día</option>
            <option [ngValue]="7">7 días</option>
            <option [ngValue]="30">30 días</option>
            <option [ngValue]="null">Indefinida</option>
          </select>
        </div>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="suspenderAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-suspender" class="btn btn-peligro" [disabled]="motivo().trim().length < 3">Suspender</button>
      </div>
    </app-modal>
  `,
})
export default class Usuarios {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);
  protected readonly tamano = TAMANO;
  protected readonly roles: RolDto[] = ['Cliente', 'Administrador', 'SuperUsuario'];
  protected readonly textoEntrada = signal('');
  private readonly texto = signal('');
  protected readonly soloSuspendidos = signal(false);
  protected readonly pagina = signal(1);
  protected readonly recurso = rxResource({
    params: () => ({ texto: this.texto(), solo: this.soloSuspendidos(), pagina: this.pagina() }),
    stream: ({ params }) => this.api.usuarios(params.texto, params.solo, params.pagina, TAMANO),
  });

  protected readonly suspenderAbierto = signal(false);
  protected readonly seleccionado = signal<UsuarioAdminDto | null>(null);
  protected readonly motivo = signal('');
  protected readonly dias = signal<number | null>(7);

  protected buscar(): void {
    this.texto.set(this.textoEntrada().trim());
    this.pagina.set(1);
  }

  protected abrirSuspender(u: UsuarioAdminDto): void {
    this.seleccionado.set(u);
    this.motivo.set('');
    this.dias.set(7);
    this.suspenderAbierto.set(true);
  }

  protected async suspender(): Promise<void> {
    const u = this.seleccionado();
    if (!u?.id) return;
    await this.correr(() => firstValueFrom(this.api.suspender(u.id!, this.motivo().trim(), this.dias())), 'Cuenta suspendida');
    this.suspenderAbierto.set(false);
  }

  protected reactivar(u: UsuarioAdminDto): Promise<void> {
    return this.correr(() => firstValueFrom(this.api.reactivar(u.id!)), 'Cuenta reactivada');
  }

  protected cambiarRol(u: UsuarioAdminDto, rol: RolDto): Promise<void> {
    return this.correr(() => firstValueFrom(this.api.cambiarRol(u.id!, rol)), `Rol cambiado a ${rol}`);
  }

  private async correr(accion: () => Promise<unknown>, exito: string): Promise<void> {
    try {
      await accion();
      this.avisos.exito(exito);
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }
}
