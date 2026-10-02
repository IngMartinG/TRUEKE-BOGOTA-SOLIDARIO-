import { ChangeDetectionStrategy, Component, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MOTIVOS_DENUNCIA, type MotivoDenunciaDto, type TipoDenunciaDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { Modal } from './modal';

@Component({
  selector: 'app-denunciar',
  imports: [FormsModule, Modal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal [(abierto)]="abierto" titulo="Reportar contenido" subtitulo="Tu reporte es anónimo y lo revisa el equipo de moderación.">
      <form id="form-denuncia" (ngSubmit)="enviar()" class="space-y-4">
        <fieldset class="space-y-2">
          <legend class="etiqueta mb-2">¿Qué está pasando?</legend>
          @for (m of motivos; track m.valor) {
            <label class="flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition"
              [class]="motivo() === m.valor ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/40' : 'border-borde hover:bg-superficie-2'">
              <input type="radio" name="motivo" class="accent-bosque-600" [value]="m.valor" [ngModel]="motivo()" (ngModelChange)="motivo.set($event)" />
              <span class="text-sm font-medium">{{ m.etiqueta }}</span>
            </label>
          }
        </fieldset>
        <div class="campo">
          <label class="etiqueta" for="detalle-denuncia">Detalles (opcional)</label>
          <textarea id="detalle-denuncia" name="detalle" class="entrada" maxlength="500" [(ngModel)]="detalle" placeholder="Cuéntanos qué viste para ayudarnos a revisarlo más rápido."></textarea>
        </div>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-denuncia" class="btn btn-peligro" [disabled]="!motivo() || enviando()">
          {{ enviando() ? 'Enviando…' : 'Enviar reporte' }}
        </button>
      </div>
    </app-modal>
  `,
})
export class Denunciar {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  readonly abierto = model(false);
  readonly tipo = input.required<TipoDenunciaDto>();
  readonly objetivoId = input.required<string>();

  protected readonly motivos = MOTIVOS_DENUNCIA;
  protected readonly motivo = signal<MotivoDenunciaDto | null>(null);
  protected readonly detalle = signal('');
  protected readonly enviando = signal(false);

  protected async enviar(): Promise<void> {
    const motivo = this.motivo();
    if (!motivo) return;
    this.enviando.set(true);
    try {
      await firstValueFrom(
        this.api.denunciar({ tipo: this.tipo(), objetivoId: this.objetivoId(), motivo, detalle: this.detalle().trim() || null }),
      );
      this.avisos.exito('Gracias por reportar', 'El equipo de moderación lo revisará pronto.');
      this.abierto.set(false);
      this.motivo.set(null);
      this.detalle.set('');
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
