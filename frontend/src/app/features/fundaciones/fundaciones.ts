import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { SesionService } from '../../core/sesion.service';
import { Avatar } from '../../shared/ui/avatar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { InsigniaFundacion } from '../../shared/ui/insignia-fundacion';

/** Sin tildes ni mayúsculas, para buscar «bosa» y encontrar «Bosa». */
const normalizar = (t: string) => t.normalize('NFD').replace(/[\u0300-\u036F]/g, '').toLowerCase().trim();

/** Público: fundaciones verificadas de la comunidad, con cuántas cosas necesitan. Se filtra en el navegador (son pocas). */
@Component({
  imports: [RouterLink, Avatar, EstadoVacio, Icono, InsigniaFundacion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-12">
      <div class="grid grid-cols-1 items-end gap-6 lg:grid-cols-[1fr_20rem]">
        <div class="max-w-2xl">
          <h1 class="text-3xl font-extrabold sm:text-4xl">Fundaciones de la comunidad</h1>
          <p class="mt-3 text-lg text-tenue">
            Organizaciones sin ánimo de lucro que verificamos con su NIT y su certificado de la Cámara de Comercio.
            Mira lo que necesitan y dona lo que ya no usas.
          </p>
        </div>
        <div class="campo">
          <label for="buscar-fundacion" class="etiqueta">Buscar por nombre o barrio</label>
          <input id="buscar-fundacion" type="search" class="entrada" autocomplete="off" placeholder="Por ejemplo: Bosa"
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
        <app-estado-vacio icono="alerta" titulo="No pudimos cargar las fundaciones" descripcion="Revisa tu conexión e inténtalo de nuevo.">
          <button type="button" class="btn btn-primario" (click)="recurso.reload()">Reintentar</button>
        </app-estado-vacio>
      } @else if (!visibles().length) {
        <app-estado-vacio icono="heart" [titulo]="filtro() ? 'Ninguna fundación coincide' : 'Aún no hay fundaciones verificadas'"
          [descripcion]="filtro() ? 'Prueba con otro nombre o barrio.' : 'Si trabajas en una, pide la verificación: es gratis.'">
          @if (!filtro()) {
            <a [routerLink]="sesion.autenticado() ? '/cuenta/fundacion' : '/registro'" class="btn btn-primario">Verificar mi fundación</a>
          }
        </app-estado-vacio>
      } @else {
        <ul class="mt-10 grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          @for (f of visibles(); track f.id) {
            <li>
              <a [routerLink]="['/fundaciones', f.id]" class="tarjeta-interactiva flex h-full flex-col gap-4 p-5">
                <div class="flex items-center gap-3">
                  <app-avatar [nombre]="f.nombre" [foto]="f.fotoUrl" [tamano]="52" />
                  <div class="min-w-0">
                    <h2 class="line-clamp-2 font-display text-lg leading-snug font-bold">{{ f.nombre }}</h2>
                    <p class="flex items-center gap-1 truncate text-sm text-tenue"><app-icono nombre="pin" [tamano]="14" />{{ f.localidad }}</p>
                  </div>
                </div>
                <p class="line-clamp-3 text-sm text-tinta/85">{{ f.descripcion }}</p>
                <div class="mt-auto flex flex-wrap items-center gap-2 border-t border-borde pt-3 text-sm">
                  @if (f.necesidades) {
                    <span class="font-semibold">{{ f.necesidades }} {{ f.necesidades === 1 ? 'cosa que necesita' : 'cosas que necesita' }}</span>
                    @if (f.necesidadesUrgentes) {
                      <span class="insignia-sol"><app-icono nombre="reloj" [tamano]="12" />{{ f.necesidadesUrgentes }} {{ f.necesidadesUrgentes === 1 ? 'urgente' : 'urgentes' }}</span>
                    }
                  } @else {
                    <span class="text-tenue">Aún no ha publicado lo que necesita</span>
                  }
                </div>
              </a>
            </li>
          }
        </ul>
      }

      <aside class="mt-14 flex flex-col items-start gap-4 rounded-tarjeta border border-tierra-500/25 bg-tierra-50 p-6 sm:flex-row sm:items-center dark:bg-tierra-700/15">
        <app-insignia-fundacion />
        <p class="flex-1 text-sm">
          <strong>¿Trabajas en una fundación?</strong> Pide la verificación con el NIT y el certificado de existencia de la Cámara de
          Comercio. Es gratis y te da una página para contar lo que necesitan.
        </p>
        <a [routerLink]="sesion.autenticado() ? '/cuenta/fundacion' : '/registro'" class="btn btn-secundario btn-sm">Verificar mi fundación</a>
      </aside>
    </div>
  `,
})
export default class Fundaciones {
  private readonly api = inject(FundacionesApi);
  protected readonly sesion = inject(SesionService);
  protected readonly recurso = rxResource({ stream: () => this.api.listar() });
  protected readonly filtro = signal('');
  protected readonly visibles = computed(() => {
    const lista = this.recurso.value() ?? [];
    const f = normalizar(this.filtro());
    return f ? lista.filter((x) => normalizar(`${x.nombre} ${x.localidad} ${x.municipio}`).includes(f)) : lista;
  });
}
