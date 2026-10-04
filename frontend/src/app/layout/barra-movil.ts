import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { SesionService } from '../core/sesion.service';
import { TiempoRealService } from '../core/tiempo-real.service';
import { Icono } from '../shared/ui/icono';

/** Navegación inferior en celular y tablet (< 1024 px), con el botón de publicar destacado al centro. */
@Component({
  selector: 'app-barra-movil',
  imports: [RouterLink, RouterLinkActive, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav
      class="fixed inset-x-0 bottom-0 z-50 border-t border-borde bg-superficie/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-xl lg:hidden"
      aria-label="Navegación inferior"
    >
      <ul class="mx-auto grid max-w-xl grid-cols-5 items-end">
        @for (e of izquierda(); track e.ruta) {
          <li>
            <a [routerLink]="e.ruta" routerLinkActive="!text-bosque-700 dark:!text-bosque-300" [routerLinkActiveOptions]="{ exact: e.ruta === '/' }"
              class="relative flex flex-col items-center gap-0.5 py-2 text-[11px] font-semibold text-tenue">
              <app-icono [nombre]="e.icono" [tamano]="22" />{{ e.texto }}
            </a>
          </li>
        }
        <li class="flex justify-center">
          <a routerLink="/publicar" class="-mt-5 grid size-14 place-items-center rounded-full bg-bosque-600 text-white shadow-[0_8px_20px_-6px_rgb(31_122_77/0.7)] ring-4 ring-fondo active:scale-95" aria-label="Publicar algo">
            <app-icono nombre="mas" [tamano]="26" [grosor]="2.5" />
          </a>
        </li>
        @for (e of derecha(); track e.ruta) {
          <li>
            <a [routerLink]="e.ruta" routerLinkActive="!text-bosque-700 dark:!text-bosque-300"
              class="relative flex flex-col items-center gap-0.5 py-2 text-[11px] font-semibold text-tenue">
              <app-icono [nombre]="e.icono" [tamano]="22" />{{ e.texto }}
              @if (e.contador && e.contador() > 0) {
                <span class="absolute top-1 left-1/2 ml-2 size-2.5 rounded-full bg-tierra-600 ring-2 ring-superficie"></span>
              }
            </a>
          </li>
        }
      </ul>
    </nav>
  `,
})
export class BarraMovil {
  private readonly sesion = inject(SesionService);
  private readonly tiempoReal = inject(TiempoRealService);

  protected izquierda() {
    return this.sesion.autenticado()
      ? [
          { ruta: '/explorar', texto: 'Explorar', icono: 'brujula' },
          { ruta: '/intercambios', texto: 'Trueques', icono: 'apreton' },
        ]
      : [
          { ruta: '/', texto: 'Inicio', icono: 'inicio' },
          { ruta: '/explorar', texto: 'Explorar', icono: 'brujula' },
        ];
  }

  protected derecha(): { ruta: string; texto: string; icono: string; contador?: () => number }[] {
    return this.sesion.autenticado()
      ? [
          { ruta: '/mensajes', texto: 'Mensajes', icono: 'mensaje', contador: this.tiempoReal.mensajesNoLeidos },
          { ruta: '/cuenta', texto: 'Cuenta', icono: 'usuario' },
        ]
      : [
          { ruta: '/eco-puntos', texto: 'Eco-Puntos', icono: 'moneda' },
          { ruta: '/ingresar', texto: 'Ingresar', icono: 'usuario' },
        ];
  }
}
