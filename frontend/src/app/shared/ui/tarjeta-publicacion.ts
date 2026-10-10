import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { infoCondicion, type PublicacionDto } from '../../api/tipos';
import { FavoritosService } from '../../core/favoritos.service';
import { CopPipe, HacePipe } from '../pipes';
import { Icono } from './icono';
import { ImagenPublicacion } from './imagen-publicacion';
import { InsigniaModo } from './insignia-modo';

@Component({
  selector: 'app-tarjeta-publicacion',
  imports: [RouterLink, Icono, ImagenPublicacion, InsigniaModo, CopPipe, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @let p = publicacion();
    <article class="group tarjeta-interactiva relative flex h-full flex-col overflow-hidden">
      <a [routerLink]="['/publicacion', p.id]" class="absolute inset-0 z-10" [attr.aria-label]="'Ver ' + p.titulo"></a>
      <div class="relative aspect-[4/3]">
        <app-imagen-publicacion
          class="size-full"
          [src]="p.imagenes?.[0]"
          [miniatura]="true"
          [alt]="p.titulo ?? ''"
          [modo]="p.modo"
          [categoriaId]="p.categoria?.id"
          claseImagen="group-hover:scale-105"
        />
        <div class="absolute top-3 left-3 flex flex-wrap gap-1.5">
          <app-insignia-modo [modo]="p.modo" class="shadow-sm" />
          @if (p.destacada) {
            <span class="insignia-sol shadow-sm"><app-icono nombre="destello" [tamano]="12" />Destacada</span>
          }
        </div>
        @if (!p.esMia) {
          <button
            type="button"
            class="absolute top-2.5 right-2.5 z-20 grid size-9 place-items-center rounded-full bg-white/90 shadow-md backdrop-blur transition hover:scale-110 dark:bg-bosque-950/80"
            [class]="favorita() ? 'text-tierra-600' : 'text-tenue'"
            [attr.aria-pressed]="favorita()"
            [attr.aria-label]="favorita() ? 'Quitar de favoritos' : 'Guardar en favoritos'"
            (click)="favoritos.alternar(p.id!, favorita())"
          >
            <app-icono nombre="heart" [tamano]="18" [relleno]="favorita()" />
          </button>
        }
        @if (p.estado && p.estado !== 'Disponible') {
          <span class="absolute right-3 bottom-3 insignia bg-bosque-950/80 text-white backdrop-blur">
            {{ p.estado === 'EnNegociacion' ? 'Reservada' : p.estado === 'Intercambiada' ? 'Intercambiada' : 'No disponible' }}
          </span>
        } @else if ((p.interesados ?? 0) > 0) {
          <span class="absolute right-3 bottom-3 insignia bg-bosque-950/80 text-white backdrop-blur">
            <app-icono nombre="usuarios" [tamano]="12" />{{ p.interesados }} {{ p.interesados === 1 ? 'interesada' : 'interesadas' }}
          </span>
        }
      </div>
      <div class="flex flex-1 flex-col gap-2 p-4">
        <p class="flex items-center justify-between gap-2 text-xs font-medium text-tenue">
          <span class="truncate">{{ p.categoria?.nombre }}</span>
          <span class="shrink-0 rounded-full bg-superficie-2 px-2 py-0.5 text-[11px] font-semibold">{{ condicion().etiqueta }}</span>
        </p>
        <h3 class="line-clamp-2 font-display text-base leading-snug font-bold group-hover:text-bosque-700 dark:group-hover:text-bosque-300">
          {{ p.titulo }}
        </h3>
        <div class="mt-auto flex items-end justify-between gap-2 pt-2">
          <div class="min-w-0">
            @if (p.modo === 'Compra' && p.precioReferenciaCop) {
              <p class="font-display text-lg font-extrabold text-cielo-700 dark:text-cielo-100">{{ p.precioReferenciaCop | cop }}</p>
            } @else if (p.modo === 'Donacion') {
              <p class="font-display text-lg font-extrabold text-tierra-600 dark:text-tierra-500">Gratis</p>
            } @else {
              <p class="font-display text-sm font-bold text-bosque-700 dark:text-bosque-300">
                Para trueke@if (p.precioReferenciaCop) {<span class="font-medium text-tenue"> · ref. {{ p.precioReferenciaCop | cop }}</span>}
              </p>
            }
            <p class="mt-0.5 flex items-center gap-1 truncate text-xs text-tenue">
              <app-icono nombre="pin" [tamano]="12" />{{ p.localidad }}@if (ciudad()) {<span>· {{ ciudad() }}</span>}
              @if (distanciaKm() !== null) {
                · {{ distanciaKm()! < 1 ? '< 1' : distanciaKm()!.toFixed(1) }} km
              }
            </p>
          </div>
          <span class="shrink-0 text-xs text-tenue">{{ p.fechaPublicacion | hace }}</span>
        </div>
      </div>
    </article>
  `,
})
export class TarjetaPublicacion {
  readonly publicacion = input.required<PublicacionDto>();
  readonly distanciaKm = input<number | null>(null);
  protected readonly favoritos = inject(FavoritosService);
  protected readonly favorita = computed(() =>
    this.favoritos.esFavorita(this.publicacion().id, this.publicacion().esFavorita),
  );
  protected readonly condicion = computed(() => infoCondicion(this.publicacion().condicion));
  /** "Medellín, Antioquia" → "Medellín"; "Bogotá, D.C." → "Bogotá". */
  protected readonly ciudad = computed(() => (this.publicacion().municipio ?? '').split(',')[0]?.trim() ?? '');
}

@Component({
  selector: 'app-tarjeta-esqueleto',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block', 'aria-hidden': 'true' },
  template: `
    <div class="tarjeta overflow-hidden">
      <div class="esqueleto aspect-[4/3] rounded-none"></div>
      <div class="space-y-3 p-4">
        <div class="esqueleto h-3 w-1/3"></div>
        <div class="esqueleto h-4 w-4/5"></div>
        <div class="esqueleto h-4 w-1/2"></div>
      </div>
    </div>
  `,
})
export class TarjetaEsqueleto {}
