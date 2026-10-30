import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NegociosApi } from '../../core/api/negocios.api';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';

/** Sin tildes ni mayúsculas, para buscar «muebles» y encontrar «Muebles». */
const normalizar = (t: string) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

/** Público: negocios de la comunidad con el plan Negocio (los verificados primero). Se filtra en el navegador. */
@Component({
  imports: [RouterLink, Avatar, EstadoVacio, Icono, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-12">
      <div class="grid grid-cols-1 items-end gap-6 lg:grid-cols-[1fr_20rem]">
        <div class="max-w-2xl">
          <h1 class="text-3xl font-extrabold text-balance sm:text-4xl">Negocios de la comunidad</h1>
          <p class="mt-3 text-lg text-tenue">
            Tiendas y emprendimientos que venden e intercambian en Trueke. El sello «Negocio verificado» significa que revisamos su
            NIT con el registro mercantil.
          </p>
        </div>
        <div class="campo">
          <label for="buscar-negocio" class="etiqueta">Buscar por nombre, barrio o lo que venden</label>
          <input id="buscar-negocio" type="search" class="entrada" autocomplete="off" placeholder="Por ejemplo: muebles"
            [value]="filtro()" (input)="filtro.set($any($event.target).value)" />
        </div>
      </div>

      @if (recurso.isLoading()) {
        <ul class="mt-10 grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3" aria-hidden="true">
          @for (i of [1, 2, 3]; track i) {
            <li class="tarjeta h-48 p-5"><div class="esqueleto h-full w-full rounded-2xl"></div></li>
          }
        </ul>
      } @else if (recurso.error()) {
        <app-estado-vacio icono="alerta" titulo="No pudimos cargar los negocios" descripcion="Revisa tu conexión e inténtalo de nuevo.">
          <button type="button" class="btn btn-primario" (click)="recurso.reload()">Reintentar</button>
        </app-estado-vacio>
      } @else if (!visibles().length) {
        <app-estado-vacio icono="edificio" [titulo]="filtro() ? 'Ningún negocio coincide' : 'Aún no hay negocios en la comunidad'"
          [descripcion]="filtro() ? 'Prueba con otro nombre o barrio.' : 'Con el plan Negocio tu tienda tiene vitrina propia.'">
          @if (!filtro()) {
            <a routerLink="/planes" class="btn btn-primario">Conocer el plan Negocio</a>
          }
        </app-estado-vacio>
      } @else {
        <ul class="mt-10 grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          @for (n of visibles(); track n.id) {
            <li>
              <a [routerLink]="['/negocios', n.id]" class="tarjeta-interactiva flex h-full flex-col gap-4 p-5">
                <div class="flex items-center gap-3">
                  <app-avatar [nombre]="n.nombre" [foto]="n.fotoUrl" [tamano]="52" />
                  <div class="min-w-0">
                    <h2 class="line-clamp-2 font-display text-lg leading-snug font-bold">{{ n.nombre }}</h2>
                    <p class="flex items-center gap-1 truncate text-sm text-tenue"><app-icono nombre="pin" [tamano]="14" />{{ n.localidad }}</p>
                  </div>
                </div>
                @if (n.verificado) {
                  <span class="insignia-trueke self-start"><app-icono nombre="verificado" [tamano]="12" />Negocio verificado</span>
                }
                @if (n.descripcion) {
                  <p class="line-clamp-3 text-sm text-tinta/85">{{ n.descripcion }}</p>
                }
                <div class="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-borde pt-3 text-sm">
                  <span class="font-semibold">{{ n.publicaciones | numero }} {{ n.publicaciones === 1 ? 'publicación' : 'publicaciones' }}</span>
                  @if (n.horario) {
                    <span class="flex min-w-0 items-center gap-1 text-tenue"><app-icono nombre="reloj" [tamano]="12" class="shrink-0" /><span class="truncate">{{ n.horario }}</span></span>
                  }
                </div>
              </a>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export default class Negocios {
  private readonly api = inject(NegociosApi);
  protected readonly recurso = rxResource({ stream: () => this.api.listar() });
  protected readonly filtro = signal('');
  protected readonly visibles = computed(() => {
    const lista = this.recurso.value() ?? [];
    const f = normalizar(this.filtro());
    return f ? lista.filter((x) => normalizar(`${x.nombre} ${x.localidad} ${x.municipio} ${x.descripcion ?? ''}`).includes(f)) : lista;
  });
}
