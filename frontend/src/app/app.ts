import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { ConfigService } from './core/config.service';
import { SesionService } from './core/sesion.service';
import { TemaService } from './core/tema.service';
import { TiempoRealService } from './core/tiempo-real.service';
import { BarraMovil } from './layout/barra-movil';
import { Encabezado } from './layout/encabezado';
import { Pie } from './layout/pie';
import { Avisos } from './shared/ui/avisos';
import { Icono } from './shared/ui/icono';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, Encabezado, Pie, BarraMovil, Avisos, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a href="#contenido" class="sr-only z-[200] rounded-full bg-bosque-700 px-4 py-2 text-white focus:not-sr-only focus:fixed focus:top-3 focus:left-3">
      Saltar al contenido
    </a>
    <app-encabezado />

    @if (!config.apiDisponible()) {
      <div class="bg-tierra-600 px-4 py-2.5 text-center text-sm font-medium text-white" role="alert">
        No pudimos conectar con el servidor. Algunas funciones no estarán disponibles; recarga en unos minutos.
      </div>
    } @else if (sesion.autenticado() && !sesion.correoVerificado()) {
      <div class="border-b border-sol-300/50 bg-sol-50 dark:bg-sol-500/10">
        <div class="contenedor flex flex-wrap items-center justify-center gap-x-3 gap-y-1 py-2.5 text-center text-sm">
          <app-icono nombre="correo" [tamano]="16" class="text-sol-600" />
          <span>Confirma tu correo para activar tu cuenta. Mientras tanto solo puedes explorar el catálogo.</span>
          <a routerLink="/verifica-tu-correo" class="enlace">Ver cómo</a>
        </div>
      </div>
    }
    @if (config.pagosDePrueba()) {
      <div class="border-b border-cielo-100 bg-cielo-50 dark:border-cielo-700/40 dark:bg-cielo-700/20" role="note">
        <div class="contenedor flex flex-wrap items-center justify-center gap-x-3 gap-y-1 py-2 text-center text-sm">
          <app-icono nombre="info" [tamano]="16" class="text-cielo-600" />
          <span><strong>Sitio de demostración:</strong> los pagos están en modo de prueba y no se cobra dinero real.</span>
          <a routerLink="/eco-puntos" fragment="pagos-de-prueba" class="enlace">Cómo probar</a>
        </div>
      </div>
    }

    <main id="contenido" class="min-h-[60vh]" tabindex="-1">
      <router-outlet />
    </main>
    <app-pie />
    <app-barra-movil />
    <app-avisos />
  `,
})
export class App {
  protected readonly config = inject(ConfigService);
  protected readonly sesion = inject(SesionService);
  // Se inyectan aquí para que arranquen con la app (tema y conexión en tiempo real).
  private readonly _tema = inject(TemaService);
  private readonly _tiempoReal = inject(TiempoRealService);
}
