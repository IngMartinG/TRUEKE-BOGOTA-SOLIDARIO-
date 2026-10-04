import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Paginador } from '../../shared/ui/paginador';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';
import { Volver } from '../../shared/ui/volver';

const TAMANO = 12;

@Component({
  imports: [Volver, RouterLink, TarjetaPublicacion, TarjetaEsqueleto, EstadoVacio, Paginador],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-10">
      <app-volver respaldo="/" class="mb-1" />
      <h1 class="text-3xl font-extrabold">Tus favoritos</h1>
      <p class="mt-1 text-tenue">Lo que guardaste para no perderlo de vista.</p>

      <div class="mt-8 grid grid-cols-2 gap-3 sm:gap-5 lg:grid-cols-4">
        @if (recurso.isLoading()) {
          @for (i of [1, 2, 3, 4]; track i) {
            <app-tarjeta-esqueleto />
          }
        } @else {
          @for (p of recurso.value()?.items ?? []; track p.id) {
            <app-tarjeta-publicacion [publicacion]="p" />
          }
        }
      </div>

      @if (!recurso.isLoading() && !recurso.value()?.items?.length) {
        <app-estado-vacio icono="heart" titulo="Aún no tienes favoritos" descripcion="Toca el corazón en cualquier publicación para guardarla aquí.">
          <a routerLink="/explorar" class="btn btn-primario">Explorar el catálogo</a>
        </app-estado-vacio>
      }

      <app-paginador [pagina]="pagina()" [total]="recurso.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />
    </div>
  `,
})
export default class Favoritos {
  private readonly api = inject(CatalogoApi);
  protected readonly tamano = TAMANO;
  protected readonly pagina = signal(1);
  protected readonly recurso = rxResource({
    params: () => this.pagina(),
    stream: ({ params }) => this.api.favoritos(params, TAMANO),
  });
}
