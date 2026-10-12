import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AvisosService } from '../../core/avisos.service';
import { Icono } from './icono';

@Component({
  selector: 'app-avisos',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Arriba, bajo el encabezado: en el celular no tapa la barra inferior ni la caja del chat -->
    <div
      class="pointer-events-none fixed inset-x-0 top-20 z-[100] flex flex-col items-center gap-2 px-4 sm:right-4 sm:left-auto sm:items-end"
      aria-live="polite"
      role="status"
    >
      @for (a of avisos.avisos(); track a.id) {
        <div
          class="pointer-events-auto flex w-full max-w-sm animate-aparecer items-start gap-3 rounded-2xl border border-borde bg-superficie p-4 shadow-elevada"
          [attr.role]="a.tipo === 'error' ? 'alert' : null"
          (mouseenter)="avisos.pausar(a.id)" (mouseleave)="avisos.reanudar(a.id)"
          (focusin)="avisos.pausar(a.id)" (focusout)="avisos.reanudar(a.id)"
        >
          <span
            class="grid size-9 shrink-0 place-items-center rounded-full"
            [class]="{
              'bg-bosque-100 text-bosque-700': a.tipo === 'exito',
              'bg-tierra-100 text-tierra-700': a.tipo === 'error',
              'bg-agua-100 text-agua-700': a.tipo === 'info',
              'bg-sol-100 text-sol-600': a.tipo === 'puntos',
            }"
          >
            <app-icono
              [nombre]="a.tipo === 'exito' ? 'checkCirculo' : a.tipo === 'error' ? 'alerta' : a.tipo === 'puntos' ? 'destello' : 'campana'"
              [tamano]="18"
            />
          </span>
          <div class="min-w-0 flex-1 pt-0.5">
            <p class="text-sm font-semibold">{{ a.titulo }}</p>
            @if (a.detalle) {
              <p class="mt-0.5 text-sm break-words text-tenue">{{ a.detalle }}</p>
            }
          </div>
          <button type="button" class="btn-icono -m-2" (click)="avisos.cerrar(a.id)" aria-label="Cerrar aviso">
            <app-icono nombre="x" [tamano]="16" />
          </button>
        </div>
      }
    </div>
  `,
})
export class Avisos {
  protected readonly avisos = inject(AvisosService);
}
