import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Avatar, Icono, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor grid gap-8 py-8 sm:py-10 lg:grid-cols-[16rem_1fr]">
      <aside>
        <div class="flex items-center gap-3">
          <app-avatar [nombre]="sesion.usuario()?.nombreCompleto" [tamano]="52" [verificado]="!!sesion.usuario()?.verificado" />
          <div class="min-w-0">
            <p class="truncate font-display font-bold">{{ sesion.usuario()?.nombreCompleto }}</p>
            <p class="flex items-center gap-1 text-sm font-semibold text-sol-600 dark:text-sol-300">
              <app-icono nombre="moneda" [tamano]="14" />{{ sesion.usuario()?.saldoEcoPuntos | numero }} Eco-Puntos
            </p>
          </div>
        </div>
        <nav class="mt-6 flex gap-1 overflow-x-auto lg:flex-col" aria-label="Secciones de la cuenta">
          @for (e of enlaces; track e.ruta) {
            <a [routerLink]="e.ruta" routerLinkActive="!bg-bosque-600 !text-white" class="flex shrink-0 items-center gap-3 rounded-xl px-4 py-2.5 text-sm font-semibold text-tenue transition hover:bg-superficie-2 hover:text-tinta">
              <app-icono [nombre]="e.icono" [tamano]="18" />{{ e.texto }}
            </a>
          }
          <a [routerLink]="['/usuarios', sesion.usuario()?.id]" class="flex shrink-0 items-center gap-3 rounded-xl px-4 py-2.5 text-sm font-semibold text-tenue transition hover:bg-superficie-2 hover:text-tinta">
            <app-icono nombre="externo" [tamano]="18" />Ver mi perfil público
          </a>
        </nav>
      </aside>
      <section class="min-w-0"><router-outlet /></section>
    </div>
  `,
})
export default class Cuenta {
  protected readonly sesion = inject(SesionService);
  protected readonly enlaces = [
    { ruta: '/cuenta/perfil', texto: 'Perfil', icono: 'usuario' },
    { ruta: '/cuenta/seguridad', texto: 'Seguridad', icono: 'escudo' },
    { ruta: '/cuenta/privacidad', texto: 'Privacidad y datos', icono: 'candado' },
  ];
}
