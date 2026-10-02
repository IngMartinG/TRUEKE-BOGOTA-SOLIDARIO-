import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { SesionService } from '../core/sesion.service';
import { TemaService } from '../core/tema.service';
import { TiempoRealService } from '../core/tiempo-real.service';
import { NumeroPipe } from '../shared/pipes';
import { Avatar } from '../shared/ui/avatar';
import { Icono } from '../shared/ui/icono';
import { Logo } from '../shared/ui/logo';

@Component({
  selector: 'app-encabezado',
  imports: [RouterLink, RouterLinkActive, FormsModule, CdkMenuTrigger, CdkMenu, CdkMenuItem, Logo, Icono, Avatar, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="sticky top-0 z-50 border-b border-borde bg-fondo/85 backdrop-blur-xl">
      <div class="contenedor flex h-16 items-center gap-3 lg:gap-6">
        <a routerLink="/" class="shrink-0 rounded-full" aria-label="Trueke Bogotá Solidario, inicio">
          <app-logo [tamano]="38" />
        </a>

        <nav class="hidden items-center gap-1 whitespace-nowrap xl:flex" aria-label="Principal">
          @for (e of enlaces; track e.ruta) {
            <a
              [routerLink]="e.ruta"
              routerLinkActive="bg-bosque-100 text-bosque-800 dark:bg-bosque-900 dark:text-bosque-100"
              class="rounded-full px-3.5 py-2 text-sm font-semibold text-tenue transition hover:text-tinta"
              >{{ e.texto }}</a
            >
          }
        </nav>

        <form class="relative hidden flex-1 md:block" role="search" (ngSubmit)="buscar()">
          <app-icono nombre="buscar" [tamano]="18" class="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-tenue" />
          <input
            type="search"
            name="q"
            [(ngModel)]="texto"
            class="entrada rounded-full bg-superficie pl-11"
            placeholder="Busca chaquetas, libros, bicicletas..."
            aria-label="Buscar en el catálogo"
            maxlength="100"
          />
        </form>

        <div class="ml-auto flex items-center gap-1 sm:gap-2">
          <button type="button" class="btn-icono md:hidden" routerLink="/explorar" aria-label="Buscar">
            <app-icono nombre="buscar" />
          </button>
          <button
            type="button"
            class="btn-icono hidden sm:inline-flex"
            (click)="tema.alternar()"
            [attr.aria-label]="tema.oscuroActivo() ? 'Activar modo claro' : 'Activar modo oscuro'"
          >
            <app-icono [nombre]="tema.oscuroActivo() ? 'sol' : 'luna'" />
          </button>

          @if (sesion.autenticado()) {
            <a routerLink="/publicar" class="btn btn-primario hidden sm:inline-flex">
              <app-icono nombre="mas" [tamano]="18" />Publicar
            </a>
            <a routerLink="/mensajes" class="btn-icono relative hidden sm:inline-flex" aria-label="Mensajes">
              <app-icono nombre="mensaje" />
              @if (tiempoReal.mensajesNoLeidos() > 0) {
                <span class="absolute top-1 right-1 grid min-w-4.5 place-items-center rounded-full bg-tierra-600 px-1 text-[10px] font-bold text-white">
                  {{ tiempoReal.mensajesNoLeidos() > 9 ? '9+' : tiempoReal.mensajesNoLeidos() }}
                </span>
              }
            </a>
            <a routerLink="/notificaciones" class="btn-icono relative" aria-label="Notificaciones">
              <app-icono nombre="campana" />
              @if (tiempoReal.notificacionesNoLeidas() > 0) {
                <span class="absolute top-1 right-1 grid min-w-4.5 place-items-center rounded-full bg-tierra-600 px-1 text-[10px] font-bold text-white">
                  {{ tiempoReal.notificacionesNoLeidas() > 9 ? '9+' : tiempoReal.notificacionesNoLeidas() }}
                </span>
              }
            </a>

            <button
              type="button"
              class="flex items-center gap-2 rounded-full border border-borde bg-superficie py-1 pr-3 pl-1 transition hover:border-bosque-300"
              [cdkMenuTriggerFor]="menuUsuario"
              aria-label="Menú de tu cuenta"
            >
              <app-avatar [nombre]="sesion.usuario()?.nombreCompleto" [tamano]="32" [verificado]="!!sesion.usuario()?.verificado" />
              <span class="hidden items-center gap-1 text-sm font-bold text-sol-600 sm:flex dark:text-sol-300">
                <app-icono nombre="moneda" [tamano]="16" />{{ sesion.usuario()?.saldoEcoPuntos | numero }}
              </span>
            </button>
          } @else {
            <a routerLink="/ingresar" class="btn btn-fantasma hidden sm:inline-flex">Ingresar</a>
            <a routerLink="/registro" class="btn btn-primario">Crear cuenta</a>
          }
        </div>
      </div>
    </header>

    <ng-template #menuUsuario>
      <div cdkMenu class="z-50 mt-2 w-72 animate-aparecer overflow-hidden rounded-2xl border border-borde bg-superficie p-2 shadow-elevada">
        <div class="mb-1 rounded-xl bg-gradient-to-br from-bosque-600 to-bosque-800 p-4 text-white">
          <p class="truncate font-display font-bold">{{ sesion.usuario()?.nombreCompleto }}</p>
          <p class="truncate text-xs text-white/70">{{ sesion.usuario()?.correo }}</p>
          <div class="mt-3 flex items-center justify-between">
            <span class="flex items-center gap-1.5 text-sm font-bold text-sol-300">
              <app-icono nombre="moneda" [tamano]="16" />{{ sesion.usuario()?.saldoEcoPuntos | numero }} Eco-Puntos
            </span>
            <span class="flex items-center gap-1 text-xs text-white/80">
              <app-icono nombre="estrella" [tamano]="14" [relleno]="true" class="text-sol-300" />{{ sesion.usuario()?.reputacion?.toFixed(1) }}
            </span>
          </div>
        </div>
        @for (item of menu; track item.ruta) {
          <a cdkMenuItem [routerLink]="item.ruta" class="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium outline-none hover:bg-superficie-2 focus:bg-superficie-2">
            <app-icono [nombre]="item.icono" [tamano]="18" class="text-tenue" />{{ item.texto }}
          </a>
        }
        @if (sesion.esModerador()) {
          <a cdkMenuItem routerLink="/admin" class="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-agua-700 outline-none hover:bg-superficie-2 focus:bg-superficie-2 dark:text-agua-100">
            <app-icono nombre="escudo" [tamano]="18" />Moderación
          </a>
        }
        <button cdkMenuItem type="button" (click)="tema.alternar()" class="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium outline-none hover:bg-superficie-2 focus:bg-superficie-2 sm:hidden">
          <app-icono [nombre]="tema.oscuroActivo() ? 'sol' : 'luna'" [tamano]="18" class="text-tenue" />
          {{ tema.oscuroActivo() ? 'Modo claro' : 'Modo oscuro' }}
        </button>
        <div class="my-1 border-t border-borde"></div>
        <button cdkMenuItem type="button" (click)="sesion.salir()" class="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-tierra-600 outline-none hover:bg-tierra-50 focus:bg-tierra-50 dark:hover:bg-tierra-700/20">
          <app-icono nombre="salir" [tamano]="18" />Cerrar sesión
        </button>
      </div>
    </ng-template>
  `,
})
export class Encabezado {
  protected readonly sesion = inject(SesionService);
  protected readonly tema = inject(TemaService);
  protected readonly tiempoReal = inject(TiempoRealService);
  private readonly router = inject(Router);
  protected readonly texto = signal('');

  protected readonly enlaces = [
    { ruta: '/explorar', texto: 'Explorar' },
    { ruta: '/como-funciona', texto: 'Cómo funciona' },
    { ruta: '/eco-puntos', texto: 'Eco-Puntos' },
  ];

  protected readonly menu = [
    { ruta: '/cuenta/perfil', texto: 'Mi cuenta', icono: 'usuario' },
    { ruta: '/mis-publicaciones', texto: 'Mis publicaciones', icono: 'paquete' },
    { ruta: '/intercambios', texto: 'Mis intercambios', icono: 'apreton' },
    { ruta: '/mensajes', texto: 'Mensajes', icono: 'mensaje' },
    { ruta: '/favoritos', texto: 'Favoritos', icono: 'heart' },
    { ruta: '/eco-puntos', texto: 'Eco-Puntos y planes', icono: 'moneda' },
  ];

  protected buscar(): void {
    const texto = this.texto().trim();
    void this.router.navigate(['/explorar'], { queryParams: texto ? { texto } : {} });
  }
}
