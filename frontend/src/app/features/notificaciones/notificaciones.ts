import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { NotificacionDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { HacePipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Paginador } from '../../shared/ui/paginador';

const TAMANO = 20;

/** A dónde lleva cada tipo de notificación y con qué ícono se muestra. */
function destino(n: NotificacionDto): { icono: string; color: string; ruta: unknown[]; query?: Record<string, string> } {
  const t = n.tipo ?? '';
  if (t === 'SolicitudNueva') return { icono: 'apreton', color: 'bg-bosque-100 text-bosque-700', ruta: ['/intercambios'], query: { tab: 'recibidas' } };
  if (t.startsWith('Solicitud')) return { icono: 'apreton', color: 'bg-bosque-100 text-bosque-700', ruta: ['/intercambios'], query: { tab: 'enviadas' } };
  if (t.startsWith('Entrega') || t.startsWith('Intercambio') || t === 'CalificacionRecibida')
    return { icono: t === 'CalificacionRecibida' ? 'estrella' : 'checkCirculo', color: 'bg-sol-100 text-sol-600', ruta: ['/intercambios'] };
  if (t === 'AlertaPagoEnRevision') return { icono: 'alerta', color: 'bg-tierra-100 text-tierra-700', ruta: ['/admin/pagos'] };
  if (t === 'PqrNueva') return { icono: 'soporte', color: 'bg-agua-100 text-agua-700', ruta: ['/admin/pqr'] };
  if (t === 'ApelacionNueva') return { icono: 'balanza', color: 'bg-tierra-100 text-tierra-700', ruta: ['/admin/apelaciones'] };
  if (t === 'DenunciaRecibida' || t === 'ApelacionResuelta')
    return { icono: 'balanza', color: 'bg-tierra-100 text-tierra-700', ruta: ['/cuenta/reportes'], ...(n.recursoId ? { query: { r: n.recursoId } } : {}) };
  if (t === 'DenunciaRevisada') return { icono: 'bandera', color: 'bg-agua-100 text-agua-700', ruta: [] };
  if (t === 'PqrRespondida') return { icono: 'soporte', color: 'bg-agua-100 text-agua-700', ruta: ['/cuenta/soporte'] };
  if (t === 'FacturaEmitida') return { icono: 'factura', color: 'bg-cielo-100 text-cielo-700', ruta: ['/cuenta/facturacion'] };
  if (t === 'PlanPorVencer') return { icono: 'corona', color: 'bg-sol-100 text-sol-600', ruta: ['/eco-puntos'] };
  if (t.startsWith('Pago')) return { icono: 'billetera', color: 'bg-cielo-100 text-cielo-700', ruta: ['/eco-puntos'] };
  if (t.startsWith('Publicacion') && n.recursoId) return { icono: 'escudo', color: 'bg-agua-100 text-agua-700', ruta: ['/publicacion', n.recursoId] };
  if (t.startsWith('Verificacion')) return { icono: 'verificado', color: 'bg-agua-100 text-agua-700', ruta: ['/cuenta/perfil'] };
  if (t.startsWith('Mensaje')) return { icono: 'mensaje', color: 'bg-agua-100 text-agua-700', ruta: ['/mensajes'] };
  return { icono: 'campana', color: 'bg-superficie-2 text-tenue', ruta: [] };
}

@Component({
  imports: [Icono, EstadoVacio, Paginador, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-3xl py-8 sm:py-10">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="text-3xl font-extrabold">Notificaciones</h1>
          <p class="mt-1 text-tenue">Todo lo que pasa con tus publicaciones e intercambios.</p>
        </div>
        <div class="flex gap-2">
          <div class="pestanas">
            <button type="button" class="pestana" [class.pestana-activa]="!soloNoLeidas()" (click)="filtrar(false)">Todas</button>
            <button type="button" class="pestana" [class.pestana-activa]="soloNoLeidas()" (click)="filtrar(true)">Sin leer</button>
          </div>
          @if (tiempoReal.notificacionesNoLeidas() > 0) {
            <button type="button" class="btn btn-secundario btn-sm" (click)="leerTodas()"><app-icono nombre="check" [tamano]="14" />Marcar todo</button>
          }
        </div>
      </div>

      <ul class="mt-6 tarjeta divide-y divide-borde overflow-hidden">
        @if (recurso.isLoading() && !recurso.value()) {
          @for (i of [1, 2, 3, 4]; track i) {
            <li class="flex gap-3 p-4"><div class="esqueleto size-10 rounded-full"></div><div class="flex-1 space-y-2"><div class="esqueleto h-3 w-3/4"></div><div class="esqueleto h-3 w-1/4"></div></div></li>
          }
        }
        @for (n of recurso.value()?.items ?? []; track n.id) {
          @let d = info(n);
          <li>
            <button type="button" class="flex w-full items-start gap-3 p-4 text-left transition hover:bg-superficie-2" [class]="n.leida ? '' : 'bg-bosque-50 dark:bg-bosque-950/40'" (click)="abrir(n)">
              <span class="grid size-10 shrink-0 place-items-center rounded-full" [class]="d.color"><app-icono [nombre]="d.icono" [tamano]="18" /></span>
              <span class="min-w-0 flex-1">
                <span class="block text-sm break-words" [class.font-semibold]="!n.leida">{{ n.mensaje }}</span>
                <span class="mt-0.5 block text-xs text-tenue">{{ n.fechaUtc | hace }}</span>
              </span>
              @if (!n.leida) {
                <span class="mt-1.5 size-2.5 shrink-0 rounded-full bg-bosque-600" aria-label="Sin leer"></span>
              }
            </button>
          </li>
        }
      </ul>

      @if (!recurso.isLoading() && !recurso.value()?.items?.length) {
        <app-estado-vacio icono="campana" [titulo]="soloNoLeidas() ? 'Estás al día' : 'Aún no tienes notificaciones'"
          descripcion="Te avisaremos aquí cuando alguien solicite tus objetos, acepte tus solicitudes o ganes Eco-Puntos." />
      }

      <app-paginador [pagina]="pagina()" [total]="recurso.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />
    </div>
  `,
})
export default class Notificaciones {
  private readonly api = inject(CuentaApi);
  private readonly router = inject(Router);
  protected readonly tiempoReal = inject(TiempoRealService);
  protected readonly tamano = TAMANO;
  protected readonly pagina = signal(1);
  protected readonly soloNoLeidas = signal(false);
  protected readonly info = destino;

  protected readonly recurso = rxResource({
    params: () => ({ pagina: this.pagina(), solo: this.soloNoLeidas() }),
    stream: ({ params }) => this.api.notificaciones(params.solo, params.pagina, TAMANO),
  });

  constructor() {
    this.tiempoReal.notificacion$.pipe(takeUntilDestroyed()).subscribe(() => this.recurso.reload());
  }

  protected filtrar(solo: boolean): void {
    this.soloNoLeidas.set(solo);
    this.pagina.set(1);
  }

  protected async abrir(n: NotificacionDto): Promise<void> {
    if (!n.leida && n.id) {
      try {
        await firstValueFrom(this.api.leerNotificacion(n.id));
        this.tiempoReal.notificacionesNoLeidas.update((v) => Math.max(0, v - 1));
      } catch {
        // No impide navegar.
      }
    }
    const d = destino(n);
    if (d.ruta.length) await this.router.navigate(d.ruta, { queryParams: d.query });
    else this.recurso.reload();
  }

  protected async leerTodas(): Promise<void> {
    try {
      await firstValueFrom(this.api.leerTodas());
      this.tiempoReal.notificacionesNoLeidas.set(0);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }
}
