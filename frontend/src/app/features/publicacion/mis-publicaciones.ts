import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ETIQUETA_ESTADO_PUBLICACION } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CopPipe, HacePipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { ImagenPublicacion } from '../../shared/ui/imagen-publicacion';
import { InsigniaModo } from '../../shared/ui/insignia-modo';

type Filtro = 'activas' | 'negociacion' | 'cerradas';

@Component({
  imports: [RouterLink, Icono, ImagenPublicacion, InsigniaModo, EstadoVacio, CopPipe, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-10">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 class="text-3xl font-extrabold">Mis publicaciones</h1>
          <p class="mt-1 text-tenue">Administra lo que ofreces a la comunidad.</p>
        </div>
        <a routerLink="/publicar" class="btn btn-primario"><app-icono nombre="mas" [tamano]="18" />Nueva publicación</a>
      </div>

      <div class="mt-6 pestanas" role="tablist" aria-label="Filtrar publicaciones">
        @for (f of filtros; track f.valor) {
          <button type="button" role="tab" class="pestana" [class.pestana-activa]="filtro() === f.valor" [attr.aria-selected]="filtro() === f.valor" (click)="filtro.set(f.valor)">
            {{ f.texto }} <span class="ml-1 opacity-70">{{ conteo()[f.valor] }}</span>
          </button>
        }
      </div>

      <ul class="mt-6 space-y-3">
        @if (recurso.isLoading()) {
          @for (i of [1, 2, 3]; track i) {
            <li class="esqueleto h-28 rounded-tarjeta"></li>
          }
        } @else {
          @for (p of visibles(); track p.id) {
            <li class="tarjeta flex animate-aparecer flex-col gap-4 p-4 sm:flex-row sm:items-center">
              <a [routerLink]="['/publicacion', p.id]" class="flex min-w-0 flex-1 items-center gap-4">
                <app-imagen-publicacion class="size-20 shrink-0 rounded-2xl sm:size-24" [src]="p.imagenes?.[0]" [modo]="p.modo" [categoriaId]="p.categoria?.id" />
                <div class="min-w-0">
                  <div class="flex flex-wrap items-center gap-1.5">
                    <app-insignia-modo [modo]="p.modo" />
                    <span class="insignia-neutra">{{ etiquetaEstado[p.estado ?? ''] ?? p.estado }}</span>
                    @if (p.destacada) {
                      <span class="insignia-sol"><app-icono nombre="destello" [tamano]="12" />Destacada</span>
                    }
                    @if (p.oculta) {
                      <span class="insignia bg-tierra-100 text-tierra-700"><app-icono nombre="ojoNo" [tamano]="12" />Oculta</span>
                    }
                  </div>
                  <p class="mt-1.5 truncate font-display font-bold">{{ p.titulo }}</p>
                  <p class="text-xs text-tenue">
                    {{ p.localidad }} · publicada {{ p.fechaPublicacion | hace }}
                    @if (p.precioReferenciaCop) {
                      · {{ p.precioReferenciaCop | cop }}
                    }
                  </p>
                </div>
              </a>
              <div class="flex shrink-0 gap-2">
                <a routerLink="/intercambios" [queryParams]="{ tab: 'recibidas' }" class="btn btn-secundario btn-sm"><app-icono nombre="apreton" [tamano]="14" />Solicitudes</a>
                @if (p.estado === 'Disponible') {
                  <a [routerLink]="['/publicacion', p.id, 'editar']" class="btn btn-secundario btn-sm"><app-icono nombre="editar" [tamano]="14" />Editar</a>
                }
              </div>
            </li>
          }
        }
      </ul>

      @if (!recurso.isLoading() && visibles().length === 0) {
        @if (filtro() === 'activas') {
          <app-estado-vacio icono="paquete" titulo="No tienes publicaciones activas" descripcion="Publica lo que ya no usas: alguien en tu localidad lo está buscando.">
            <a routerLink="/publicar" class="btn btn-primario">Publicar mi primer objeto</a>
          </app-estado-vacio>
        } @else {
          <app-estado-vacio icono="hoja" titulo="Nada por aquí" descripcion="Cuando tus publicaciones cambien de estado, aparecerán en esta pestaña." />
        }
      }
    </div>
  `,
})
export default class MisPublicaciones {
  private readonly api = inject(CatalogoApi);
  protected readonly etiquetaEstado = ETIQUETA_ESTADO_PUBLICACION;
  protected readonly filtro = signal<Filtro>('activas');
  protected readonly filtros: { valor: Filtro; texto: string }[] = [
    { valor: 'activas', texto: 'Disponibles' },
    { valor: 'negociacion', texto: 'En negociación' },
    { valor: 'cerradas', texto: 'Cerradas' },
  ];
  protected readonly recurso = rxResource({ stream: () => this.api.mias() });

  private readonly grupo = (estado: string | undefined): Filtro =>
    estado === 'Disponible' ? 'activas' : estado === 'EnNegociacion' ? 'negociacion' : 'cerradas';

  protected readonly visibles = computed(() => (this.recurso.value() ?? []).filter((p) => this.grupo(p.estado) === this.filtro()));
  protected readonly conteo = computed(() => {
    const c: Record<Filtro, number> = { activas: 0, negociacion: 0, cerradas: 0 };
    for (const p of this.recurso.value() ?? []) c[this.grupo(p.estado)]++;
    return c;
  });
}
