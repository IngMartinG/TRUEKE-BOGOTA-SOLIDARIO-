import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { filter, firstValueFrom } from 'rxjs';
import type { NotificacionDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { NumeroPipe } from '../pipes';
import { Icono } from './icono';
import { Modal } from './modal';

/**
 * El momento cumbre del producto: cuando ambas partes confirman la entrega llega "IntercambioCompletado" en tiempo
 * real y aquí se celebra con los Eco-Puntos ganados (saldo antes y después), el saldo nuevo y lo que sigue.
 */
@Component({
  selector: 'app-logro-intercambio',
  imports: [Modal, Icono, RouterLink, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .sello {
      animation: sello 520ms cubic-bezier(0.23, 1, 0.32, 1) both;
    }
    @keyframes sello {
      from {
        opacity: 0;
        transform: scale(0.9) rotate(-8deg);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .sello {
        animation: none;
      }
    }
  `,
  template: `
    <app-modal [(abierto)]="abierto" titulo="¡Intercambio completado!" [subtitulo]="objeto() ? objeto() + ' ya tiene un nuevo hogar.' : 'Un objeto más encontró un nuevo hogar.'">
      <div class="flex flex-col items-center gap-4 text-center">
        <span class="sello grid size-20 place-items-center rounded-[1.75rem] bg-gradient-to-br from-bosque-100 to-sol-100 text-bosque-700 shadow-suave dark:from-bosque-900 dark:to-sol-500/20 dark:text-bosque-200">
          <app-icono nombre="hoja" [tamano]="40" [grosor]="1.6" />
        </span>
        @if (ganados() > 0) {
          <div>
            <p class="font-display text-5xl font-extrabold text-sol-600 tabular-nums dark:text-sol-300" aria-hidden="true">+{{ mostrados() | numero }}</p>
            <p class="sr-only">Ganaste {{ ganados() }} Eco-Puntos.</p>
            <p class="mt-1 text-sm font-semibold">Eco-Puntos para ti</p>
          </div>
          <p class="text-sm text-tenue">Tu saldo ahora es de <strong class="text-tinta">{{ saldo() | numero }} Eco-Puntos</strong>. Úsalos para destacar o impulsar tus publicaciones.</p>
        } @else {
          <p class="text-sm text-tenue">{{ mensaje() }}</p>
        }
        <a routerLink="/impacto" class="enlace mt-3 inline-block text-sm" (click)="abierto.set(false)">Mira lo que hemos logrado juntos</a>
      </div>
      <div pie class="flex flex-col-reverse gap-2 border-t border-borde p-4 sm:flex-row sm:justify-end">
        <a routerLink="/publicar" class="btn btn-secundario" (click)="abierto.set(false)"><app-icono nombre="mas" [tamano]="16" />Publicar otro objeto</a>
        <a routerLink="/intercambios" [queryParams]="{ calificar: solicitudId() }" class="btn btn-sol" (click)="abierto.set(false)">
          <app-icono nombre="estrella" [tamano]="16" />Calificar a la otra persona
        </a>
      </div>
    </app-modal>
  `,
})
export class LogroIntercambio {
  private readonly sesion = inject(SesionService);
  private readonly cuenta = inject(CuentaApi);
  private readonly destroy = inject(DestroyRef);

  protected readonly abierto = signal(false);
  protected readonly objeto = signal('');
  protected readonly mensaje = signal('');
  protected readonly solicitudId = signal<string | null>(null);
  protected readonly ganados = signal(0);
  protected readonly mostrados = signal(0);
  protected readonly saldo = signal(0);

  constructor() {
    inject(TiempoRealService)
      .notificacion$.pipe(
        filter((n) => n.tipo === 'IntercambioCompletado'),
        takeUntilDestroyed(),
      )
      .subscribe((n) => void this.celebrar(n));
  }

  private async celebrar(n: NotificacionDto): Promise<void> {
    // El saldo se toma ya: la notificación llega antes de que la pantalla vuelva a pedir el usuario
    const antes = this.sesion.usuario()?.saldoEcoPuntos ?? 0;
    this.objeto.set(n.mensaje?.match(/"([^"]+)"/)?.[1] ?? '');
    this.mensaje.set(n.mensaje ?? '');
    this.solicitudId.set(n.recursoId ?? null);
    let despues = antes;
    try {
      const u = await firstValueFrom(this.cuenta.yo());
      this.sesion.actualizarUsuario(u);
      despues = u.saldoEcoPuntos ?? antes;
    } catch {
      // Sin el saldo nuevo se celebra igual, con el mensaje del servidor.
    }
    this.ganados.set(Math.max(0, despues - antes));
    this.saldo.set(despues);
    this.abierto.set(true);
    this.contar(this.ganados());
  }

  /** Los puntos suben de 0 al total en ~0,7 s (instantáneo con "reducir movimiento"). */
  private contar(total: number): void {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches || total <= 0) {
      this.mostrados.set(total);
      return;
    }
    const inicio = performance.now();
    let id = 0;
    const paso = (t: number) => {
      const p = Math.min(1, (t - inicio) / 700);
      this.mostrados.set(Math.round(total * (1 - Math.pow(1 - p, 3))));
      if (p < 1) id = requestAnimationFrame(paso);
    };
    id = requestAnimationFrame(paso);
    this.destroy.onDestroy(() => cancelAnimationFrame(id));
  }
}
