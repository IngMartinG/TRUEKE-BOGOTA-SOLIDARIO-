import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SesionService } from '../../core/sesion.service';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-r from-agua-700 to-bosque-800 text-white">
      <div class="contenedor flex flex-wrap items-center justify-between gap-4 py-8">
        <div class="flex items-center gap-4">
          <span class="grid size-12 place-items-center rounded-2xl bg-white/15"><app-icono nombre="escudo" [tamano]="26" /></span>
          <div>
            <h1 class="text-2xl font-extrabold text-white">Administración</h1>
            <p class="text-sm text-white/75">Mantén la comunidad segura y confiable · rol {{ sesion.usuario()?.rol }}</p>
          </div>
        </div>
        @if (!sesion.usuario()?.dosFactoresActivo) {
          <a routerLink="/cuenta/seguridad" class="btn btn-sol btn-sm"><app-icono nombre="alerta" [tamano]="14" />Activa la verificación en dos pasos</a>
        }
      </div>
      <nav class="contenedor flex gap-1 overflow-x-auto" aria-label="Secciones de moderación">
        @for (e of enlaces(); track e.ruta) {
          <a [routerLink]="e.ruta" routerLinkActive="!bg-fondo !text-tinta" class="flex shrink-0 items-center gap-2 rounded-t-2xl px-4 py-3 text-sm font-semibold text-white/80 transition hover:text-white">
            <app-icono [nombre]="e.icono" [tamano]="16" />{{ e.texto }}
          </a>
        }
      </nav>
    </section>
    <div class="contenedor py-8"><router-outlet /></div>
  `,
})
export default class Panel {
  protected readonly sesion = inject(SesionService);
  protected readonly enlaces = computed(() => [
    { ruta: 'denuncias', texto: 'Denuncias', icono: 'bandera' },
    { ruta: 'usuarios', texto: 'Usuarios', icono: 'usuarios' },
    { ruta: 'verificaciones', texto: 'Verificaciones', icono: 'verificado' },
    { ruta: 'pagos', texto: 'Pagos en revisión', icono: 'billetera' },
    { ruta: 'facturas', texto: 'Facturas', icono: 'factura' },
    { ruta: 'pqr', texto: 'PQR', icono: 'soporte' },
    ...(this.sesion.esSuperUsuario() ? [{ ruta: 'ingresos', texto: 'Ingresos', icono: 'grafica' }] : []),
  ]);
}
