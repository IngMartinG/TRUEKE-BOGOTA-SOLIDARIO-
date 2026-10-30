import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AdminApi } from '../../core/api/admin.api';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { Icono } from '../../shared/ui/icono';

interface Enlace {
  ruta: string;
  texto: string;
  icono: string;
}

/**
 * Secciones agrupadas por tema: barra lateral fija en PC y, en celular y tablet, un selector nativo con los mismos grupos
 * (el teclado y el lector de pantalla del sistema ya lo resuelven). Antes eran pestañas horizontales con scroll.
 */
@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-r from-agua-700 to-bosque-800 text-white">
      <div class="contenedor flex flex-wrap items-center justify-between gap-4 py-6">
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
    </section>

    <div class="contenedor grid gap-6 py-6 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-10 lg:py-8">
      <div class="lg:hidden">
        <label for="seccion-admin" class="etiqueta">Sección</label>
        <select id="seccion-admin" class="entrada font-semibold" (change)="ir($event)">
          @for (g of grupos(); track g.titulo) {
            <optgroup [label]="g.titulo">
              @for (e of g.enlaces; track e.ruta) {
                <option [value]="e.ruta" [selected]="e.ruta === actual()">
                  {{ e.texto }}{{ e.ruta === 'publicaciones' && pendientes() ? ' (' + pendientes() + ' por revisar)' : '' }}
                </option>
              }
            </optgroup>
          }
        </select>
      </div>

      <nav class="hidden lg:block" aria-label="Secciones de administración">
        <div class="sticky top-24 max-h-[calc(100dvh-7rem)] space-y-6 overflow-y-auto pr-1 pb-4">
          @for (g of grupos(); track g.titulo; let i = $index) {
            <div>
              <p class="px-3 text-xs font-bold tracking-wide text-tenue uppercase" [id]="'grupo-admin-' + i">{{ g.titulo }}</p>
              <ul class="mt-2 space-y-0.5" [attr.aria-labelledby]="'grupo-admin-' + i">
                @for (e of g.enlaces; track e.ruta) {
                  <li>
                    <a
                      [routerLink]="e.ruta"
                      routerLinkActive="!bg-bosque-100 !text-bosque-800 dark:!bg-bosque-900 dark:!text-bosque-100"
                      ariaCurrentWhenActive="page"
                      class="flex items-center gap-3 rounded-xl px-3 py-2 text-sm font-semibold text-tenue transition-colors hover:bg-superficie-2 hover:text-tinta"
                    >
                      <app-icono [nombre]="e.icono" [tamano]="18" class="shrink-0" />
                      <span class="min-w-0 flex-1 truncate">{{ e.texto }}</span>
                      @if (e.ruta === 'publicaciones' && pendientes()) {
                        <span class="rounded-full bg-sol-400 px-1.5 text-xs font-bold text-bosque-900 tabular-nums" [attr.aria-label]="pendientes() + ' por revisar'">{{ pendientes() }}</span>
                      }
                    </a>
                  </li>
                }
              </ul>
            </div>
          }
        </div>
      </nav>

      <div class="min-w-0"><router-outlet /></div>
    </div>
  `,
})
export default class Panel {
  protected readonly sesion = inject(SesionService);
  private readonly admin = inject(AdminApi);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly cola = rxResource({ stream: () => this.admin.colaModeracion() });
  private readonly tiempoReal = inject(TiempoRealService);
  /** Lo que espera revisión: llega en vivo; mientras no cambie, el valor con que se abrió el panel (0 si no cargó). */
  protected readonly pendientes = computed(
    () => this.tiempoReal.colaModeracion() ?? (this.cola.hasValue() ? this.cola.value().publicaciones : undefined) ?? 0,
  );

  /** La sección abierta (el primer segmento después de /admin), para el selector del celular. */
  protected readonly actual = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.seccionActual()),
    ),
    { initialValue: this.seccionActual() },
  );

  protected readonly grupos = computed(() => {
    const su = this.sesion.esSuperUsuario();
    const grupos: { titulo: string; enlaces: Enlace[] }[] = [
      {
        titulo: 'Moderación',
        enlaces: [
          { ruta: 'publicaciones', texto: 'Publicaciones', icono: 'reloj' },
          { ruta: 'denuncias', texto: 'Denuncias', icono: 'bandera' },
          { ruta: 'apelaciones', texto: 'Apelaciones', icono: 'balanza' },
          { ruta: 'fotos', texto: 'Fotos de perfil', icono: 'camara' },
        ],
      },
      {
        titulo: 'Personas',
        enlaces: [
          { ruta: 'usuarios', texto: 'Usuarios', icono: 'usuarios' },
          { ruta: 'verificaciones', texto: 'Verificaciones', icono: 'verificado' },
          { ruta: 'pqr', texto: 'PQR', icono: 'soporte' },
        ],
      },
      {
        titulo: 'Organizaciones',
        enlaces: [
          { ruta: 'negocios', texto: 'Negocios', icono: 'edificio' },
          { ruta: 'empresas', texto: 'Empresas e informes', icono: 'grafica' },
          ...(su ? [{ ruta: 'fundaciones', texto: 'Fundaciones', icono: 'heart' }] : []),
        ],
      },
      {
        titulo: 'Dinero',
        enlaces: [
          { ruta: 'pagos', texto: 'Pagos en revisión', icono: 'billetera' },
          { ruta: 'facturas', texto: 'Facturas', icono: 'factura' },
          ...(su ? [{ ruta: 'ingresos', texto: 'Ingresos', icono: 'grafica' }] : []),
        ],
      },
      ...(su ? [{ titulo: 'Plataforma', enlaces: [{ ruta: 'impacto', texto: 'Factores de impacto', icono: 'hoja' }] }] : []),
    ];
    return grupos;
  });

  protected ir(evento: Event): void {
    void this.router.navigate([(evento.target as HTMLSelectElement).value], { relativeTo: this.ruta });
  }

  private seccionActual(): string {
    return this.router.url.split(/[?#]/)[0].split('/admin/')[1]?.split('/')[0] ?? 'publicaciones';
  }
}
