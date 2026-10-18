import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, untracked, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet, Avatar, Icono, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- grid-cols-1 (minmax(0,1fr)): sin él, en pantallas medianas la columna toma el ancho de las pestañas y desborda la página -->
    <div class="contenedor grid grid-cols-1 gap-8 py-8 sm:py-10 lg:grid-cols-[16rem_1fr]">
      <aside class="min-w-0">
        <div class="flex items-center gap-3">
          <app-avatar [nombre]="sesion.usuario()?.nombreCompleto" [foto]="sesion.usuario()?.fotoUrl" [tamano]="52" [verificado]="!!sesion.usuario()?.verificado" />
          <div class="min-w-0">
            <p class="truncate font-display font-bold">{{ sesion.usuario()?.nombreCompleto }}</p>
            <p class="flex items-center gap-1 text-sm font-semibold text-sol-600 dark:text-sol-300">
              <app-icono nombre="moneda" [tamano]="14" />{{ sesion.usuario()?.saldoEcoPuntos | numero }} Eco-Puntos
            </p>
          </div>
        </div>
        <!-- En celular y tablet: un menú plegable con la sección actual (antes, una fila que se deslizaba de lado y escondía la mitad) -->
        <details #menu class="tarjeta mt-6 lg:hidden">
          <summary class="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-3 font-semibold [&::-webkit-details-marker]:hidden">
            <span class="flex items-center gap-3"><app-icono [nombre]="actual()?.icono ?? 'usuario'" [tamano]="18" />{{ actual()?.texto ?? 'Mi cuenta' }}</span>
            <span class="flex items-center gap-1 text-xs text-tenue">Secciones <app-icono nombre="abajo" [tamano]="16" /></span>
          </summary>
          <nav class="grid grid-cols-1 gap-1 border-t border-borde p-2 sm:grid-cols-2" aria-label="Secciones de la cuenta">
            <ng-container *ngTemplateOutlet="enlacesCuenta" />
          </nav>
        </details>
        <nav class="mt-6 hidden flex-col gap-1 lg:flex" aria-label="Secciones de la cuenta">
          <ng-container *ngTemplateOutlet="enlacesCuenta" />
        </nav>
        <ng-template #enlacesCuenta>
          @for (e of enlaces; track e.ruta) {
            <a [routerLink]="e.ruta" routerLinkActive="!bg-bosque-600 !text-white" class="flex items-center gap-3 rounded-xl px-4 py-2.5 text-sm font-semibold text-tenue transition hover:bg-superficie-2 hover:text-tinta">
              <app-icono [nombre]="e.icono" [tamano]="18" />{{ e.texto }}
            </a>
          }
          <a [routerLink]="['/usuarios', sesion.usuario()?.id]" class="flex items-center gap-3 rounded-xl px-4 py-2.5 text-sm font-semibold text-tenue transition hover:bg-superficie-2 hover:text-tinta">
            <app-icono nombre="externo" [tamano]="18" />Ver mi perfil público
          </a>
        </ng-template>
      </aside>
      <section class="min-w-0"><router-outlet /></section>
    </div>
  `,
})
export default class Cuenta {
  protected readonly sesion = inject(SesionService);
  protected readonly enlaces = [
    { ruta: '/cuenta/perfil', texto: 'Perfil', icono: 'usuario' },
    // Antes solo se llegaba a estas dos desde el menú del avatar
    { ruta: '/mis-publicaciones', texto: 'Mis publicaciones', icono: 'paquete' },
    { ruta: '/favoritos', texto: 'Favoritos', icono: 'heart' },
    { ruta: '/lo-que-busco', texto: 'Lo que busco', icono: 'mira' },
    { ruta: '/cuenta/seguridad', texto: 'Seguridad', icono: 'escudo' },
    { ruta: '/cuenta/avisos', texto: 'Avisos', icono: 'campana' },
    { ruta: '/cuenta/busquedas', texto: 'Búsquedas guardadas', icono: 'buscar' },
    { ruta: '/cuenta/facturacion', texto: 'Pagos y facturas', icono: 'factura' },
    { ruta: '/cuenta/vendedor', texto: 'Registro de vendedor', icono: 'tienda' },
    { ruta: '/cuenta/soporte', texto: 'Soporte y PQR', icono: 'soporte' },
    { ruta: '/cuenta/reportes', texto: 'Reportes sobre ti', icono: 'balanza' },
    { ruta: '/cuenta/privacidad', texto: 'Privacidad y datos', icono: 'candado' },
  ];

  private readonly url = toSignal(
    inject(Router).events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: inject(Router).url },
  );
  /** La sección abierta, para el resumen del menú plegable del celular. */
  protected readonly actual = computed(() => this.enlaces.find((e) => this.url().split(/[?#]/)[0] === e.ruta));

  private readonly menu = viewChild<ElementRef<HTMLDetailsElement>>('menu');

  constructor() {
    // Al elegir una sección (cambia la ruta), el menú plegable se cierra
    effect(() => {
      this.url();
      const m = untracked(() => this.menu()?.nativeElement);
      if (m) m.open = false;
    });
  }
}
