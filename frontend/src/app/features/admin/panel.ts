import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminApi } from '../../core/api/admin.api';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
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
        <div class="flex flex-wrap items-center gap-2">
          @if (!sesion.usuario()?.dosFactoresActivo) {
            <a routerLink="/cuenta/seguridad" class="btn btn-sol btn-sm"><app-icono nombre="alerta" [tamano]="14" />Activa la verificación en dos pasos</a>
          }
          <a routerLink="/" class="btn btn-sm border border-white/30 text-white hover:bg-white/10"><app-icono nombre="izquierda" [tamano]="14" />Volver al sitio</a>
        </div>
      </div>
      <nav class="contenedor flex gap-1 overflow-x-auto" aria-label="Secciones de moderación">
        @for (e of enlaces(); track e.ruta) {
          <a [routerLink]="e.ruta" routerLinkActive="!bg-fondo !text-tinta" class="flex shrink-0 items-center gap-2 rounded-t-2xl px-4 py-3 text-sm font-semibold text-white/80 transition hover:text-white">
            <app-icono [nombre]="e.icono" [tamano]="16" />{{ e.texto }}
            @if (e.ruta === 'publicaciones' && pendientes()) {
              <span class="rounded-full bg-sol-400 px-1.5 text-xs font-bold text-bosque-900 tabular-nums" [attr.aria-label]="pendientes() + ' por revisar'">{{ pendientes() }}</span>
            }
          </a>
        }
      </nav>
    </section>
    <div class="contenedor py-8"><router-outlet /></div>
  `,
})
export default class Panel {
  protected readonly sesion = inject(SesionService);
  private readonly admin = inject(AdminApi);
  private readonly cola = rxResource({ stream: () => this.admin.colaModeracion() });
  private readonly tiempoReal = inject(TiempoRealService);
  /** Lo que espera revisión: llega en vivo; mientras no cambie, el valor con que se abrió el panel. */
  protected readonly pendientes = computed(() => this.tiempoReal.colaModeracion() ?? this.cola.value()?.publicaciones ?? 0);
  protected readonly enlaces = computed(() => [
    { ruta: 'publicaciones', texto: 'Publicaciones', icono: 'reloj' },
    { ruta: 'denuncias', texto: 'Denuncias', icono: 'bandera' },
    { ruta: 'apelaciones', texto: 'Apelaciones', icono: 'balanza' },
    { ruta: 'usuarios', texto: 'Usuarios', icono: 'usuarios' },
    { ruta: 'fotos', texto: 'Fotos de perfil', icono: 'camara' },
    { ruta: 'verificaciones', texto: 'Verificaciones', icono: 'verificado' },
    { ruta: 'negocios', texto: 'Negocios', icono: 'edificio' },
    { ruta: 'empresas', texto: 'Empresas e informes', icono: 'grafica' },
    { ruta: 'pagos', texto: 'Pagos en revisión', icono: 'billetera' },
    { ruta: 'facturas', texto: 'Facturas', icono: 'factura' },
    { ruta: 'pqr', texto: 'PQR', icono: 'soporte' },
    ...(this.sesion.esSuperUsuario()
      ? [{ ruta: 'fundaciones', texto: 'Fundaciones', icono: 'heart' }, { ruta: 'impacto', texto: 'Impacto', icono: 'hoja' }, { ruta: 'ingresos', texto: 'Ingresos', icono: 'grafica' }]
      : []),
  ]);
}
