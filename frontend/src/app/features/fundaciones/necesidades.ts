import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import type { FundacionResumenDto, NecesidadDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { InsigniaRegimen } from '../../shared/ui/insignia-regimen';

/** Sin tildes ni mayúsculas, para buscar «cuaderno» y encontrar «Cuadernos». */
const normalizar = (t: string) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

interface Pedido {
  necesidad: NecesidadDto;
  fundacion: FundacionResumenDto;
  /** Texto donde se busca: la necesidad, su categoría y la fundación. */
  busqueda: string;
}

/**
 * Público: todo lo que piden las fundaciones verificadas, en una sola lista (las urgentes primero). Sale del mismo listado de
 * fundaciones (en caché), así que filtrar no hace más peticiones.
 */
@Component({
  imports: [RouterLink, Avatar, EstadoVacio, Icono, InsigniaRegimen, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-12">
      <div class="max-w-2xl">
        <h1 class="text-3xl font-extrabold text-balance sm:text-4xl">Lo que piden las fundaciones</h1>
        <p class="mt-3 text-lg text-tenue">
          Lo que necesitan las fundaciones verificadas de la comunidad. Si tienes algo de esto, publícalo como donación: la fundación
          recibe un aviso y te lo pide por aquí.
        </p>
      </div>

      <form class="mt-8 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_14rem_14rem_auto] lg:items-end" (submit)="$event.preventDefault()"
        aria-label="Filtrar lo que piden">
        <div class="campo">
          <label for="buscar-pedido" class="etiqueta">Buscar</label>
          <input id="buscar-pedido" type="search" class="entrada" autocomplete="off" placeholder="Por ejemplo: cuadernos"
            [value]="texto()" (input)="texto.set($any($event.target).value)" />
        </div>
        <div class="campo">
          <label for="pedido-categoria" class="etiqueta">Categoría</label>
          <select id="pedido-categoria" class="entrada" [value]="categoria()" (change)="categoria.set($any($event.target).value)">
            <option value="">Todas</option>
            @for (c of categorias(); track c.id) {
              <option [value]="c.id">{{ c.nombre }}</option>
            }
          </select>
        </div>
        <div class="campo">
          <label for="pedido-zona" class="etiqueta">Zona de la fundación</label>
          <select id="pedido-zona" class="entrada" [value]="zona()" (change)="zona.set($any($event.target).value)">
            <option value="">Todas</option>
            @for (z of zonas(); track z) {
              <option [value]="z">{{ z }}</option>
            }
          </select>
        </div>
        <label class="flex cursor-pointer items-center gap-2 pb-3 text-sm font-semibold">
          <input type="checkbox" class="size-4 accent-bosque-600" [checked]="soloUrgentes()" (change)="soloUrgentes.set($any($event.target).checked)" />
          Solo urgentes
        </label>
      </form>

      @if (recurso.isLoading()) {
        <ul class="mt-8 space-y-3" aria-hidden="true">
          @for (i of [1, 2, 3]; track i) {
            <li class="tarjeta h-24 p-5"><div class="esqueleto h-full w-full rounded-2xl"></div></li>
          }
        </ul>
      } @else if (recurso.error()) {
        <app-estado-vacio icono="alerta" titulo="No pudimos cargar lo que piden" descripcion="Revisa tu conexión e inténtalo de nuevo.">
          <button type="button" class="btn btn-primario" (click)="recurso.reload()">Reintentar</button>
        </app-estado-vacio>
      } @else if (!visibles().length) {
        <app-estado-vacio icono="heart" [titulo]="filtrado() ? 'Nada coincide con los filtros' : 'Por ahora las fundaciones no piden nada'"
          [descripcion]="filtrado() ? 'Prueba con otra búsqueda o quita algún filtro.' : 'Vuelve pronto o mira las fundaciones de la comunidad.'">
          <a routerLink="/fundaciones" class="btn btn-secundario">Ver las fundaciones</a>
        </app-estado-vacio>
      } @else {
        <p class="mt-8 text-sm text-tenue" aria-live="polite">{{ visibles().length | numero }} {{ visibles().length === 1 ? 'pedido' : 'pedidos' }}</p>
        <ul class="mt-3 divide-y divide-borde overflow-hidden rounded-tarjeta border border-borde bg-superficie">
          @for (p of visibles(); track p.necesidad.id) {
            <li class="grid grid-cols-1 gap-4 p-4 sm:grid-cols-[1fr_auto] sm:items-center sm:p-5" [class]="p.necesidad.urgente ? 'bg-sol-50 dark:bg-sol-500/10' : ''">
              <div class="min-w-0">
                <p class="text-lg leading-snug font-bold break-words">
                  @if (p.necesidad.cantidad) { <span class="tabular-nums">{{ p.necesidad.cantidad | numero }}</span> · }{{ p.necesidad.texto }}
                </p>
                <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-tenue">
                  @if (p.necesidad.urgente) {
                    <span class="insignia-sol"><app-icono nombre="reloj" [tamano]="12" />Urgente</span>
                  }
                  @if (p.necesidad.categoria) { <span>{{ p.necesidad.categoria }}</span> }
                  @if (p.necesidad.cantidad && p.necesidad.recibidos) {
                    <span class="tabular-nums">· {{ p.necesidad.recibidos | numero }} de {{ p.necesidad.cantidad | numero }} recibidas</span>
                  }
                </p>
                <a [routerLink]="['/fundaciones', p.fundacion.id]" class="mt-3 flex items-center gap-2 text-sm hover:underline">
                  <app-avatar [nombre]="p.fundacion.nombre" [foto]="p.fundacion.fotoUrl" [tamano]="28" />
                  <span class="min-w-0 truncate font-semibold">{{ p.fundacion.nombre }}</span>
                  <span class="shrink-0 text-tenue">· {{ p.fundacion.localidad }}</span>
                </a>
                @if (p.fundacion.regimenTributarioEspecial) {
                  <div class="mt-2"><app-insignia-regimen /></div>
                }
              </div>
              @if (sesion.usuario()?.id !== p.fundacion.id) {
                <a routerLink="/publicar"
                  [queryParams]="{ modo: 'Donacion', titulo: p.necesidad.texto, categoria: p.necesidad.categoriaId, fundacion: p.fundacion.id, necesidad: p.necesidad.id }"
                  class="btn btn-primario btn-sm justify-self-start" [attr.aria-label]="'Donar a ' + p.fundacion.nombre + ': ' + p.necesidad.texto">
                  <app-icono nombre="regalo" [tamano]="14" />Tengo esto
                </a>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export default class Necesidades {
  private readonly api = inject(FundacionesApi);
  protected readonly sesion = inject(SesionService);
  protected readonly recurso = rxResource({ stream: () => this.api.listar() });
  protected readonly categorias = toSignal(inject(CatalogoApi).categorias$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly texto = signal('');
  protected readonly categoria = signal('');
  protected readonly zona = signal('');
  protected readonly soloUrgentes = signal(false);
  protected readonly filtrado = computed(() => !!(this.texto() || this.categoria() || this.zona() || this.soloUrgentes()));

  private readonly pedidos = computed<Pedido[]>(() =>
    (this.recurso.value() ?? [])
      .flatMap((f) => (f.abiertas ?? []).map((n) => ({
        necesidad: n,
        fundacion: f,
        busqueda: normalizar(`${n.texto} ${n.categoria ?? ''} ${f.nombre}`),
      })))
      .sort((a, b) => Number(!!b.necesidad.urgente) - Number(!!a.necesidad.urgente)
        || (b.necesidad.fechaUtc ?? '').localeCompare(a.necesidad.fechaUtc ?? '')),
  );
  protected readonly zonas = computed(() => [...new Set((this.recurso.value() ?? []).filter((f) => f.abiertas?.length).map((f) => f.localidad ?? ''))]
    .filter(Boolean).sort((a, b) => a.localeCompare(b, 'es')));
  protected readonly visibles = computed(() => {
    const t = normalizar(this.texto());
    const cat = this.categoria() ? Number(this.categoria()) : null;
    const zona = this.zona();
    return this.pedidos().filter((p) =>
      (!t || p.busqueda.includes(t)) && (cat === null || p.necesidad.categoriaId === cat)
      && (!zona || p.fundacion.localidad === zona) && (!this.soloUrgentes() || p.necesidad.urgente));
  });
}
