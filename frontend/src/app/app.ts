import { LiveAnnouncer } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { ConfigService } from './core/config.service';
import { SesionService } from './core/sesion.service';
import { TemaService } from './core/tema.service';
import { TiempoRealService } from './core/tiempo-real.service';
import { AsistenteSoporte } from './features/ayuda/asistente-soporte';
import { BarraMovil } from './layout/barra-movil';
import { Encabezado } from './layout/encabezado';
import { Pie } from './layout/pie';
import { Avisos } from './shared/ui/avisos';
import { Icono } from './shared/ui/icono';
import { LogroIntercambio } from './shared/ui/logro-intercambio';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, Encabezado, Pie, BarraMovil, Avisos, Icono, LogroIntercambio, AsistenteSoporte],
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
          <app-icono nombre="correo" [tamano]="16" class="text-sol-600 dark:text-sol-300" />
          <span>Confirma tu correo para activar tu cuenta. Mientras tanto solo puedes explorar el catálogo.</span>
          <a routerLink="/verifica-tu-correo" class="enlace">Ver cómo</a>
        </div>
      </div>
    } @else if (sesion.autenticado() && !sesion.tieneFoto()) {
      <div class="border-b border-sol-300/50 bg-sol-50 dark:bg-sol-500/10">
        <div class="contenedor flex flex-wrap items-center justify-center gap-x-3 gap-y-1 py-2.5 text-center text-sm">
          <app-icono nombre="camara" [tamano]="16" class="text-sol-600 dark:text-sol-300" />
          <span>Agrega una foto de perfil donde se vea tu cara para publicar, solicitar y chatear.</span>
          <a routerLink="/cuenta/perfil" fragment="foto" class="enlace">Agregar foto</a>
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
    <app-logro-intercambio />
    <!-- El asistente no es urgente: se carga cuando el navegador queda libre, fuera del paquete inicial. -->
    @defer (on idle) {
      <app-asistente-soporte />
    }
  `,
})
export class App {
  protected readonly config = inject(ConfigService);
  protected readonly sesion = inject(SesionService);
  // Se inyectan aquí para que arranquen con la app (tema y conexión en tiempo real).
  private readonly _tema = inject(TemaService);
  private readonly _tiempoReal = inject(TiempoRealService);

  constructor() {
    // Al cambiar de página (no al cambiar solo filtros o el #fragmento), el foco va al contenido y el lector de
    // pantalla anuncia el título nuevo; si no, la persona queda en un enlace que ya no existe sin saber qué pasó.
    const anunciador = inject(LiveAnnouncer);
    const titulo = inject(Title);
    let anterior: string | null = null;
    inject(Router)
      .events.pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((e) => {
        const ruta = e.urlAfterRedirects.split(/[?#]/)[0];
        if (anterior !== null && ruta !== anterior) {
          setTimeout(() => {
            document.getElementById('contenido')?.focus({ preventScroll: true });
            void anunciador.announce(titulo.getTitle());
          });
        }
        anterior = ruta;
      });
  }
}
