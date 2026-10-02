import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { FechaPipe, HacePipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Denunciar } from '../../shared/ui/denunciar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Icono } from '../../shared/ui/icono';
import { Paginador } from '../../shared/ui/paginador';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';

@Component({
  imports: [RouterLink, Avatar, Estrellas, Icono, EstadoVacio, Paginador, TarjetaPublicacion, TarjetaEsqueleto, Denunciar, FechaPipe, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (perfil.error()) {
      <app-estado-vacio icono="usuario" titulo="Este perfil no está disponible" descripcion="Puede que la cuenta ya no exista.">
        <a routerLink="/explorar" class="btn btn-primario">Ir al catálogo</a>
      </app-estado-vacio>
    } @else {
      <section class="bg-gradient-to-b from-bosque-100 to-fondo dark:from-bosque-950/70">
        <div class="contenedor flex flex-col items-center gap-6 py-10 text-center sm:flex-row sm:text-left">
          @if (perfil.value(); as p) {
            <app-avatar [nombre]="p.nombre" [tamano]="104" [verificado]="!!p.verificado" />
            <div class="flex-1">
              <div class="flex flex-wrap items-center justify-center gap-2 sm:justify-start">
                <h1 class="text-3xl font-extrabold">{{ p.nombre }}</h1>
                @if (p.verificado) {
                  <span class="insignia-agua"><app-icono nombre="verificado" [tamano]="12" />Verificada</span>
                }
                @if (p.tipoCuenta && p.tipoCuenta !== 'Individual') {
                  <span class="insignia-sol"><app-icono nombre="corona" [tamano]="12" />{{ p.tipoCuenta }}</span>
                }
              </div>
              <p class="mt-1 flex items-center justify-center gap-3 text-sm text-tenue sm:justify-start">
                <span class="flex items-center gap-1"><app-icono nombre="pin" [tamano]="14" />{{ p.localidad }}</span>
                <span>Miembro desde {{ p.miembroDesde | fecha }}</span>
              </p>
              @if (p.calificacionPromedio) {
                <p class="mt-2 flex items-center justify-center gap-2 sm:justify-start">
                  <app-estrellas [valor]="p.calificacionPromedio" [tamano]="18" />
                  <span class="text-sm font-semibold">{{ p.calificacionPromedio.toFixed(1) }}</span>
                  <span class="text-sm text-tenue">({{ p.totalCalificaciones }} calificaciones)</span>
                </p>
              }
            </div>
            <dl class="grid grid-cols-4 gap-2 text-center">
              @for (d of datos(); track d.texto) {
                <div class="min-w-20 rounded-2xl bg-superficie p-3 shadow-suave">
                  <dd class="font-display text-2xl font-extrabold">{{ d.valor }}</dd>
                  <dt class="text-[11px] leading-tight text-tenue">{{ d.texto }}</dt>
                </div>
              }
            </dl>
          } @else {
            <div class="esqueleto size-26 rounded-full"></div>
            <div class="flex-1 space-y-3"><div class="esqueleto h-8 w-60"></div><div class="esqueleto h-4 w-40"></div></div>
          }
        </div>
      </section>

      <div class="contenedor grid gap-10 py-10 lg:grid-cols-[1fr_22rem]">
        <section aria-labelledby="titulo-publicaciones">
          <h2 id="titulo-publicaciones" class="text-xl font-bold">Publicaciones activas</h2>
          <div class="mt-5 grid grid-cols-2 gap-3 sm:gap-5 xl:grid-cols-3">
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
            <p class="mt-4 rounded-tarjeta border-2 border-dashed border-borde p-8 text-center text-sm text-tenue">No tiene publicaciones activas en este momento.</p>
          }
          <app-paginador [pagina]="paginaPub()" [total]="publicaciones.value()?.total ?? 0" [tamano]="12" (cambiar)="paginaPub.set($event)" />
        </section>

        <aside aria-labelledby="titulo-calificaciones">
          <h2 id="titulo-calificaciones" class="text-xl font-bold">Lo que dice la comunidad</h2>
          <ul class="mt-5 space-y-3">
            @for (c of calificaciones.value()?.items ?? []; track c.id) {
              <li class="tarjeta p-4">
                <div class="flex items-center gap-3">
                  <app-avatar [nombre]="c.autor?.nombre" [tamano]="32" />
                  <div class="min-w-0 flex-1">
                    <p class="truncate text-sm font-semibold">{{ c.autor?.nombre }}</p>
                    <app-estrellas [valor]="c.estrellas ?? 0" [tamano]="12" />
                  </div>
                  <span class="text-xs text-tenue">{{ c.fechaUtc | hace }}</span>
                </div>
                @if (c.comentario) {
                  <p class="mt-2 text-sm break-words text-tinta/85">{{ c.comentario }}</p>
                }
              </li>
            } @empty {
              @if (!calificaciones.isLoading()) {
                <li class="rounded-tarjeta border-2 border-dashed border-borde p-6 text-center text-sm text-tenue">Aún no tiene calificaciones.</li>
              }
            }
          </ul>
          @if ((calificaciones.value()?.total ?? 0) > (calificaciones.value()?.items?.length ?? 0)) {
            <button type="button" class="btn btn-secundario btn-sm mt-4 w-full" (click)="paginaCal.set(paginaCal() + 1)">Ver más</button>
          }
          @if (sesion.autenticado() && sesion.usuario()?.id !== id()) {
            <button type="button" class="mt-6 flex items-center gap-1 text-xs text-tenue hover:text-tierra-600" (click)="denunciaAbierta.set(true)">
              <app-icono nombre="bandera" [tamano]="14" />Reportar este perfil
            </button>
            <app-denunciar [(abierto)]="denunciaAbierta" tipo="Usuario" [objetivoId]="id()" />
          }
        </aside>
      </div>
    }
  `,
})
export default class PerfilPublico {
  private readonly api = inject(CuentaApi);
  private readonly titulo = inject(Title);
  protected readonly sesion = inject(SesionService);
  readonly id = input.required<string>();
  protected readonly paginaPub = signal(1);
  protected readonly paginaCal = signal(1);
  protected readonly denunciaAbierta = signal(false);

  protected readonly perfil = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.perfil(params) });
  protected readonly publicaciones = rxResource({
    params: () => ({ id: this.id(), pagina: this.paginaPub() }),
    stream: ({ params }) => this.api.publicacionesDe(params.id, params.pagina, 12),
  });
  protected readonly calificaciones = rxResource({
    params: () => ({ id: this.id(), tamano: 5 * this.paginaCal() }),
    stream: ({ params }) => this.api.calificacionesDe(params.id, 1, params.tamano),
  });

  protected readonly datos = computed(() => {
    const p = this.perfil.value();
    return [
      { texto: 'Truekes', valor: p?.truekesCompletados ?? 0 },
      { texto: 'Ventas y compras', valor: p?.comprasRealizadas ?? 0 },
      { texto: 'Donaciones', valor: p?.donacionesRealizadas ?? 0 },
      { texto: 'Reputación', valor: (p?.reputacion ?? 0).toFixed(1) },
    ];
  });

  constructor() {
    effect(() => {
      const n = this.perfil.value()?.nombre;
      if (n) this.titulo.setTitle(`${n} · Trueke Bogotá Solidario`);
    });
  }
}
