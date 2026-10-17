import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import type { CoincidenciaDeseoDto, DeseoDto, GuardarDeseoRequest } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { ImagenPublicacion } from '../../shared/ui/imagen-publicacion';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';
import { Volver } from '../../shared/ui/volver';

const MAXIMO = 10;

/** "Lo que busco": la lista de deseos y las publicaciones de otras personas que coinciden, con los truekes mutuos primero. */
@Component({
  imports: [
    FormsModule,
    RouterLink,
    Volver,
    Icono,
    EstadoVacio,
    ImagenPublicacion,
    TarjetaPublicacion,
    TarjetaEsqueleto,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-10">
      <app-volver respaldo="/" class="mb-1" />
      <h1 class="text-3xl font-extrabold">Lo que busco</h1>
      <p class="mt-1 max-w-2xl text-tenue">
        Anota lo que te hace falta. Te mostramos quién lo tiene y, si esa persona busca algo que tú
        publicaste, te proponemos el trueke.
      </p>

      <div class="mt-8 grid grid-cols-1 gap-10 lg:grid-cols-[22rem_1fr]">
        <section aria-labelledby="titulo-lista" class="min-w-0">
          <div class="flex items-baseline justify-between gap-3">
            <h2 id="titulo-lista" class="text-xl font-bold">Tu lista</h2>
            <span class="text-sm text-tenue tabular-nums"
              >{{ deseos.value()?.length ?? 0 }} de {{ maximo }}</span
            >
          </div>

          @if ((deseos.value()?.length ?? 0) < maximo) {
            <form class="tarjeta mt-4 space-y-3 p-4" (ngSubmit)="agregar()">
              <div>
                <label for="deseo-texto" class="etiqueta">¿Qué buscas?</label>
                <input
                  id="deseo-texto"
                  name="texto"
                  class="entrada"
                  maxlength="60"
                  autocomplete="off"
                  required
                  placeholder="Por ejemplo: bicicleta de ruta"
                  [(ngModel)]="texto"
                  [attr.aria-invalid]="!!error()"
                  [attr.aria-describedby]="error() ? 'deseo-error' : 'deseo-ayuda'"
                />
                @if (error()) {
                  <p
                    id="deseo-error"
                    class="mt-1 text-sm text-tierra-700 dark:text-tierra-400"
                    role="alert"
                  >
                    {{ error() }}
                  </p>
                } @else {
                  <p id="deseo-ayuda" class="ayuda">
                    Pocas palabras funcionan mejor: buscamos publicaciones que las tengan todas.
                  </p>
                }
              </div>
              <div>
                <label for="deseo-categoria" class="etiqueta">Categoría</label>
                <select
                  id="deseo-categoria"
                  name="categoria"
                  class="entrada"
                  [(ngModel)]="categoriaId"
                >
                  <option [ngValue]="null">Cualquiera</option>
                  @for (c of categorias(); track c.id) {
                    <option [ngValue]="c.id">{{ c.nombre }}</option>
                  }
                </select>
              </div>
              <label class="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  name="publico"
                  class="mt-0.5 accent-bosque-600"
                  [(ngModel)]="publico"
                />
                <span
                  >Mostrarlo en mi perfil
                  <span class="block text-xs text-tenue"
                    >Así otras personas saben qué ofrecerte.</span
                  ></span
                >
              </label>
              <button
                type="submit"
                class="btn btn-primario w-full"
                [disabled]="trabajando() || texto().trim().length < 3"
              >
                <app-icono nombre="mas" [tamano]="18" />Agregar a mi lista
              </button>
            </form>
          } @else {
            <p
              class="mt-4 rounded-tarjeta border-2 border-dashed border-borde p-4 text-sm text-tenue"
            >
              Tu lista está llena. Borra un deseo para agregar otro.
            </p>
          }

          @if (deseos.isLoading() && !deseos.value()) {
            <div class="mt-4 space-y-3">
              <div class="esqueleto h-16"></div>
              <div class="esqueleto h-16"></div>
            </div>
          } @else {
            <ul class="mt-4 space-y-2">
              @for (d of deseos.value(); track d.id) {
                <li class="tarjeta p-3">
                  @if (editando() === d.id) {
                    <form class="flex gap-2" (ngSubmit)="guardarTexto(d)">
                      <label class="sr-only" [for]="'editar-' + d.id">Editar «{{ d.texto }}»</label>
                      <input
                        [id]="'editar-' + d.id"
                        name="nuevo"
                        class="entrada min-w-0 flex-1"
                        maxlength="60"
                        required
                        [(ngModel)]="textoEditado"
                      />
                      <button
                        type="submit"
                        class="btn btn-primario btn-sm"
                        [disabled]="ocupado() === d.id || textoEditado().trim().length < 3"
                      >
                        Guardar
                      </button>
                      <button
                        type="button"
                        class="btn btn-fantasma btn-sm"
                        (click)="editando.set(null)"
                      >
                        Cancelar
                      </button>
                    </form>
                  } @else {
                    <div class="flex items-start gap-2">
                      <div class="min-w-0 flex-1">
                        <p class="font-semibold break-words">{{ d.texto }}</p>
                        <p class="text-xs text-tenue">{{ d.categoria ?? 'Cualquier categoría' }}</p>
                      </div>
                      @if (!d.oculto) {
                        <button
                          type="button"
                          class="btn-icono"
                          (click)="editar(d)"
                          [disabled]="ocupado() === d.id"
                          [attr.aria-label]="'Editar ' + d.texto"
                        >
                          <app-icono nombre="editar" [tamano]="16" />
                        </button>
                      }
                      <button
                        type="button"
                        class="btn-icono text-tierra-600 dark:text-tierra-500"
                        (click)="eliminar(d)"
                        [disabled]="ocupado() === d.id"
                        [attr.aria-label]="'Borrar ' + d.texto"
                      >
                        <app-icono nombre="basura" [tamano]="16" />
                      </button>
                    </div>
                    @if (d.oculto) {
                      <p
                        class="mt-2 rounded-xl bg-tierra-50 p-2 text-xs text-tierra-800 dark:bg-tierra-700/20 dark:text-tierra-100"
                      >
                        Oculto por moderación{{
                          d.motivoOcultamiento ? ': ' + d.motivoOcultamiento : ''
                        }}. Ya no se ve en tu perfil; puedes borrarlo.
                      </p>
                    } @else {
                      <div class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm">
                        <label class="flex cursor-pointer items-center gap-2">
                          <input
                            type="checkbox"
                            class="accent-bosque-600"
                            [checked]="d.publico"
                            [disabled]="ocupado() === d.id"
                            (change)="cambiar(d, { publico: $any($event.target).checked })"
                          />
                          En mi perfil
                        </label>
                        <label class="flex cursor-pointer items-center gap-2">
                          <input
                            type="checkbox"
                            class="accent-bosque-600"
                            [checked]="d.avisosActivos"
                            [disabled]="ocupado() === d.id"
                            (change)="cambiar(d, { avisos: $any($event.target).checked })"
                          />
                          Avisarme
                        </label>
                      </div>
                    }
                  }
                </li>
              }
            </ul>
          }
        </section>

        <section aria-labelledby="titulo-coincidencias" class="min-w-0">
          <h2 id="titulo-coincidencias" class="text-xl font-bold">Quién lo tiene</h2>
          @if (coincidencias.isLoading() && !coincidencias.value()) {
            <div class="mt-4 grid grid-cols-2 gap-3 sm:gap-5 xl:grid-cols-3">
              @for (i of [1, 2, 3]; track i) {
                <app-tarjeta-esqueleto />
              }
            </div>
          } @else if (!deseos.value()?.length) {
            <app-estado-vacio
              icono="mira"
              titulo="Empieza tu lista"
              [nivel]="3"
              descripcion="Escribe lo primero que te hace falta. Aquí aparecerán las publicaciones que coinciden."
            />
          } @else if (!coincidencias.value()?.length) {
            <app-estado-vacio
              icono="mira"
              titulo="Todavía nadie lo publica"
              [nivel]="3"
              descripcion="Te avisamos cuando alguien publique algo de tu lista (si dejaste «Avisarme» encendido)."
            >
              <a routerLink="/publicar" class="btn btn-secundario"
                >Publicar algo para intercambiar</a
              >
            </app-estado-vacio>
          } @else {
            @if (mutuas().length) {
              <ul class="mt-4 space-y-3" aria-label="Truekes posibles">
                @for (c of mutuas(); track c.publicacion?.id) {
                  <li
                    class="relative overflow-hidden rounded-tarjeta border-2 border-sol-300 bg-sol-50 p-4 dark:border-sol-500/40 dark:bg-sol-500/10"
                  >
                    <p
                      class="flex items-center gap-2 text-sm font-bold text-sol-700 dark:text-sol-300"
                    >
                      <app-icono nombre="repeat" [tamano]="16" />Trueke posible
                    </p>
                    <div class="mt-3 grid grid-cols-[1fr_auto_1fr] items-center gap-3">
                      <a [routerLink]="['/publicacion', c.publicacion?.id]" class="group min-w-0">
                        <app-imagen-publicacion
                          [src]="c.publicacion?.imagenes?.[0]"
                          [modo]="c.publicacion?.modo"
                          [categoriaId]="c.publicacion?.categoria?.id"
                          [miniatura]="true"
                          alt=""
                          class="aspect-square rounded-xl"
                        />
                        <p class="mt-1.5 truncate text-sm font-semibold group-hover:underline">
                          {{ c.publicacion?.titulo }}
                        </p>
                        <p class="truncate text-xs text-tenue">buscabas «{{ c.tuDeseo }}»</p>
                      </a>
                      <span
                        class="grid size-10 place-items-center rounded-full bg-superficie text-sol-600 shadow-suave dark:text-sol-300"
                        aria-hidden="true"
                      >
                        <app-icono nombre="repeat" [tamano]="20" />
                      </span>
                      <div class="min-w-0">
                        <div
                          class="grid aspect-square place-items-center rounded-xl border-2 border-dashed border-sol-300 bg-superficie p-3 text-center dark:border-sol-500/40"
                        >
                          <span class="line-clamp-4 text-sm font-semibold break-words">{{
                            c.tuPublicacionTitulo
                          }}</span>
                        </div>
                        <p class="mt-1.5 truncate text-sm font-semibold">Lo tuyo</p>
                        <p class="truncate text-xs text-tenue">busca «{{ c.suDeseo }}»</p>
                      </div>
                    </div>
                    <div class="mt-4 flex flex-wrap gap-2">
                      <a
                        [routerLink]="['/publicacion', c.publicacion?.id]"
                        [queryParams]="{ ofrecer: c.tuPublicacionId }"
                        class="btn btn-sol btn-sm"
                      >
                        Proponer trueke
                      </a>
                      <a
                        [routerLink]="['/usuarios', c.publicacion?.propietario?.id]"
                        class="btn btn-fantasma btn-sm"
                        >Ver su perfil</a
                      >
                    </div>
                  </li>
                }
              </ul>
            }
            @if (otras().length) {
              @if (mutuas().length) {
                <h3 class="mt-8 font-bold">También coinciden con tu lista</h3>
              }
              <div class="mt-4 grid grid-cols-2 gap-3 sm:gap-5 xl:grid-cols-3">
                @for (c of otras(); track c.publicacion?.id) {
                  <div class="min-w-0">
                    <app-tarjeta-publicacion [publicacion]="c.publicacion!" />
                    <p class="mt-1.5 truncate px-1 text-xs text-tenue">Por «{{ c.tuDeseo }}»</p>
                  </div>
                }
              </div>
            }
          }
        </section>
      </div>
    </div>
  `,
})
export default class LoQueBusco {
  private readonly api = inject(CatalogoApi);
  private readonly avisos = inject(AvisosService);
  protected readonly maximo = MAXIMO;

  protected readonly deseos = rxResource({ stream: () => this.api.deseos() });
  protected readonly coincidencias = rxResource({ stream: () => this.api.coincidencias() });
  protected readonly categorias = toSignal(this.api.categorias$.pipe(catchError(() => of([]))), {
    initialValue: [],
  });

  protected readonly mutuas = computed(() =>
    (this.coincidencias.value() ?? []).filter((c) => c.mutua),
  );
  protected readonly otras = computed<CoincidenciaDeseoDto[]>(() =>
    (this.coincidencias.value() ?? []).filter((c) => !c.mutua),
  );

  protected readonly texto = signal('');
  protected readonly categoriaId = signal<number | null>(null);
  protected readonly publico = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly trabajando = signal(false);
  protected readonly ocupado = signal<string | null>(null);
  protected readonly editando = signal<string | null>(null);
  protected readonly textoEditado = signal('');

  protected async agregar(): Promise<void> {
    const texto = this.texto().trim();
    if (texto.length < 3) return;
    this.trabajando.set(true);
    this.error.set(null);
    try {
      const nuevo = await firstValueFrom(
        this.api.crearDeseo({
          texto,
          categoriaId: this.categoriaId(),
          publico: this.publico(),
          avisos: true,
        }),
      );
      this.deseos.update((l) => [nuevo, ...(l ?? [])]);
      this.texto.set('');
      this.categoriaId.set(null);
      this.avisos.exito('Agregado a tu lista');
      this.coincidencias.reload();
    } catch (e) {
      this.error.set(mensajeDe(e));
    } finally {
      this.trabajando.set(false);
    }
  }

  protected editar(d: DeseoDto): void {
    this.textoEditado.set(d.texto ?? '');
    this.editando.set(d.id!);
  }

  protected async guardarTexto(d: DeseoDto): Promise<void> {
    if (await this.actualizar(d, { texto: this.textoEditado().trim() })) {
      this.editando.set(null);
      this.coincidencias.reload();
    }
  }

  protected async cambiar(d: DeseoDto, cambio: Partial<GuardarDeseoRequest>): Promise<void> {
    const ok = await this.actualizar(d, cambio);
    if (!ok)
      this.deseos.reload(); // el interruptor vuelve a su estado real
    else if (cambio.publico !== undefined)
      this.avisos.exito(cambio.publico ? 'Ahora se ve en tu perfil' : 'Ya no se ve en tu perfil');
    else this.avisos.exito(cambio.avisos ? 'Te avisaremos' : 'Avisos apagados');
  }

  private async actualizar(d: DeseoDto, cambio: Partial<GuardarDeseoRequest>): Promise<boolean> {
    this.ocupado.set(d.id!);
    try {
      const r: GuardarDeseoRequest = {
        texto: d.texto ?? '',
        categoriaId: d.categoriaId,
        publico: !!d.publico,
        avisos: !!d.avisosActivos,
        ...cambio,
      };
      const nuevo = await firstValueFrom(this.api.editarDeseo(d.id!, r));
      this.deseos.update((l) => l?.map((x) => (x.id === nuevo.id ? nuevo : x)));
      return true;
    } catch (e) {
      this.avisos.error(mensajeDe(e));
      return false;
    } finally {
      this.ocupado.set(null);
    }
  }

  protected async eliminar(d: DeseoDto): Promise<void> {
    this.ocupado.set(d.id!);
    try {
      await firstValueFrom(this.api.eliminarDeseo(d.id!));
      this.deseos.update((l) => l?.filter((x) => x.id !== d.id));
      this.avisos.exito('Borrado de tu lista');
      this.coincidencias.reload();
    } finally {
      this.ocupado.set(null);
    }
  }
}
