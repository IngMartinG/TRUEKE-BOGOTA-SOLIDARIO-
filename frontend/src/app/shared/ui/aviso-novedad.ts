import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { firstValueFrom, map } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { Icono } from './icono';

/** Tipo de la notificación que la API dejó en la bandeja de quien ya tenía ventas antes de "También acepto trueke". */
export const TIPO_NOVEDAD_VENTA_TRUEKE = 'NovedadVentaTrueke';

/**
 * Franja al abrir la cuenta con una novedad pendiente. Vive en la bandeja de notificaciones: al cerrarla se marca leída
 * y ya no sale en ningún dispositivo.
 */
@Component({
  selector: 'app-aviso-novedad',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (novedad(); as n) {
      <div class="border-b border-bosque-200 bg-bosque-50 dark:border-bosque-800 dark:bg-bosque-950/40" role="status">
        <div class="contenedor flex flex-wrap items-center justify-center gap-x-3 gap-y-2 py-2.5 text-center text-sm">
          <app-icono nombre="repeat" [tamano]="16" class="text-bosque-700 dark:text-bosque-300" />
          <span><strong>Novedad:</strong> tus publicaciones de compra y venta ahora también pueden recibir trueke. Edítalas y marca
            «También acepto trueke» si quieres que te ofrezcan objetos.</span>
          <span class="flex gap-2">
            <button type="button" class="btn btn-primario btn-sm" (click)="cerrar(n, true)">Ver mis ventas</button>
            <button type="button" class="btn btn-secundario btn-sm" (click)="cerrar(n, false)">Entendido</button>
          </span>
        </div>
      </div>
    }
  `,
})
export class AvisoNovedad {
  private readonly api = inject(CuentaApi);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionService);
  private readonly tiempoReal = inject(TiempoRealService);
  private readonly cerradaId = signal<string | undefined>(undefined);

  // Se consulta al iniciar sesión (y al cambiar de cuenta); sin sesión el recurso queda inactivo. Sin avisos de error:
  // si falla, la franja simplemente no sale.
  private readonly recurso = rxResource({
    params: () => (this.sesion.autenticado() ? (this.sesion.usuario()?.id ?? '') : undefined),
    stream: () =>
      this.api
        .notificaciones(true, 1, 50, true)
        .pipe(map((p) => (p.items ?? []).find((n) => n.tipo === TIPO_NOVEDAD_VENTA_TRUEKE) ?? null)),
  });

  protected readonly novedad = computed(() => {
    if (!this.sesion.autenticado() || !this.recurso.hasValue()) return null;
    const n = this.recurso.value();
    return n && n.id !== this.cerradaId() ? n : null;
  });

  protected async cerrar(n: { id?: string }, verVentas: boolean): Promise<void> {
    this.cerradaId.set(n.id);
    if (n.id) {
      try {
        await firstValueFrom(this.api.leerNotificacion(n.id));
        this.tiempoReal.notificacionesNoLeidas.update((v) => Math.max(0, v - 1));
      } catch {
        // Si falla, vuelve a salir la próxima vez; no impide seguir.
      }
    }
    if (verVentas) await this.router.navigate(['/mis-publicaciones']);
  }
}
