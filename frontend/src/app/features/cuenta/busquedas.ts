import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { BusquedaGuardadaDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';

/** Los filtros de la búsqueda como parámetros del catálogo (/explorar). */
export function parametrosDe(b: BusquedaGuardadaDto): Record<string, string> {
  const p: Record<string, string> = {};
  const poner = (clave: string, valor: string | number | null | undefined) => {
    if (valor !== null && valor !== undefined && valor !== '') p[clave] = String(valor);
  };
  poner('texto', b.texto);
  poner('categoriaId', b.categoriaId);
  poner('modo', b.modo);
  poner('condicion', b.condicion);
  poner('departamentoCodigo', b.departamentoCodigo);
  poner('municipioCodigo', b.municipioCodigo);
  poner('localidad', b.localidad);
  poner('precioMin', b.precioMin);
  poner('precioMax', b.precioMax);
  if (b.soloVerificados) p['soloVerificados'] = 'true';
  return p;
}

/** Búsquedas guardadas desde el catálogo, con alertas de publicaciones nuevas. */
@Component({
  imports: [RouterLink, Icono, EstadoVacio],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Búsquedas guardadas</h1>
    <p class="mt-1 text-tenue">
      Con la alerta encendida te avisamos (en la app y con notificaciones, si las activaste) cuando publiquen algo nuevo que coincida,
      como mucho una vez por hora por búsqueda.
    </p>

    @if (lista.isLoading() && !lista.value()) {
      <div class="mt-6 space-y-3"><div class="esqueleto h-20"></div><div class="esqueleto h-20"></div></div>
    } @else {
      <ul class="mt-6 space-y-3">
        @for (b of lista.value(); track b.id) {
          <li class="tarjeta flex flex-wrap items-center gap-4 p-4">
            <span class="grid size-10 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900/40 dark:text-bosque-200">
              <app-icono nombre="buscar" [tamano]="20" />
            </span>
            <div class="min-w-0 flex-1">
              <p class="truncate font-semibold">{{ b.nombre }}</p>
              <a [routerLink]="['/explorar']" [queryParams]="parametros(b)" class="enlace text-sm">Ver resultados</a>
            </div>
            <label class="flex shrink-0 cursor-pointer items-center gap-2 text-sm">
              <span class="text-tenue">Alerta</span>
              <input type="checkbox" role="switch" class="peer sr-only" [checked]="b.alertasActivas" [disabled]="ocupada() === b.id"
                (change)="cambiarAlertas(b, $any($event.target).checked)" [attr.aria-label]="'Alerta para ' + b.nombre" />
              <span class="relative block h-6 w-11 rounded-full bg-borde transition peer-checked:bg-bosque-600 peer-focus-visible:ring-2 peer-focus-visible:ring-bosque-400 peer-disabled:opacity-60
                after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:shadow after:transition peer-checked:after:translate-x-5" aria-hidden="true"></span>
            </label>
            <button type="button" class="btn-icono text-tierra-600" (click)="eliminar(b)" [disabled]="ocupada() === b.id" [attr.aria-label]="'Borrar la búsqueda ' + b.nombre">
              <app-icono nombre="basura" [tamano]="18" />
            </button>
          </li>
        } @empty {
          <app-estado-vacio icono="buscar" titulo="Aún no guardas búsquedas"
            descripcion="En el catálogo, busca o filtra lo que te interesa y toca «Guardar búsqueda»: te avisaremos cuando aparezca algo nuevo.">
            <a routerLink="/explorar" class="btn btn-primario">Ir al catálogo</a>
          </app-estado-vacio>
        }
      </ul>
    }
  `,
})
export default class Busquedas {
  private readonly api = inject(CatalogoApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);

  /** Desde una notificación: ?ver=<id> abre directamente los resultados de esa búsqueda. */
  readonly ver = input<string>();

  protected readonly lista = rxResource({ stream: () => this.api.busquedasGuardadas() });
  protected readonly ocupada = signal<string | null>(null);
  protected readonly parametros = parametrosDe;

  constructor() {
    effect(() => {
      const id = this.ver();
      const b = this.lista.value()?.find((x) => x.id === id);
      if (b) untracked(() => void this.router.navigate(['/explorar'], { queryParams: parametrosDe(b), replaceUrl: true }));
    });
  }

  protected async cambiarAlertas(b: BusquedaGuardadaDto, activas: boolean): Promise<void> {
    this.ocupada.set(b.id!);
    try {
      const nueva = await firstValueFrom(this.api.alertasBusqueda(b.id!, activas));
      this.lista.update((l) => l?.map((x) => (x.id === nueva.id ? nueva : x)));
      this.avisos.exito(activas ? 'Alerta encendida' : 'Alerta apagada');
    } catch {
      this.lista.reload(); // el interceptor ya mostró el error
    } finally {
      this.ocupada.set(null);
    }
  }

  protected async eliminar(b: BusquedaGuardadaDto): Promise<void> {
    this.ocupada.set(b.id!);
    try {
      await firstValueFrom(this.api.eliminarBusqueda(b.id!));
      this.lista.update((l) => l?.filter((x) => x.id !== b.id));
      this.avisos.exito('Búsqueda borrada');
    } finally {
      this.ocupada.set(null);
    }
  }
}
