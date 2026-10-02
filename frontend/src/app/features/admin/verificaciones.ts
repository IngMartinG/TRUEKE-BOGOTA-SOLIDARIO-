import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import type { VerificacionPendienteDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { HacePipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

@Component({
  imports: [FormsModule, Avatar, Icono, Modal, EstadoVacio, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-xl font-bold">Verificaciones de identidad</h2>
    <p class="mt-1 text-sm text-tenue">El documento se borra automáticamente al aprobar o rechazar.</p>

    <ul class="mt-6 grid gap-3 md:grid-cols-2">
      @for (v of recurso.value() ?? []; track v.usuarioId) {
        <li class="tarjeta p-5">
          <div class="flex items-center gap-3">
            <app-avatar [nombre]="v.nombreCompleto" [tamano]="44" />
            <div class="min-w-0 flex-1">
              <p class="truncate font-semibold">{{ v.nombreCompleto }}</p>
              <p class="truncate text-xs text-tenue">{{ v.correo }} · registrada {{ v.fechaRegistro | hace }}</p>
            </div>
          </div>
          <a [href]="v.documentoUrl" target="_blank" rel="noopener noreferrer" referrerpolicy="no-referrer" class="btn btn-secundario btn-sm mt-4 w-full">
            <app-icono nombre="externo" [tamano]="14" />Ver documento
          </a>
          <div class="mt-3 grid grid-cols-2 gap-2">
            <button type="button" class="btn btn-fantasma btn-sm text-tierra-600" (click)="abrirRechazo(v)">Rechazar</button>
            <button type="button" class="btn btn-primario btn-sm" (click)="aprobar(v)"><app-icono nombre="check" [tamano]="14" />Aprobar</button>
          </div>
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="verificado" titulo="No hay verificaciones pendientes" />
    }

    <app-modal [(abierto)]="rechazoAbierto" titulo="Rechazar verificación" subtitulo="Explica qué debe corregir (lo verá la persona).">
      <form id="form-rechazo" (ngSubmit)="rechazar()" class="campo">
        <label for="motivo-rechazo" class="etiqueta">Motivo</label>
        <textarea id="motivo-rechazo" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivo" placeholder="Ej: la foto del documento no es legible"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="rechazoAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-rechazo" class="btn btn-peligro" [disabled]="motivo().trim().length < 3">Rechazar</button>
      </div>
    </app-modal>
  `,
})
export default class Verificaciones {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly recurso = rxResource({ stream: () => this.api.verificaciones() });
  protected readonly rechazoAbierto = signal(false);
  protected readonly motivo = signal('');
  private seleccionada: VerificacionPendienteDto | null = null;

  protected async aprobar(v: VerificacionPendienteDto): Promise<void> {
    try {
      await firstValueFrom(this.api.aprobarVerificacion(v.usuarioId!));
      this.avisos.exito('Cuenta verificada');
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }

  protected abrirRechazo(v: VerificacionPendienteDto): void {
    this.seleccionada = v;
    this.motivo.set('');
    this.rechazoAbierto.set(true);
  }

  protected async rechazar(): Promise<void> {
    const v = this.seleccionada;
    if (!v?.usuarioId) return;
    try {
      await firstValueFrom(this.api.rechazarVerificacion(v.usuarioId, this.motivo().trim()));
      this.rechazoAbierto.set(false);
      this.avisos.exito('Verificación rechazada');
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }
}
