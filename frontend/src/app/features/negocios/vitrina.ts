import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { NegociosApi } from '../../core/api/negocios.api';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Icono } from '../../shared/ui/icono';
import { Paginador } from '../../shared/ui/paginador';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';
import { Volver } from '../../shared/ui/volver';

const TAMANO = 12;

/** Vitrina pública de un negocio: quién es, su horario y su catálogo con búsqueda (las publicaciones visibles de la cuenta). */
@Component({
  imports: [RouterLink, Volver, Avatar, Estrellas, Icono, EstadoVacio, Paginador, TarjetaPublicacion, TarjetaEsqueleto, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (negocio.error()) {
      <div class="contenedor py-16">
        <app-estado-vacio icono="edificio" titulo="Este negocio no está disponible" descripcion="Puede que su plan haya terminado.">
          <a routerLink="/negocios" class="btn btn-primario">Ver los negocios</a>
        </app-estado-vacio>
      </div>
    } @else {
      <section class="border-b border-borde bg-superficie">
        <div class="contenedor pt-4"><app-volver respaldo="/negocios" texto="Negocios" /></div>
        <div class="contenedor flex flex-col gap-6 pt-4 pb-10 sm:flex-row sm:items-start">
          @if (negocio.value(); as n) {
            <app-avatar [nombre]="n.nombre" [foto]="n.fotoUrl" [tamano]="104" />
            <div class="min-w-0 flex-1">
              @if (n.verificado) {
                <span class="insignia-trueke" title="Revisamos su NIT con el registro mercantil (RUES)"><app-icono nombre="verificado" [tamano]="12" />Negocio verificado</span>
              }
              <h1 class="mt-2 text-3xl font-extrabold text-balance sm:text-4xl">{{ n.nombre }}</h1>
              <p class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-tenue">
                <span class="flex items-center gap-1"><app-icono nombre="pin" [tamano]="14" />{{ n.localidad }}{{ n.municipio ? ', ' + n.municipio : '' }}</span>
                @if (n.horario) {
                  <span class="flex items-center gap-1"><app-icono nombre="reloj" [tamano]="14" />{{ n.horario }}</span>
                }
                @if (n.nit) {
                  <span>NIT {{ n.nit }}</span>
                }
              </p>
              @if (n.descripcion) {
                <p class="mt-4 max-w-prose whitespace-pre-line text-tinta/90">{{ n.descripcion }}</p>
              }
              <div class="mt-5 flex flex-wrap items-center gap-3">
                @if (n.calificacionPromedio) {
                  <span class="flex items-center gap-2 text-sm">
                    <app-estrellas [valor]="n.calificacionPromedio" [tamano]="16" />
                    <span class="font-semibold">{{ n.calificacionPromedio.toFixed(1) }}</span>
                    <span class="text-tenue">({{ n.totalCalificaciones | numero }})</span>
                  </span>
                }
                <a [routerLink]="['/usuarios', n.id]" class="enlace text-sm font-semibold">Ver su perfil y calificaciones</a>
              </div>
            </div>
          } @else {
            <div class="esqueleto size-26 rounded-full"></div>
            <div class="flex-1 space-y-3"><div class="esqueleto h-9 w-72"></div><div class="esqueleto h-4 w-48"></div><div class="esqueleto h-16 w-full max-w-prose"></div></div>
          }
        </div>
      </section>

      <div class="contenedor py-10">
        <section aria-labelledby="titulo-catalogo">
          <div class="flex flex-wrap items-end justify-between gap-4">
            <h2 id="titulo-catalogo" class="text-2xl font-extrabold">Su catálogo
              @if (publicaciones.value(); as p) { <span class="text-base font-normal text-tenue">· {{ p.total | numero }}</span> }
            </h2>
            <div class="campo w-full sm:w-72">
              <label for="buscar-vitrina" class="sr-only">Buscar en el catálogo</label>
              <input id="buscar-vitrina" type="search" class="entrada" autocomplete="off" placeholder="Buscar en su catálogo"
                [value]="texto()" (input)="buscar($any($event.target).value)" />
            </div>
          </div>
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
            <p class="mt-4 text-sm text-tenue">{{ texto() ? 'Nada coincide con tu búsqueda.' : 'No tiene publicaciones activas en este momento.' }}</p>
          }
          <app-paginador [pagina]="pagina()" [total]="publicaciones.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />
        </section>
      </div>
    }
  `,
})
export default class Vitrina {
  private readonly api = inject(NegociosApi);
  private readonly catalogo = inject(CatalogoApi);
  private readonly titulo = inject(Title);
  readonly id = input.required<string>();
  protected readonly tamano = TAMANO;
  protected readonly texto = signal('');
  protected readonly pagina = signal(1);
  private espera: ReturnType<typeof setTimeout> | undefined;

  protected readonly negocio = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.obtener(params) });
  protected readonly publicaciones = rxResource({
    params: () => ({ id: this.id(), texto: this.texto(), pagina: this.pagina() }),
    stream: ({ params: p }) => this.catalogo.listar({ propietarioId: p.id, texto: p.texto || undefined, pagina: p.pagina, tamano: TAMANO }),
  });

  constructor() {
    effect(() => {
      const n = this.negocio.value()?.nombre;
      if (n) this.titulo.setTitle(`${n} · Trueke Bogotá Solidario`);
    });
  }

  /** Espera a que la persona deje de escribir para no pedir una página por cada letra. */
  protected buscar(valor: string): void {
    clearTimeout(this.espera);
    this.espera = setTimeout(() => {
      this.pagina.set(1);
      this.texto.set(valor.trim());
    }, 300);
  }
}
