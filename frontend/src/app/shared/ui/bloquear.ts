import { ChangeDetectionStrategy, Component, inject, input, model, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { Icono } from './icono';
import { Modal } from './modal';

/**
 * Botón "Bloquear / Desbloquear" con confirmación. Bloquear impide que ambos se escriban o se soliciten publicaciones;
 * a la otra persona no se le avisa.
 */
@Component({
  selector: 'app-bloquear',
  imports: [Icono, Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (bloqueado()) {
      <button type="button" [class]="clase()" (click)="desbloquear()" [disabled]="trabajando()">
        <app-icono nombre="ban" [tamano]="16" />Desbloquear
      </button>
    } @else {
      <button type="button" [class]="clase()" (click)="abierto.set(true)">
        <app-icono nombre="ban" [tamano]="16" />Bloquear
      </button>
    }

    <app-modal [(abierto)]="abierto" [titulo]="'¿Bloquear a ' + nombre() + '?'" subtitulo="No le avisaremos.">
      <ul class="list-disc space-y-1.5 pl-5 text-sm text-tenue">
        <li>Ninguno de los dos podrá escribirle al otro ni solicitar sus publicaciones.</li>
        <li>No verán si el otro está en línea o escribiendo.</li>
        <li>Si hubo algo grave (acoso, estafa), además repórtalo: así lo revisa el equipo de moderación.</li>
        <li>Puedes desbloquear cuando quieras desde Cuenta → Privacidad y datos.</li>
      </ul>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="button" class="btn btn-peligro" (click)="bloquear()" [disabled]="trabajando()">Bloquear</button>
      </div>
    </app-modal>
  `,
})
export class Bloquear {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  readonly usuarioId = input.required<string>();
  readonly nombre = input('esta persona');
  readonly bloqueado = model(false);
  readonly clase = input('btn btn-fantasma btn-sm');
  protected readonly abierto = signal(false);
  protected readonly trabajando = signal(false);

  protected async bloquear(): Promise<void> {
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.bloquear(this.usuarioId()));
      this.bloqueado.set(true);
      this.abierto.set(false);
      this.avisos.exito(`Bloqueaste a ${this.nombre()}`);
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async desbloquear(): Promise<void> {
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.desbloquear(this.usuarioId()));
      this.bloqueado.set(false);
      this.avisos.exito(`Desbloqueaste a ${this.nombre()}`);
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }
}
