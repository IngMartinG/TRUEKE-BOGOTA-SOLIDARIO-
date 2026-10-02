import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { PagoEstadoDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CopPipe, FechaPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';

const INTERVALO_MS = 3000;
const MAX_INTENTOS = 40; // ~2 minutos

/** Resultado de un pago: consulta el estado hasta que se resuelva (Wompi confirma por webhook). */
@Component({
  imports: [RouterLink, Icono, CopPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor flex min-h-[60vh] max-w-lg flex-col items-center justify-center py-16 text-center">
      @if (!pago() && !error()) {
        <span class="size-14 animate-spin rounded-full border-4 border-sol-100 border-t-sol-500" aria-hidden="true"></span>
        <h1 class="mt-6 text-2xl font-bold">Consultando tu pago…</h1>
      } @else if (error()) {
        <span class="grid size-20 place-items-center rounded-full bg-tierra-100 text-tierra-600"><app-icono nombre="alerta" [tamano]="36" /></span>
        <h1 class="mt-6 text-2xl font-bold">No encontramos este pago</h1>
        <p class="mt-2 text-tenue">{{ error() }}</p>
        <a routerLink="/eco-puntos" class="btn btn-primario mt-8">Volver a Eco-Puntos</a>
      } @else if (pago(); as p) {
        <span class="grid size-20 place-items-center rounded-full shadow-elevada" [class]="vista().fondo">
          @if (p.estado === 'Pendiente') {
            <span class="size-10 animate-spin rounded-full border-4 border-white/40 border-t-white"></span>
          } @else {
            <app-icono [nombre]="vista().icono" [tamano]="40" />
          }
        </span>
        <h1 class="mt-6 text-2xl font-bold">{{ vista().titulo }}</h1>
        <p class="mt-2 text-tenue">{{ vista().texto }}</p>

        <dl class="mt-8 w-full space-y-2 rounded-tarjeta border border-borde bg-superficie p-5 text-left text-sm">
          <div class="flex justify-between"><dt class="text-tenue">Concepto</dt><dd class="font-semibold">{{ p.concepto }}</dd></div>
          <div class="flex justify-between"><dt class="text-tenue">Valor</dt><dd class="font-semibold">{{ p.montoCop | cop }}</dd></div>
          <div class="flex justify-between"><dt class="text-tenue">Referencia</dt><dd class="font-mono text-xs">{{ p.referencia }}</dd></div>
          <div class="flex justify-between"><dt class="text-tenue">Fecha</dt><dd>{{ p.fechaUtc | fecha: true }}</dd></div>
        </dl>

        @if (p.estado === 'Pendiente' && esSimulado()) {
          <div class="mt-6 w-full rounded-2xl border border-dashed border-agua-500 bg-agua-50 p-4 text-sm dark:bg-agua-700/20">
            <p class="font-semibold text-agua-700 dark:text-agua-100">Entorno de pruebas: pasarela simulada</p>
            <div class="mt-3 flex justify-center gap-2">
              <button type="button" class="btn btn-primario btn-sm" (click)="simular(true)" [disabled]="simulando()">Aprobar pago</button>
              <button type="button" class="btn btn-secundario btn-sm" (click)="simular(false)" [disabled]="simulando()">Rechazar</button>
            </div>
          </div>
        }

        <div class="mt-8 flex flex-wrap justify-center gap-3">
          <a routerLink="/eco-puntos" class="btn btn-secundario">Eco-Puntos</a>
          <a routerLink="/mis-publicaciones" class="btn btn-primario">Mis publicaciones</a>
        </div>
      }
    </div>
  `,
})
export default class EstadoPago {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly sesion = inject(SesionService);

  readonly referencia = input.required<string>();
  /** `?simulado=1` cuando el pago se inició con la pasarela simulada de desarrollo. */
  readonly simulado = input<string>();

  protected readonly pago = signal<PagoEstadoDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly simulando = signal(false);
  protected readonly esSimulado = computed(() => this.simulado() === '1');
  private temporizador: ReturnType<typeof setTimeout> | undefined;
  private intentos = 0;

  protected readonly vista = computed(() => {
    switch (this.pago()?.estado) {
      case 'Aprobado':
        return { titulo: '¡Pago aprobado!', texto: 'Tu beneficio ya está activo. Gracias por apoyar la economía circular.', icono: 'check', fondo: 'bg-bosque-600 text-white' };
      case 'Rechazado':
        return { titulo: 'El pago fue rechazado', texto: 'No se realizó ningún cobro. Puedes intentarlo de nuevo con otro medio de pago.', icono: 'x', fondo: 'bg-tierra-600 text-white' };
      case 'Expirado':
        return { titulo: 'El pago expiró', texto: 'El tiempo para completar el pago terminó. Inicia uno nuevo cuando quieras.', icono: 'reloj', fondo: 'bg-superficie-2 text-tenue' };
      case 'RequiereRevision':
        return { titulo: 'Pago en revisión', texto: 'Recibimos tu pago pero hubo un problema al aplicar el beneficio. Nuestro equipo lo revisará y te avisaremos.', icono: 'alerta', fondo: 'bg-sol-400 text-bosque-950' };
      case 'Reembolsado':
        return { titulo: 'Pago reembolsado', texto: 'El valor fue devuelto a tu medio de pago.', icono: 'refrescar', fondo: 'bg-cielo-600 text-white' };
      default:
        return { titulo: 'Esperando confirmación', texto: 'Estamos esperando la respuesta de la pasarela. Esta página se actualiza sola.', icono: 'reloj', fondo: 'bg-sol-400 text-bosque-950' };
    }
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.temporizador));
    effect(() => {
      const ref = this.referencia();
      untracked(() => {
        this.intentos = 0;
        void this.consultar(ref);
      });
    });
  }

  private async consultar(ref: string): Promise<void> {
    clearTimeout(this.temporizador);
    try {
      const p = await firstValueFrom(this.api.estadoPago(ref));
      const anterior = this.pago()?.estado;
      this.pago.set(p);
      if (p.estado === 'Pendiente' && ++this.intentos < MAX_INTENTOS) {
        this.temporizador = setTimeout(() => void this.consultar(ref), INTERVALO_MS);
      } else if (anterior === 'Pendiente' && p.estado === 'Aprobado') {
        this.avisos.puntos('¡Pago aprobado!');
        this.sesion.recargarUsuario();
      } else if (p.estado === 'Aprobado') {
        this.sesion.recargarUsuario();
      }
    } catch (e) {
      this.error.set(mensajeDe(e));
    }
  }

  protected async simular(aprobado: boolean): Promise<void> {
    this.simulando.set(true);
    try {
      await firstValueFrom(this.api.simularPago(this.referencia(), aprobado));
      this.intentos = 0;
      await this.consultar(this.referencia());
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.simulando.set(false);
    }
  }
}
