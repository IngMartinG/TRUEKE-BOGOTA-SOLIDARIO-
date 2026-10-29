import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CuentaApi } from '../../core/api/cuenta.api';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Icono } from '../../shared/ui/icono';
import { InsigniaFundacion } from '../../shared/ui/insignia-fundacion';
import { InsigniaRegimen } from '../../shared/ui/insignia-regimen';
import { Paginador } from '../../shared/ui/paginador';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';
import { Volver } from '../../shared/ui/volver';

const formatoMesAnio = new Intl.DateTimeFormat('es-CO', { month: 'long', year: 'numeric', timeZone: 'America/Bogota' });

/**
 * Página pública de una fundación verificada: quién es, lo que necesita (urgente primero) y lo que publica. Cada necesidad
 * lleva a publicar una donación con ese título, para que la fundación la pida.
 */
@Component({
  imports: [RouterLink, Volver, Avatar, Estrellas, Icono, InsigniaFundacion, InsigniaRegimen, EstadoVacio, Paginador, TarjetaPublicacion, TarjetaEsqueleto, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (fundacion.error()) {
      <app-estado-vacio icono="heart" titulo="Esta fundación no está disponible" descripcion="Puede que ya no esté verificada.">
        <a routerLink="/fundaciones" class="btn btn-primario">Ver las fundaciones</a>
      </app-estado-vacio>
    } @else {
      <section class="border-b border-borde bg-tierra-50/70 dark:bg-tierra-700/10">
        <div class="contenedor pt-4"><app-volver respaldo="/fundaciones" texto="Fundaciones" /></div>
        <div class="contenedor flex flex-col gap-6 pt-4 pb-10 sm:flex-row sm:items-start">
          @if (fundacion.value(); as f) {
            <app-avatar [nombre]="f.nombre" [foto]="f.fotoUrl" [tamano]="104" />
            <div class="min-w-0 flex-1">
              <div class="flex flex-wrap items-center gap-2">
                <app-insignia-fundacion />
                @if (f.regimenTributarioEspecial) {
                  <app-insignia-regimen />
                }
              </div>
              <h1 class="mt-2 text-3xl font-extrabold text-balance sm:text-4xl">{{ f.nombre }}</h1>
              <p class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-tenue">
                <span class="flex items-center gap-1"><app-icono nombre="pin" [tamano]="14" />{{ f.localidad }}{{ f.municipio ? ', ' + f.municipio : '' }}</span>
                <span>Verificada desde {{ mesYAnio(f.verificadaDesde) }}</span>
                @if (f.nit) {
                  <span>NIT {{ f.nit }}</span>
                }
              </p>
              <p class="mt-4 max-w-prose whitespace-pre-line text-tinta/90">{{ f.descripcion }}</p>
              <div class="mt-5 flex flex-wrap items-center gap-3">
                @if (f.calificacionPromedio) {
                  <span class="flex items-center gap-2 text-sm">
                    <app-estrellas [valor]="f.calificacionPromedio" [tamano]="16" />
                    <span class="font-semibold">{{ f.calificacionPromedio.toFixed(1) }}</span>
                    <span class="text-tenue">({{ f.totalCalificaciones | numero }})</span>
                  </span>
                }
                <a [routerLink]="['/usuarios', f.id]" class="enlace text-sm font-semibold">Ver su perfil y calificaciones</a>
              </div>
            </div>
          } @else {
            <div class="esqueleto size-26 rounded-full"></div>
            <div class="flex-1 space-y-3"><div class="esqueleto h-9 w-72"></div><div class="esqueleto h-4 w-48"></div><div class="esqueleto h-16 w-full max-w-prose"></div></div>
          }
        </div>
      </section>

      <div class="contenedor py-10">
        <section aria-labelledby="titulo-necesidades">
          <div class="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 id="titulo-necesidades" class="text-2xl font-extrabold">Lo que necesitamos</h2>
              <p class="mt-1 max-w-2xl text-tenue">
                ¿Tienes algo de esto? Publícalo como donación y la fundación podrá pedírtelo; coordinan la entrega por el chat.
              </p>
            </div>
            @if (esMia()) {
              <a routerLink="/cuenta/fundacion" class="btn btn-secundario btn-sm"><app-icono nombre="editar" [tamano]="14" />Editar la lista</a>
            }
          </div>

          @if (necesidades().length) {
            <ul class="mt-6 divide-y divide-borde overflow-hidden rounded-tarjeta border border-borde bg-superficie">
              @for (n of necesidades(); track n.id) {
                <li class="grid grid-cols-[4.5rem_1fr] items-center gap-x-4 gap-y-3 p-4 sm:grid-cols-[5.5rem_1fr_auto] sm:p-5"
                  [class]="n.cubierta ? 'opacity-75' : n.urgente ? 'bg-sol-50 dark:bg-sol-500/10' : ''">
                  <div class="text-center" [attr.aria-hidden]="!n.cantidad">
                    @if (n.cantidad) {
                      <p class="font-display text-3xl leading-none font-extrabold tabular-nums">{{ n.cantidad | numero }}</p>
                      <p class="mt-1 text-xs text-tenue">aprox.</p>
                    } @else {
                      <app-icono nombre="regalo" [tamano]="28" class="text-tierra-600 dark:text-tierra-400" />
                    }
                  </div>
                  <div class="min-w-0">
                    <p class="text-lg leading-snug font-bold break-words">{{ n.texto }}</p>
                    <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-tenue">
                      @if (n.urgente) {
                        <span class="insignia-sol"><app-icono nombre="reloj" [tamano]="12" />Urgente</span>
                      }
                      @if (n.categoria) {
                        <span>{{ n.categoria }}</span>
                      }
                    </p>
                    @if (n.cubierta) {
                      <p class="mt-2 flex items-center gap-1 text-sm font-semibold text-bosque-700 dark:text-bosque-300">
                        <app-icono nombre="check" [tamano]="14" />¡Cubierta, gracias a la comunidad!
                      </p>
                    } @else if (n.cantidad && n.recibidos) {
                      <div class="mt-2 max-w-xs">
                        <p class="text-xs text-tenue">{{ n.recibidos | numero }} de {{ n.cantidad | numero }} recibidas por Trueke</p>
                        <div class="mt-1 h-1.5 overflow-hidden rounded-full bg-superficie-2" role="progressbar" [attr.aria-valuenow]="n.recibidos"
                          aria-valuemin="0" [attr.aria-valuemax]="n.cantidad" [attr.aria-label]="'Progreso de ' + n.texto">
                          <div class="h-full rounded-full bg-bosque-500" [style.width.%]="(n.recibidos! / n.cantidad) * 100"></div>
                        </div>
                      </div>
                    } @else if (n.recibidos) {
                      <p class="mt-2 text-xs text-tenue">{{ n.recibidos | numero }} {{ n.recibidos === 1 ? 'donación recibida' : 'donaciones recibidas' }} por Trueke</p>
                    }
                  </div>
                  @if (!esMia() && !n.cubierta) {
                    <a routerLink="/publicar" [queryParams]="{ modo: 'Donacion', titulo: n.texto, categoria: n.categoriaId, fundacion: id(), necesidad: n.id }"
                      class="btn btn-primario btn-sm col-start-2 justify-self-start sm:col-start-auto"[attr.aria-label]="'Donar: ' + n.texto">
                      <app-icono nombre="regalo" [tamano]="14" />Tengo esto
                    </a>
                  }
                </li>
              }
            </ul>
          } @else if (fundacion.value()) {
            <p class="mt-6 rounded-tarjeta border-2 border-dashed border-borde p-8 text-center text-sm text-tenue">
              {{ esMia() ? 'Cuenta lo que necesitan: aparecerá aquí para toda la comunidad.' : 'La fundación aún no ha contado lo que necesita.' }}
              @if (esMia()) {
                <a routerLink="/cuenta/fundacion" class="enlace">Agregar necesidades</a>
              }
            </p>
          }
        </section>

        <section aria-labelledby="titulo-pub-fundacion" class="mt-14">
          <h2 id="titulo-pub-fundacion" class="text-xl font-bold">Lo que publica</h2>
          <div class="mt-5 grid grid-cols-2 gap-3 sm:gap-5 lg:grid-cols-3 xl:grid-cols-4">
            @if (publicaciones.isLoading()) {
              @for (i of [1, 2, 3]; track i) {
                <app-tarjeta-esqueleto />
              }
            } @else {
              @for (pub of publicaciones.value()?.items ?? []; track pub.id) {
                <app-tarjeta-publicacion [publicacion]="pub" />
              }
            }
          </div>
          @if (!publicaciones.isLoading() && !publicaciones.value()?.items?.length) {
            <p class="mt-4 text-sm text-tenue">No tiene publicaciones activas en este momento.</p>
          }
          <app-paginador [pagina]="pagina()" [total]="publicaciones.value()?.total ?? 0" [tamano]="12" (cambiar)="pagina.set($event)" />
        </section>
      </div>
    }
  `,
})
export default class Fundacion {
  private readonly api = inject(FundacionesApi);
  private readonly cuentas = inject(CuentaApi);
  private readonly titulo = inject(Title);
  private readonly sesion = inject(SesionService);
  readonly id = input.required<string>();
  protected readonly pagina = signal(1);

  protected readonly fundacion = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.obtener(params) });
  protected readonly publicaciones = rxResource({
    params: () => ({ id: this.id(), pagina: this.pagina() }),
    stream: ({ params }) => this.cuentas.publicacionesDe(params.id, params.pagina),
  });
  protected readonly necesidades = computed(() => this.fundacion.value()?.necesidades ?? []);
  protected readonly esMia = computed(() => this.sesion.usuario()?.id === this.id());

  protected mesYAnio(fecha: string | null | undefined): string {
    return fecha ? formatoMesAnio.format(Date.parse(fecha)) : '';
  }

  constructor() {
    effect(() => {
      const n = this.fundacion.value()?.nombre;
      if (n) this.titulo.setTitle(`${n} · Trueke Bogotá Solidario`);
    });
  }
}
