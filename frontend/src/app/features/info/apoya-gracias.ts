import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ConfigService } from '../../core/config.service';
import { NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { CompartirProyecto } from './compartir-proyecto';
import { consultarAporte, esRegresoWompi } from './wompi-transaccion';

type Vista = 'consultando' | 'aprobado' | 'pendiente' | 'fallido' | 'sin-confirmar';

/** Consultas mientras Wompi deja el pago en PENDING (PSE o Nequi pueden tardar unos segundos). */
const INTENTOS_PENDIENTE = 6;
const ESPERA_MS = 4000;

/**
 * Agradecimiento al volver del enlace de pago de Wompi. Solo celebra si Wompi confirma el pago; si no se puede
 * consultar, agradece sin afirmar que se aprobó.
 */
@Component({
  imports: [RouterLink, Icono, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section class="relative isolate min-h-[calc(100dvh-4rem)] overflow-hidden bg-bosque-950 text-white">
      <div class="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_50%_35%,#7a2e1d_0%,transparent_50%),radial-gradient(ellipse_at_bottom,#1f7a4d_0%,transparent_60%)] opacity-60"></div>

      @if (vista() === 'aprobado') {
        <!-- Corazones que suben una sola vez -->
        <div class="pointer-events-none absolute inset-0 -z-10 overflow-hidden" aria-hidden="true">
          @for (c of corazones; track $index) {
            <svg viewBox="0 0 24 24" class="absolute bottom-0 animate-subir"
              [style.left.%]="c.x" [style.width.px]="c.tam" [style.animationDelay]="c.retraso + 's'" [style.--giro]="c.giro + 'deg'" [style.color]="c.color">
              <path fill="currentColor" d="M12 21s-7.5-4.6-9.6-9.3C1 8.4 3.1 4.5 6.8 4.5c2.1 0 3.6 1.1 4.2 2.3h2c.6-1.2 2.1-2.3 4.2-2.3 3.7 0 5.8 3.9 4.4 7.2C19.5 16.4 12 21 12 21Z" />
            </svg>
          }
        </div>
      }

      <div class="contenedor flex max-w-2xl flex-col items-center py-16 text-center sm:py-24" aria-live="polite">
        @switch (vista()) {
          @case ('consultando') {
            <div class="size-24 animate-pulse rounded-full bg-white/10" aria-hidden="true"></div>
            <p class="mt-6 text-lg text-bosque-100/85">Confirmando tu aporte con Wompi…</p>
          }
          @case ('aprobado') {
            <div class="w-36 animate-latido sm:w-44" aria-hidden="true">
              <svg viewBox="0 0 24 24" class="w-full drop-shadow-[0_18px_40px_rgb(226_92_60/0.5)]">
                <path fill="#e25c3c" d="M12 21s-7.5-4.6-9.6-9.3C1 8.4 3.1 4.5 6.8 4.5c2.1 0 3.6 1.1 4.2 2.3h2c.6-1.2 2.1-2.3 4.2-2.3 3.7 0 5.8 3.9 4.4 7.2C19.5 16.4 12 21 12 21Z" />
              </svg>
            </div>
            <h1 class="mt-8 font-display text-5xl leading-none font-extrabold text-white sm:text-7xl">¡Gracias!</h1>
            <p class="mt-6 text-xl text-bosque-50 sm:text-2xl">
              @if (valor(); as v) {
                Tu aporte de <strong class="text-sol-300">\${{ v | numero }}</strong> ya está ayudando a que Bogotá siga intercambiando, donando y dándole una segunda vida a las cosas.
              } @else {
                Tu aporte ya está ayudando a que Bogotá siga intercambiando, donando y dándole una segunda vida a las cosas.
              }
            </p>
            <p class="mt-4 max-w-lg text-bosque-100/80">
              Gracias a personas como tú, Trueke sigue siendo gratis para todos. Wompi te envía el comprobante al correo.
            </p>
            <div class="mt-10 flex flex-wrap justify-center gap-3">
              <button type="button" class="btn btn-sol btn-lg" (click)="compartir()"><app-icono nombre="usuarios" [tamano]="20" />Invitar a mis vecinos</button>
              <a routerLink="/explorar" class="btn btn-lg text-white hover:bg-white/10">Explorar publicaciones</a>
            </div>
          }
          @case ('pendiente') {
            <div class="grid size-24 place-items-center rounded-full bg-sol-400/15 text-sol-300" aria-hidden="true"><app-icono nombre="reloj" [tamano]="44" /></div>
            <h1 class="mt-8 font-display text-4xl font-extrabold text-white sm:text-5xl">Tu aporte está en camino</h1>
            <p class="mt-5 text-lg text-bosque-100/85">
              Wompi todavía está confirmando el pago. Puede tardar unos minutos; te llegará el comprobante al correo cuando se apruebe. Gracias por tu generosidad.
            </p>
            <div class="mt-10 flex flex-wrap justify-center gap-3">
              <button type="button" class="btn btn-sol btn-lg" (click)="consultar()"><app-icono nombre="refrescar" [tamano]="20" />Consultar de nuevo</button>
              <a routerLink="/" class="btn btn-lg text-white hover:bg-white/10">Ir al inicio</a>
            </div>
          }
          @case ('fallido') {
            <div class="grid size-24 place-items-center rounded-full bg-white/10 text-bosque-100" aria-hidden="true"><app-icono nombre="info" [tamano]="44" /></div>
            <h1 class="mt-8 font-display text-4xl font-extrabold text-white sm:text-5xl">El pago no se completó</h1>
            <p class="mt-5 text-lg text-bosque-100/85">Wompi no aprobó el pago, así que no se te cobró nada. Gracias por querer apoyar: puedes intentarlo de nuevo cuando quieras.</p>
            <div class="mt-10 flex flex-wrap justify-center gap-3">
              @if (donacionUrl(); as enlace) {
                <a [href]="enlace" target="_blank" rel="noopener noreferrer" class="btn btn-sol btn-lg">
                  <app-icono nombre="heart" [tamano]="20" [relleno]="true" />Intentar de nuevo<span class="sr-only"> (abre otra pestaña)</span>
                </a>
              }
              <a routerLink="/" class="btn btn-lg text-white hover:bg-white/10">Ir al inicio</a>
            </div>
          }
          @case ('sin-confirmar') {
            <div class="w-28 text-tierra-500" aria-hidden="true">
              <svg viewBox="0 0 24 24" class="w-full"><path fill="currentColor" d="M12 21s-7.5-4.6-9.6-9.3C1 8.4 3.1 4.5 6.8 4.5c2.1 0 3.6 1.1 4.2 2.3h2c.6-1.2 2.1-2.3 4.2-2.3 3.7 0 5.8 3.9 4.4 7.2C19.5 16.4 12 21 12 21Z" /></svg>
            </div>
            <h1 class="mt-8 font-display text-4xl font-extrabold text-white sm:text-5xl">Gracias por querer apoyar</h1>
            <p class="mt-5 text-lg text-bosque-100/85">
              No pudimos confirmar el estado del pago desde aquí. Si se aprobó, Wompi te envía el comprobante al correo.
            </p>
            <div class="mt-10 flex flex-wrap justify-center gap-3">
              <button type="button" class="btn btn-sol btn-lg" (click)="compartir()"><app-icono nombre="usuarios" [tamano]="20" />Invitar a mis vecinos</button>
              <a routerLink="/" class="btn btn-lg text-white hover:bg-white/10">Ir al inicio</a>
            </div>
          }
        }
      </div>
    </section>
  `,
})
export default class ApoyaGracias {
  private readonly ruta = inject(ActivatedRoute);
  private readonly compartirProyecto = inject(CompartirProyecto);
  protected readonly donacionUrl = inject(ConfigService).donacionUrl;

  protected readonly vista = signal<Vista>('consultando');
  protected readonly valor = signal<number | null>(null);

  private readonly cancelar = new AbortController();
  private temporizador: ReturnType<typeof setTimeout> | undefined;

  /** Posiciones fijas (no aleatorias): la misma lluvia en cada visita y en las pruebas. */
  protected readonly corazones = [
    { x: 6, tam: 22, retraso: 0.1, giro: -20, color: '#e25c3c' },
    { x: 14, tam: 34, retraso: 0.6, giro: 15, color: '#f2b632' },
    { x: 23, tam: 18, retraso: 1.2, giro: -8, color: '#e25c3c' },
    { x: 31, tam: 28, retraso: 0.3, giro: 22, color: '#ffffff' },
    { x: 40, tam: 20, retraso: 1.6, giro: -14, color: '#f2b632' },
    { x: 48, tam: 38, retraso: 0.9, giro: 10, color: '#e25c3c' },
    { x: 57, tam: 24, retraso: 0.2, giro: -25, color: '#ffffff' },
    { x: 65, tam: 30, retraso: 1.4, giro: 18, color: '#f2b632' },
    { x: 73, tam: 18, retraso: 0.5, giro: -10, color: '#e25c3c' },
    { x: 81, tam: 34, retraso: 1.1, giro: 24, color: '#f2b632' },
    { x: 89, tam: 22, retraso: 0.4, giro: -18, color: '#ffffff' },
    { x: 94, tam: 26, retraso: 1.8, giro: 12, color: '#e25c3c' },
  ];

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.cancelar.abort();
      clearTimeout(this.temporizador);
    });
    void this.consultar();
  }

  protected async consultar(intento = 1): Promise<void> {
    clearTimeout(this.temporizador);
    const q = this.ruta.snapshot.queryParamMap;
    const id = q.get('id');
    const env = q.get('env');
    if (!esRegresoWompi(id, env)) {
      this.vista.set('sin-confirmar');
      return;
    }
    try {
      const aporte = await consultarAporte(id, env as string, this.cancelar.signal);
      this.valor.set(aporte.valor);
      if (aporte.estado === 'APPROVED') this.vista.set('aprobado');
      else if (aporte.estado === 'PENDING') {
        this.vista.set(intento === 1 ? 'consultando' : 'pendiente');
        if (intento < INTENTOS_PENDIENTE) this.temporizador = setTimeout(() => void this.consultar(intento + 1), ESPERA_MS);
        else this.vista.set('pendiente');
      } else this.vista.set('fallido');
    } catch {
      if (!this.cancelar.signal.aborted) this.vista.set('sin-confirmar');
    }
  }

  protected compartir(): void {
    void this.compartirProyecto.compartir('Acabo de apoyar a Trueke Bogotá Solidario: intercambia, compra de segunda mano o dona con tus vecinos. Es gratis.');
  }
}
