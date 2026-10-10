import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import {
  CODIGO_BOGOTA,
  CONDICIONES,
  INFO_MODO,
  LOCALIDADES,
  MODOS,
  ORDENES,
  type CondicionDto,
  type ModoDto,
  type OrdenPublicacionesDto,
  type PublicacionDto,
} from '../../api/tipos';
import { CatalogoApi, type FiltrosCatalogo } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Buscador } from '../../shared/ui/buscador';
import { Icono } from '../../shared/ui/icono';
import { Mapa, type PuntoMapa } from '../../shared/ui/mapa';
import { Modal } from '../../shared/ui/modal';
import { Paginador } from '../../shared/ui/paginador';
import { SelectorMunicipio } from '../../shared/ui/selector-municipio';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';

const TAMANO = 12;

@Component({
  imports: [
    NgTemplateOutlet,
    FormsModule,
    RouterLink,
    Icono,
    Buscador,
    TarjetaPublicacion,
    TarjetaEsqueleto,
    EstadoVacio,
    Paginador,
    Modal,
    Mapa,
    NumeroPipe,
    SelectorMunicipio,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="border-b border-borde bg-gradient-to-b from-bosque-50 to-fondo dark:from-bosque-950/60">
      <div class="contenedor py-8 sm:py-10">
        <h1 class="text-3xl font-extrabold sm:text-4xl">Explora el catálogo</h1>
        <p class="mt-2 text-tenue">Objetos de personas de todo Colombia esperando una segunda oportunidad. Filtra por tu ciudad para encontrar lo que está cerca.</p>

        <div class="mt-6 flex flex-col gap-3 lg:flex-row lg:items-center">
          <app-buscador
            class="block flex-1"
            [(texto)]="textoBusqueda"
            [grande]="true"
            claseEntrada="entrada rounded-full py-3 pl-12 text-base shadow-suave"
            (buscar)="aplicar({ texto: $event || null })"
          />
          <div class="flex gap-2 overflow-x-auto pb-1 lg:pb-0" role="group" aria-label="Filtrar por modo">
            <button type="button" class="btn shrink-0" [class]="!filtros().modo ? 'btn-primario' : 'btn-secundario'" (click)="aplicar({ modo: null })">
              Todos
            </button>
            @for (m of modos; track m) {
              <button
                type="button"
                class="btn shrink-0"
                [class]="filtros().modo === m ? 'btn-primario' : 'btn-secundario'"
                [attr.aria-pressed]="filtros().modo === m"
                (click)="aplicar({ modo: m })"
              >
                <app-icono [nombre]="info[m].icono" [tamano]="16" />{{ info[m].etiqueta }}
              </button>
            }
          </div>
        </div>
      </div>
    </section>

    <div class="contenedor grid grid-cols-1 gap-8 py-8 lg:grid-cols-[17rem_1fr]">
      <!-- Filtros (escritorio) -->
      <aside class="hidden lg:block" aria-label="Filtros">
        <div class="sticky top-24 tarjeta p-5">
          <ng-container *ngTemplateOutlet="panelFiltros" />
        </div>
      </aside>

      <section aria-live="polite">
        <div class="mb-5 flex flex-wrap items-center justify-between gap-3">
          <p class="text-sm text-tenue">
            @if (cercaDeMi()) {
              <strong class="text-tinta">{{ cercanas.value()?.length ?? 0 }}</strong> {{ (cercanas.value()?.length ?? 0) === 1 ? 'publicación' : 'publicaciones' }} a menos de {{ radioKm() }} km
            } @else {
              <strong class="text-tinta">{{ resultados.value()?.total ?? 0 | numero }}</strong> {{ resultados.value()?.total === 1 ? 'publicación' : 'publicaciones' }}
            }
            @if (filtrosActivos() > 0) {
              <button type="button" class="enlace ml-2 text-xs" (click)="limpiar()">Limpiar filtros</button>
            }
          </p>
          <div class="flex flex-wrap items-center gap-2">
            <button type="button" class="btn btn-secundario btn-sm lg:hidden" (click)="filtrosMovil.set(true)">
              <app-icono nombre="ajustes" [tamano]="16" /> Filtros
              @if (filtrosActivos() > 0) {
                <span class="grid size-5 place-items-center rounded-full bg-bosque-600 text-[10px] text-white">{{ filtrosActivos() }}</span>
              }
            </button>
            <button type="button" class="btn btn-sm" [class]="cercaDeMi() ? 'btn-primario' : 'btn-secundario'" (click)="alternarCercania()" [disabled]="ubicando()">
              <app-icono nombre="mira" [tamano]="16" /> {{ ubicando() ? 'Ubicando…' : 'Cerca de mí' }}
            </button>
            <div class="pestanas" role="group" aria-label="Vista">
              <button type="button" class="pestana flex items-center gap-1.5" [class.pestana-activa]="vista() === 'cuadricula'" (click)="vista.set('cuadricula')" [attr.aria-pressed]="vista() === 'cuadricula'">
                <app-icono nombre="cuadricula" [tamano]="16" /><span class="hidden sm:inline">Cuadrícula</span>
              </button>
              <button type="button" class="pestana flex items-center gap-1.5" [class.pestana-activa]="vista() === 'mapa'" (click)="vista.set('mapa')" [attr.aria-pressed]="vista() === 'mapa'">
                <app-icono nombre="mapa" [tamano]="16" /><span class="hidden sm:inline">Mapa</span>
              </button>
            </div>
          </div>
        </div>

        @if (cercaDeMi()) {
          <div class="mb-5 flex flex-wrap items-center gap-2 rounded-2xl bg-agua-50 p-3 text-sm dark:bg-agua-700/20">
            <app-icono nombre="pin" [tamano]="16" class="text-agua-700" />
            <span>Radio de búsqueda:</span>
            @for (r of radios; track r) {
              <button type="button" class="btn btn-sm" [class]="radioKm() === r ? 'btn-primario' : 'btn-secundario'" (click)="radioKm.set(r)">{{ r }} km</button>
            }
          </div>
        }

        @if (!cercaDeMi() && (filtros().pagina ?? 1) === 1 && (destacadas.value()?.length ?? 0) > 0) {
          <div class="mb-6 rounded-tarjeta border border-sol-200 bg-sol-50/60 p-4 dark:border-sol-500/30 dark:bg-sol-500/10">
            <p class="mb-3 flex items-center gap-2 text-sm font-bold"><app-icono nombre="destello" [tamano]="16" class="text-sol-600" />Destacadas</p>
            <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
              @for (p of destacadas.value(); track p.id) {
                <app-tarjeta-publicacion [publicacion]="p" />
              }
            </div>
          </div>
        }

        @if (vista() === 'mapa') {
          <app-mapa class="h-[60dvh] min-h-[22rem] rounded-tarjeta border border-borde shadow-suave sm:h-[32rem] lg:h-[40rem]" [puntos]="puntosMapa()" [centro]="miUbicacion()" [zoom]="miUbicacion() ? 14 : 12" etiqueta="Mapa de publicaciones" />
          @if (puntosMapa().length === 0 && !cargando()) {
            <p class="mt-3 text-center text-sm text-tenue">Ninguna publicación de esta búsqueda tiene ubicación en el mapa.</p>
          }
        } @else {
          <div class="grid grid-cols-2 gap-3 sm:gap-5 xl:grid-cols-3">
            @if (cargando()) {
              @for (i of esqueletos; track i) {
                <app-tarjeta-esqueleto />
              }
            } @else {
              @for (item of lista(); track item.publicacion.id) {
                <app-tarjeta-publicacion [publicacion]="item.publicacion" [distanciaKm]="item.distanciaKm" class="animate-aparecer" />
              }
            }
          </div>

          @if (!cargando() && lista().length === 0) {
            @if (errorCarga()) {
              <app-estado-vacio icono="alerta" titulo="No pudimos cargar el catálogo" descripcion="Revisa tu conexión e inténtalo de nuevo.">
                <button type="button" class="btn btn-primario" (click)="resultados.reload()">Reintentar</button>
              </app-estado-vacio>
            } @else {
              <app-estado-vacio icono="buscar" titulo="No encontramos publicaciones" descripcion="Prueba con otras palabras, quita algunos filtros o amplía el radio de búsqueda.">
                @if (filtrosActivos() > 0) {
                  <button type="button" class="btn btn-secundario" (click)="limpiar()">Limpiar filtros</button>
                }
                <a [routerLink]="sesion.autenticado() ? '/publicar' : '/registro'" class="btn btn-primario">Publicar lo que tengo</a>
              </app-estado-vacio>
            }
          }

          @if (!cercaDeMi()) {
            <app-paginador [pagina]="filtros().pagina ?? 1" [total]="resultados.value()?.total ?? 0" [tamano]="tamano" (cambiar)="irAPagina($event)" />
          }
        }
      </section>
    </div>

    <app-modal [(abierto)]="filtrosMovil" titulo="Filtros">
      <ng-container *ngTemplateOutlet="panelFiltros" />
      <div pie class="border-t border-borde p-4">
        <button type="button" class="btn btn-primario w-full" (click)="filtrosMovil.set(false)">
          Ver {{ resultados.value()?.total ?? 0 | numero }} resultados
        </button>
      </div>
    </app-modal>

    <ng-template #panelFiltros>
      <div class="space-y-5">
        <div class="campo">
          <label class="etiqueta" for="f-categoria">Categoría</label>
          <select id="f-categoria" class="entrada" [ngModel]="filtros().categoriaId ?? ''" (ngModelChange)="aplicar({ categoriaId: $event ? +$event : null })">
            <option value="">Todas las categorías</option>
            @for (c of categorias(); track c.id) {
              <option [value]="c.id">{{ c.nombre }}</option>
            }
          </select>
        </div>
        <app-selector-municipio id="f-ubicacion" [filtro]="true" [apilado]="true" [ngModel]="ubicacionFiltro()" (ngModelChange)="cambiarUbicacion($event)" />
        @if (filtros().municipioCodigo === codigoBogota) {
          <div class="campo">
            <label class="etiqueta" for="f-localidad">Localidad</label>
            <select id="f-localidad" class="entrada" [ngModel]="filtros().localidad ?? ''" (ngModelChange)="aplicar({ localidad: $event || null })">
              <option value="">Toda Bogotá</option>
              @for (l of localidades; track l) {
                <option [value]="l">{{ l }}</option>
              }
            </select>
          </div>
        }
        <div class="campo">
          <label class="etiqueta" for="f-condicion">Estado del producto</label>
          <select id="f-condicion" class="entrada" [ngModel]="filtros().condicion ?? ''" (ngModelChange)="aplicar({ condicion: $event || null })">
            <option value="">Cualquier estado</option>
            @for (c of condiciones; track c.valor) {
              <option [value]="c.valor">{{ c.etiqueta }}</option>
            }
          </select>
        </div>
        <div class="campo">
          <span class="etiqueta">Precio de referencia (COP)</span>
          <div class="grid grid-cols-2 gap-2">
            <input type="number" min="0" step="1000" class="entrada" placeholder="Mínimo" aria-label="Precio mínimo"
              [ngModel]="filtros().precioMin" (change)="aplicar({ precioMin: valorNumero($event) })" />
            <input type="number" min="0" step="1000" class="entrada" placeholder="Máximo" aria-label="Precio máximo"
              [ngModel]="filtros().precioMax" (change)="aplicar({ precioMax: valorNumero($event) })" />
          </div>
        </div>
        <div class="campo">
          <label class="etiqueta" for="f-orden">Ordenar por</label>
          <select id="f-orden" class="entrada" [ngModel]="filtros().orden" (ngModelChange)="aplicar({ orden: $event })">
            @for (o of ordenes; track o.valor) {
              <option [value]="o.valor">{{ o.etiqueta }}</option>
            }
          </select>
        </div>
        <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-borde p-3 transition hover:bg-superficie-2">
          <input type="checkbox" class="casilla mt-0.5" [ngModel]="filtros().soloVerificados" (ngModelChange)="aplicar({ soloVerificados: $event || null })" />
          <span>
            <span class="flex items-center gap-1 text-sm font-semibold"><app-icono nombre="verificado" [tamano]="16" class="text-agua-600" /> Solo cuentas verificadas</span>
            <span class="ayuda">Personas que validaron su identidad.</span>
          </span>
        </label>
      </div>
    </ng-template>
  `,
})
export default class Catalogo {
  private readonly api = inject(CatalogoApi);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);

  protected readonly info = INFO_MODO;
  protected readonly modos = MODOS;
  protected readonly ordenes = ORDENES;
  protected readonly localidades = LOCALIDADES;
  protected readonly condiciones = CONDICIONES;
  protected readonly codigoBogota = CODIGO_BOGOTA;
  protected readonly tamano = TAMANO;
  protected readonly radios = [2, 5, 10, 20];
  protected readonly esqueletos = [1, 2, 3, 4, 5, 6];

  protected readonly vista = signal<'cuadricula' | 'mapa'>('cuadricula');
  protected readonly filtrosMovil = signal(false);
  protected readonly textoBusqueda = signal('');
  protected readonly miUbicacion = signal<[number, number] | null>(null);
  protected readonly radioKm = signal(5);
  protected readonly ubicando = signal(false);
  protected readonly cercaDeMi = computed(() => this.miUbicacion() !== null);

  protected readonly categorias = toSignal(this.api.categorias$.pipe(catchError(() => of([]))), { initialValue: [] });
  private readonly parametros = toSignal(this.ruta.queryParamMap, { requireSync: true });

  /** Los filtros viven en la URL: se pueden compartir y sobreviven a recargar la página. */
  protected readonly filtros = computed<FiltrosCatalogo>(() => {
    const q = this.parametros();
    const num = (k: string) => (q.get(k) ? Number(q.get(k)) : null);
    const modo = q.get('modo');
    const condicion = q.get('condicion');
    const dep = q.get('departamentoCodigo');
    const mpio = q.get('municipioCodigo');
    return {
      texto: q.get('texto') ?? undefined,
      categoriaId: num('categoriaId'),
      modo: MODOS.includes(modo as ModoDto) ? (modo as ModoDto) : null,
      condicion: CONDICIONES.some((c) => c.valor === condicion) ? (condicion as CondicionDto) : null,
      departamentoCodigo: dep && /^\d{2}$/.test(dep) ? dep : null,
      municipioCodigo: mpio && /^\d{5}$/.test(mpio) ? mpio : null,
      localidad: q.get('localidad'),
      precioMin: num('precioMin'),
      precioMax: num('precioMax'),
      soloVerificados: q.get('soloVerificados') === 'true',
      orden: (q.get('orden') as OrdenPublicacionesDto) ?? 'Recientes',
      pagina: num('pagina') ?? 1,
      tamano: TAMANO,
    };
  });

  protected readonly filtrosActivos = computed(() => {
    const f = this.filtros();
    return [f.texto, f.categoriaId, f.modo, f.condicion, f.municipioCodigo ?? f.departamentoCodigo, f.localidad, f.precioMin, f.precioMax, f.soloVerificados || null].filter(
      (v) => v !== null && v !== undefined && v !== '',
    ).length;
  });

  /** Valor del selector de ubicación: municipio (5 dígitos), solo departamento (2) o vacío (toda Colombia). */
  protected readonly ubicacionFiltro = computed(() => this.filtros().municipioCodigo ?? this.filtros().departamentoCodigo ?? '');

  /** Vitrina: destacadas de la zona elegida (se muestran con cualquier orden). */
  protected readonly destacadas = rxResource({
    params: () => ({
      departamentoCodigo: this.filtros().departamentoCodigo,
      municipioCodigo: this.filtros().municipioCodigo,
      categoriaId: this.filtros().categoriaId,
    }),
    stream: ({ params }) => this.api.destacadas(params, 4).pipe(catchError(() => of([]))),
  });

  protected readonly resultados = rxResource({
    params: () => (this.cercaDeMi() ? undefined : this.filtros()),
    stream: ({ params }) => this.api.listar(params),
  });

  protected readonly cercanas = rxResource({
    params: () => {
      const u = this.miUbicacion();
      return u ? { u, radio: this.radioKm(), modo: this.filtros().modo, cat: this.filtros().categoriaId, cond: this.filtros().condicion } : undefined;
    },
    stream: ({ params }) => this.api.cercanas(params.u[0], params.u[1], params.radio, params.modo, params.cat, params.cond),
  });

  protected readonly cargando = computed(() =>
    this.cercaDeMi() ? this.cercanas.isLoading() : this.resultados.isLoading(),
  );
  protected readonly errorCarga = computed(() => (this.cercaDeMi() ? this.cercanas.error() : this.resultados.error()));

  protected readonly lista = computed<{ publicacion: PublicacionDto; distanciaKm: number | null }[]>(() =>
    this.cercaDeMi()
      ? (this.cercanas.value() ?? [])
          .filter((c) => c.publicacion)
          .map((c) => ({ publicacion: c.publicacion!, distanciaKm: c.distanciaKm ?? null }))
      : (this.resultados.value()?.items ?? []).map((p) => ({ publicacion: p, distanciaKm: null })),
  );

  protected readonly puntosMapa = computed<PuntoMapa[]>(() =>
    this.lista()
      .map((i) => i.publicacion)
      .filter((p) => p.latitud != null && p.longitud != null)
      .map((p) => ({
        id: p.id!,
        lat: p.latitud!,
        lon: p.longitud!,
        titulo: p.titulo ?? '',
        modo: p.modo,
        detalle: `${INFO_MODO[p.modo as ModoDto]?.etiqueta ?? ''} · ${p.localidad ?? ''}`,
      })),
  );

  constructor() {
    effect(() => {
      const texto = this.filtros().texto ?? '';
      untracked(() => this.textoBusqueda.set(texto));
    });
  }

  protected aplicar(cambios: Record<string, string | number | boolean | null>): void {
    void this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: { ...cambios, pagina: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected cambiarUbicacion(valor: string | null): void {
    const v = valor ?? '';
    this.aplicar({
      municipioCodigo: /^\d{5}$/.test(v) ? v : null,
      departamentoCodigo: /^\d{2}$/.test(v) ? v : null,
      localidad: null,
    });
  }

  protected irAPagina(pagina: number): void {
    void this.router.navigate([], { relativeTo: this.ruta, queryParams: { pagina }, queryParamsHandling: 'merge' });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected limpiar(): void {
    void this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
  }

  protected valorNumero(e: Event): number | null {
    const v = (e.target as HTMLInputElement).value;
    return v === '' ? null : Math.max(0, Number(v));
  }

  protected alternarCercania(): void {
    if (this.cercaDeMi()) {
      this.miUbicacion.set(null);
      return;
    }
    if (!('geolocation' in navigator)) {
      this.avisos.error('Tu navegador no permite obtener la ubicación.');
      return;
    }
    this.ubicando.set(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.ubicando.set(false);
        this.miUbicacion.set([pos.coords.latitude, pos.coords.longitude]);
      },
      () => {
        this.ubicando.set(false);
        this.avisos.error('No pudimos obtener tu ubicación', 'Revisa los permisos de ubicación del navegador.');
      },
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 300_000 },
    );
  }
}
