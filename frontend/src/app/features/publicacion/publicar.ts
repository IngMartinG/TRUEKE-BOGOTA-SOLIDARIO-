import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import {
  CENTRO_LOCALIDAD,
  CODIGO_BOGOTA,
  CONDICIONES,
  iconoCategoria,
  infoCondicion,
  INFO_MODO,
  LOCALIDADES,
  MODOS,
  type CondicionDto,
  type CrearPublicacionRequest,
  type ModoDto,
  type MunicipioDto,
} from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { erroresDeCampos, mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { SubidasService } from '../../core/subidas.service';
import { comprimirImagen, urlPublica } from '../../shared/imagenes';
import { CopPipe } from '../../shared/pipes';
import { aplicarErroresServidor, ErrorCampo } from '../../shared/ui/error-campo';
import { Icono } from '../../shared/ui/icono';
import { ImagenPublicacion } from '../../shared/ui/imagen-publicacion';
import { InsigniaModo } from '../../shared/ui/insignia-modo';
import { Mapa } from '../../shared/ui/mapa';
import { SelectorMunicipio } from '../../shared/ui/selector-municipio';
import { Volver } from '../../shared/ui/volver';

interface Foto {
  clave: string;
  vista: string;
  url?: string;
  progreso: number;
  error?: string;
  local?: boolean;
}

const PASOS = ['Modo', 'Detalles', 'Fotos', 'Ubicación', 'Revisar'] as const;

@Component({
  imports: [Volver, ReactiveFormsModule, RouterLink, Icono, ErrorCampo, Mapa, InsigniaModo, ImagenPublicacion, CopPipe, SelectorMunicipio],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-3xl py-6 sm:py-10">
      <div class="mb-3 flex items-center justify-between gap-4">
        <app-volver [respaldo]="id() ? '/publicacion/' + id() : '/mis-publicaciones'" />
        <a [routerLink]="id() ? ['/publicacion', id()] : '/mis-publicaciones'" class="btn btn-fantasma btn-sm">Cancelar</a>
      </div>
      <h1 class="text-3xl font-extrabold">{{ id() ? 'Editar publicación' : 'Publica algo' }}</h1>
      <p class="mt-1 text-tenue">{{ id() ? 'Actualiza la información de tu publicación.' : 'En cinco pasos sencillos tu objeto estará visible para personas de todo el país.' }}</p>

      <!-- Indicador de pasos -->
      <ol class="mt-8 grid grid-cols-5 gap-2" aria-label="Pasos">
        @for (nombre of pasos; track nombre; let i = $index) {
          <li>
            <button type="button" class="group w-full text-left" [disabled]="i > pasoMaximo()" (click)="irA(i)" [attr.aria-label]="'Paso ' + (i + 1) + ': ' + nombre" [attr.aria-current]="i === paso() ? 'step' : null">
              <span class="block h-1.5 rounded-full transition-all" [class]="i <= paso() ? 'bg-bosque-500' : 'bg-borde'"></span>
              <span class="mt-2 hidden text-xs font-semibold sm:block" [class]="i === paso() ? 'text-tinta' : 'text-tenue'">{{ i + 1 }}. {{ nombre }}</span>
            </button>
          </li>
        }
      </ol>
      <p class="mt-2 text-sm font-semibold sm:hidden">Paso {{ paso() + 1 }} de {{ pasos.length }}: {{ pasos[paso()] }}</p>

      @if (cargandoEdicion()) {
        <div class="mt-8 space-y-4"><div class="esqueleto h-40"></div><div class="esqueleto h-12"></div></div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="publicar()" class="mt-8" novalidate>
          @switch (paso()) {
            <!-- ========== 1. MODO ========== -->
            @case (0) {
              <fieldset class="animate-aparecer">
                <legend class="text-xl font-bold">¿Qué quieres hacer con tu objeto?</legend>
                <div class="mt-5 grid gap-3">
                  @for (m of modos; track m) {
                    <label class="group flex cursor-pointer items-center gap-4 rounded-tarjeta border-2 bg-superficie p-5 transition"
                      [class]="modoActual() === m ? bordeModo[m] : 'border-borde hover:border-bosque-200'">
                      <input type="radio" class="sr-only" formControlName="modo" [value]="m" [attr.aria-label]="info[m].etiqueta" />
                      <span class="grid size-14 shrink-0 place-items-center rounded-2xl text-white shadow-lg transition group-hover:scale-105" [class]="solidoModo[m]">
                        <app-icono [nombre]="info[m].icono" [tamano]="26" />
                      </span>
                      <span class="flex-1">
                        <span class="block font-display text-lg font-bold">{{ info[m].etiqueta }}</span>
                        <span class="block text-sm text-tenue">{{ info[m].descripcion }}</span>
                      </span>
                      <span class="grid size-6 shrink-0 place-items-center rounded-full border-2 transition"
                        [class]="modoActual() === m ? 'border-bosque-600 bg-bosque-600 text-white' : 'border-borde'">
                        @if (modoActual() === m) {
                          <app-icono nombre="check" [tamano]="14" [grosor]="3" />
                        }
                      </span>
                    </label>
                  }
                </div>
              </fieldset>
            }

            <!-- ========== 2. DETALLES ========== -->
            @case (1) {
              <div class="animate-aparecer space-y-6">
                <div class="campo">
                  <label class="etiqueta" for="titulo">Título</label>
                  <input id="titulo" class="entrada text-base" formControlName="titulo" maxlength="120" placeholder="Ej: Bicicleta de ruta talla M, poco uso"
                    [attr.aria-invalid]="form.controls.titulo.invalid && form.controls.titulo.touched" aria-describedby="titulo-error" />
                  <div class="flex justify-between">
                    <app-error-campo [control]="form.controls.titulo" etiqueta="El título" idError="titulo-error" />
                    <span class="ml-auto ayuda">{{ form.controls.titulo.value.length }}/120</span>
                  </div>
                </div>

                <fieldset>
                  <legend class="etiqueta mb-2">Categoría</legend>
                  <div class="grid grid-cols-2 gap-2 sm:grid-cols-4">
                    @for (c of categorias(); track c.id) {
                      <label class="flex cursor-pointer flex-col items-center gap-2 rounded-2xl border-2 p-3 text-center text-xs font-semibold transition"
                        [class]="categoriaActual() === c.id ? 'border-bosque-500 bg-bosque-50 text-bosque-800 dark:bg-bosque-900/50 dark:text-bosque-100' : 'border-borde hover:border-bosque-200'">
                        <input type="radio" class="sr-only" formControlName="categoriaId" [value]="c.id" [attr.aria-label]="c.nombre" />
                        <app-icono [nombre]="iconoCategoria(c.id)" [tamano]="24" [grosor]="1.7" />{{ c.nombre }}
                      </label>
                    }
                  </div>
                  <app-error-campo [control]="form.controls.categoriaId" etiqueta="La categoría" />
                </fieldset>

                <fieldset>
                  <legend class="etiqueta mb-1">¿En qué estado está?</legend>
                  <p class="ayuda mb-2">Sé honesto: un estado claro evita reclamos y genera confianza.</p>
                  <div class="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    @for (c of condiciones; track c.valor) {
                      <label class="flex cursor-pointer items-start gap-3 rounded-2xl border-2 p-3 transition"
                        [class]="condicionActual() === c.valor ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/50' : 'border-borde hover:border-bosque-200'">
                        <input type="radio" class="sr-only" formControlName="condicion" [value]="c.valor" [attr.aria-label]="c.etiqueta" />
                        <span class="mt-0.5 grid size-5 shrink-0 place-items-center rounded-full border-2 transition"
                          [class]="condicionActual() === c.valor ? 'border-bosque-600 bg-bosque-600 text-white' : 'border-borde'">
                          @if (condicionActual() === c.valor) {
                            <app-icono nombre="check" [tamano]="12" [grosor]="3" />
                          }
                        </span>
                        <span>
                          <span class="block text-sm font-semibold">{{ c.etiqueta }}</span>
                          <span class="block text-xs text-tenue">{{ c.descripcion }}</span>
                        </span>
                      </label>
                    }
                  </div>
                  <app-error-campo [control]="form.controls.condicion" etiqueta="El estado del producto" />
                </fieldset>

                @if (requiereDetalle()) {
                  <div class="campo animate-aparecer">
                    <label class="etiqueta" for="detalle-condicion">
                      {{ condicionActual() === 'Reparado' ? '¿Qué se reparó y quién lo hizo?' : condicionActual() === 'ParaRepuestos' ? '¿Qué falla y qué piezas sirven?' : '¿Qué detalles tiene?' }}
                    </label>
                    <textarea id="detalle-condicion" class="entrada min-h-24" formControlName="detalleCondicion" maxlength="300"
                      placeholder="Ej: rayón en la tapa trasera; batería cambiada en servicio técnico en 2025; no enciende pero la pantalla sirve."
                      [attr.aria-invalid]="form.controls.detalleCondicion.invalid && form.controls.detalleCondicion.touched" aria-describedby="detalle-error"></textarea>
                    <div class="flex justify-between">
                      <app-error-campo [control]="form.controls.detalleCondicion" etiqueta="La descripción del estado" idError="detalle-error" />
                      <span class="ml-auto ayuda">{{ form.controls.detalleCondicion.value.length }}/300</span>
                    </div>
                  </div>
                }

                <div class="campo">
                  <label class="etiqueta" for="descripcion">Descripción</label>
                  <textarea id="descripcion" class="entrada min-h-36" formControlName="descripcion" maxlength="2000"
                    placeholder="Cuenta el estado, medidas, tiempo de uso y cualquier detalle útil. Mientras más claro, más rápido encuentra dueño."
                    [attr.aria-invalid]="form.controls.descripcion.invalid && form.controls.descripcion.touched" aria-describedby="descripcion-error"></textarea>
                  <div class="flex justify-between">
                    <app-error-campo [control]="form.controls.descripcion" etiqueta="La descripción" idError="descripcion-error" />
                    <span class="ml-auto ayuda">{{ form.controls.descripcion.value.length }}/2000</span>
                  </div>
                </div>

                @if (modoActual() !== 'Donacion') {
                  <div class="campo">
                    <label class="etiqueta" for="precio">{{ modoActual() === 'Compra' ? 'Precio de venta' : 'Valor de referencia (opcional)' }}</label>
                    <div class="relative">
                      <span class="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 font-semibold text-tenue">$</span>
                      <input id="precio" type="number" inputmode="numeric" min="0" step="1000" class="entrada pl-8 text-base" formControlName="precioReferenciaCop"
                        placeholder="0" aria-describedby="precio-ayuda precio-error" [attr.aria-invalid]="form.controls.precioReferenciaCop.invalid && form.controls.precioReferenciaCop.touched" />
                    </div>
                    <p id="precio-ayuda" class="ayuda">
                      {{ modoActual() === 'Compra' ? 'En pesos colombianos. El pago se acuerda directamente entre ustedes.' : 'Ayuda a valorar el trueke; no es obligatorio.' }}
                    </p>
                    <app-error-campo [control]="form.controls.precioReferenciaCop" etiqueta="El precio" idError="precio-error" />
                  </div>
                }
              </div>
            }

            <!-- ========== 3. FOTOS ========== -->
            @case (2) {
              <div class="animate-aparecer">
                <h2 class="text-xl font-bold">Agrega fotos</h2>
                <p class="mt-1 text-sm text-tenue">Hasta {{ config.maxImagenes() }} fotos. La primera será la portada. Las publicaciones con fotos reciben muchas más solicitudes.</p>

                @if (!config.subidasHabilitadas()) {
                  <div class="mt-5 flex items-start gap-3 rounded-2xl bg-sol-50 p-4 text-sm dark:bg-sol-500/10">
                    <app-icono nombre="info" class="shrink-0 text-sol-600" />
                    <p>La subida de fotos no está disponible en este momento. Puedes publicar sin fotos y agregarlas después editando la publicación.</p>
                  </div>
                } @else {
                  <label
                    class="mt-5 flex cursor-pointer flex-col items-center justify-center rounded-tarjeta border-2 border-dashed p-8 text-center transition"
                    [class]="arrastrando() ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/40' : 'border-borde bg-superficie hover:border-bosque-300'"
                    [class.pointer-events-none]="fotos().length >= config.maxImagenes()"
                    [class.opacity-50]="fotos().length >= config.maxImagenes()"
                    (dragover)="$event.preventDefault(); arrastrando.set(true)"
                    (dragleave)="arrastrando.set(false)"
                    (drop)="soltar($event)"
                  >
                    <span class="grid size-14 place-items-center rounded-2xl bg-bosque-100 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200"><app-icono nombre="camara" [tamano]="28" /></span>
                    <span class="mt-3 font-semibold">Arrastra tus fotos aquí o <span class="text-bosque-700 underline dark:text-bosque-300">elígelas</span></span>
                    <span class="mt-1 text-xs text-tenue">JPG, PNG o WebP · las reducimos automáticamente</span>
                    <input type="file" class="sr-only" accept="image/jpeg,image/png,image/webp" multiple (change)="elegir($event)" [disabled]="fotos().length >= config.maxImagenes()" />
                  </label>
                }

                @if (fotos().length) {
                  <ul class="mt-5 grid grid-cols-3 gap-3 sm:grid-cols-5">
                    @for (f of fotos(); track f.clave; let i = $index) {
                      <li class="group relative aspect-square overflow-hidden rounded-2xl border-2" [class]="i === 0 ? 'border-bosque-500' : 'border-borde'">
                        <img [src]="f.vista" alt="" class="size-full object-cover" referrerpolicy="no-referrer" />
                        @if (i === 0) {
                          <span class="absolute top-1.5 left-1.5 insignia bg-bosque-600 text-white">Portada</span>
                        }
                        @if (f.progreso < 100 && !f.error) {
                          <div class="absolute inset-0 grid place-items-center bg-black/40">
                            <div class="h-1.5 w-3/4 overflow-hidden rounded-full bg-white/30"><div class="h-full bg-white transition-all" [style.width.%]="f.progreso"></div></div>
                          </div>
                        }
                        @if (f.error) {
                          <div class="absolute inset-0 grid place-items-center bg-tierra-700/80 p-2 text-center text-[11px] font-semibold text-white">{{ f.error }}</div>
                        }
                        <div class="absolute inset-x-1.5 bottom-1.5 flex justify-between opacity-100 transition sm:opacity-0 sm:group-hover:opacity-100 sm:group-focus-within:opacity-100">
                          @if (i > 0 && !f.error) {
                            <button type="button" class="grid size-8 place-items-center rounded-full bg-white/90 text-bosque-900 shadow" (click)="hacerPortada(i)" aria-label="Usar como portada">
                              <app-icono nombre="estrella" [tamano]="14" />
                            </button>
                          } @else {
                            <span></span>
                          }
                          <button type="button" class="grid size-8 place-items-center rounded-full bg-white/90 text-tierra-600 shadow" (click)="quitar(i)" aria-label="Quitar foto">
                            <app-icono nombre="basura" [tamano]="14" />
                          </button>
                        </div>
                      </li>
                    }
                  </ul>
                }
              </div>
            }

            <!-- ========== 4. UBICACIÓN ========== -->
            @case (3) {
              <div class="animate-aparecer space-y-5">
                <div>
                  <app-selector-municipio id="pub-ubicacion" formControlName="municipioCodigo" (cambio)="municipio.set($event)"
                    [invalido]="form.controls.municipioCodigo.invalid && form.controls.municipioCodigo.touched" />
                  <app-error-campo [control]="form.controls.municipioCodigo" etiqueta="El municipio" />
                </div>
                <div class="campo">
                  @if (esBogota()) {
                    <label class="etiqueta" for="localidad">Localidad</label>
                    <select id="localidad" class="entrada" formControlName="localidad" aria-describedby="localidad-error">
                      <option value="" disabled>Elige la localidad</option>
                      @for (l of localidades; track l) {
                        <option [value]="l">{{ l }}</option>
                      }
                    </select>
                  } @else {
                    <label class="etiqueta" for="localidad">Barrio, sector o vereda</label>
                    <input id="localidad" class="entrada" formControlName="localidad" maxlength="60" placeholder="Ej: El Poblado, Centro, vereda La Esperanza" aria-describedby="localidad-error" />
                  }
                  <app-error-campo [control]="form.controls.localidad" etiqueta="La localidad" idError="localidad-error" />
                </div>
                <div>
                  <div class="flex flex-wrap items-center justify-between gap-2">
                    <p class="etiqueta">Punto de entrega (opcional)</p>
                    <div class="flex gap-2">
                      <button type="button" class="btn btn-secundario btn-sm" (click)="usarMiUbicacion()"><app-icono nombre="mira" [tamano]="14" />Mi ubicación</button>
                      @if (ubicacion()) {
                        <button type="button" class="btn btn-fantasma btn-sm" (click)="ubicacion.set(null)">Quitar</button>
                      }
                    </div>
                  </div>
                  <p class="ayuda mt-1">Toca el mapa o arrastra el marcador. En público solo se muestra una zona aproximada; el punto exacto lo ve quien tenga una solicitud aceptada.</p>
                  <app-mapa class="mt-3 h-80 rounded-tarjeta border border-borde" [seleccion]="true" [ubicacion]="ubicacion()" [centro]="centroMapa()" [zoom]="14"
                    (ubicacionCambiada)="ubicacion.set($event)" etiqueta="Elige el punto de entrega" />
                </div>
              </div>
            }

            <!-- ========== 5. REVISAR ========== -->
            @case (4) {
              <div class="animate-aparecer">
                <h2 class="text-xl font-bold">Así se verá tu publicación</h2>
                <div class="mt-5 grid grid-cols-1 gap-6 sm:grid-cols-[16rem_1fr]">
                  <div class="tarjeta overflow-hidden">
                    <app-imagen-publicacion class="aspect-[4/3]" [src]="fotosListas()[0]" [miniatura]="true" [modo]="modoActual()" [categoriaId]="categoriaActual()" />
                    <div class="p-4">
                      <app-insignia-modo [modo]="modoActual()" />
                      <p class="mt-2 font-display font-bold">{{ form.controls.titulo.value }}</p>
                      <p class="mt-1 text-sm text-tenue">{{ form.controls.localidad.value }} · {{ municipio()?.nombre }}</p>
                    </div>
                  </div>
                  <dl class="space-y-3 text-sm">
                    <div><dt class="text-tenue">Categoría</dt><dd class="font-semibold">{{ nombreCategoria() }}</dd></div>
                    <div>
                      <dt class="text-tenue">Estado</dt>
                      <dd class="font-semibold">{{ etiquetaCondicion() }}@if (requiereDetalle() && form.controls.detalleCondicion.value) {<span class="block font-normal text-tenue">{{ form.controls.detalleCondicion.value }}</span>}</dd>
                    </div>
                    <div><dt class="text-tenue">Ubicación</dt><dd class="font-semibold">{{ form.controls.localidad.value }}, {{ municipio()?.nombre }} ({{ municipio()?.departamento }})</dd></div>
                    @if (modoActual() !== 'Donacion' && form.controls.precioReferenciaCop.value) {
                      <div><dt class="text-tenue">{{ modoActual() === 'Compra' ? 'Precio' : 'Valor de referencia' }}</dt><dd class="font-semibold">{{ form.controls.precioReferenciaCop.value | cop }}</dd></div>
                    }
                    <div><dt class="text-tenue">Fotos</dt><dd class="font-semibold">{{ fotosListas().length }}</dd></div>
                    <div><dt class="text-tenue">Punto de entrega</dt><dd class="font-semibold">{{ ubicacion() ? 'Marcado en el mapa' : 'Solo localidad' }}</dd></div>
                    <div><dt class="text-tenue">Descripción</dt><dd class="line-clamp-4 whitespace-pre-line">{{ form.controls.descripcion.value }}</dd></div>
                  </dl>
                </div>
                <div class="mt-6 flex items-start gap-3 rounded-2xl bg-bosque-50 p-4 text-sm dark:bg-bosque-950/50">
                  <app-icono nombre="moneda" class="shrink-0 text-sol-500" />
                  <p>Cuando completes este {{ info[modoActual()].etiqueta.toLowerCase() }}, tú y la otra persona ganarán <strong>+{{ info[modoActual()].puntos }} Eco-Puntos</strong>.</p>
                </div>
                @if (error()) {
                  <p class="mt-4 rounded-xl bg-tierra-50 p-3 text-sm text-tierra-700 dark:bg-tierra-700/20 dark:text-tierra-100" role="alert">{{ error() }}</p>
                }
              </div>
            }
          }

          <!-- Navegación -->
          <div class="sticky bottom-16 z-10 mt-10 flex items-center justify-between gap-3 border-t border-borde bg-fondo/90 py-4 backdrop-blur lg:bottom-0">
            @if (paso() > 0) {
              <button type="button" class="btn btn-secundario" (click)="anterior()"><app-icono nombre="izquierda" [tamano]="16" />Atrás</button>
            } @else {
              <span></span>
            }
            @if (paso() < pasos.length - 1) {
              <button type="button" class="btn btn-primario btn-lg" (click)="siguiente()" [disabled]="subiendo()">
                Continuar <app-icono nombre="derecha" [tamano]="18" />
              </button>
            } @else {
              <button type="submit" class="btn btn-primario btn-lg" [disabled]="enviando() || subiendo()">
                <app-icono nombre="check" [tamano]="18" />{{ enviando() ? 'Guardando…' : id() ? 'Guardar cambios' : 'Publicar ahora' }}
              </button>
            }
          </div>
        </form>
      }
    </div>
  `,
})
export default class Publicar {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CatalogoApi);
  private readonly subidas = inject(SubidasService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionService);
  protected readonly config = inject(ConfigService);

  /** Presente al editar (`/publicacion/:id/editar`). */
  readonly id = input<string>();

  protected readonly pasos = PASOS;
  protected readonly modos = MODOS;
  protected readonly info = INFO_MODO;
  protected readonly localidades = LOCALIDADES;
  protected readonly condiciones = CONDICIONES;
  protected readonly iconoCategoria = iconoCategoria;
  protected readonly bordeModo: Record<string, string> = {
    Trueke: 'border-bosque-500 bg-bosque-50/60 dark:bg-bosque-900/40',
    Compra: 'border-cielo-600 bg-cielo-50/60 dark:bg-cielo-700/20',
    Donacion: 'border-tierra-500 bg-tierra-50/60 dark:bg-tierra-700/20',
  };
  protected readonly solidoModo: Record<string, string> = { Trueke: 'bg-bosque-600', Compra: 'bg-cielo-600', Donacion: 'bg-tierra-600' };

  protected readonly paso = signal(0);
  protected readonly pasoMaximo = signal(0);
  protected readonly fotos = signal<Foto[]>([]);
  protected readonly arrastrando = signal(false);
  protected readonly ubicacion = signal<[number, number] | null>(null);
  protected readonly enviando = signal(false);
  protected readonly cargandoEdicion = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Municipio elegido (con coordenadas para centrar el mapa). */
  protected readonly municipio = signal<MunicipioDto | null>(null);

  protected readonly form = this.fb.group({
    modo: this.fb.control<ModoDto>('Trueke'),
    titulo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    categoriaId: this.fb.control<number | null>(null, Validators.required),
    condicion: this.fb.control<CondicionDto | null>(null, Validators.required),
    detalleCondicion: ['', [Validators.maxLength(300)]],
    descripcion: ['', [Validators.required, Validators.maxLength(2000)]],
    precioReferenciaCop: this.fb.control<number | null>(null, [Validators.min(0), Validators.max(1_000_000_000)]),
    municipioCodigo: [this.sesion.usuario()?.municipioCodigo ?? CODIGO_BOGOTA, [Validators.required, Validators.pattern(/^\d{5}$/)]],
    localidad: [this.sesion.usuario()?.localidad ?? '', [Validators.required, Validators.minLength(2), Validators.maxLength(60)]],
  });

  protected readonly categorias = toSignal(this.api.categorias$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly modoActual = toSignal(this.form.controls.modo.valueChanges, { initialValue: this.form.controls.modo.value });
  protected readonly categoriaActual = toSignal(this.form.controls.categoriaId.valueChanges, { initialValue: null });
  protected readonly condicionActual = toSignal(this.form.controls.condicion.valueChanges, { initialValue: null });
  protected readonly requiereDetalle = computed(() => infoCondicion(this.condicionActual()).requiereDetalle && !!this.condicionActual());
  protected readonly etiquetaCondicion = computed(() => (this.condicionActual() ? infoCondicion(this.condicionActual()).etiqueta : ''));
  private readonly municipioActual = toSignal(this.form.controls.municipioCodigo.valueChanges, { initialValue: this.form.controls.municipioCodigo.value });
  /** Último municipio visto: si cambia por elección del usuario se limpia la localidad (no al cargar una edición). */
  private municipioPrevio = this.form.controls.municipioCodigo.value;
  protected readonly esBogota = computed(() => this.municipioActual() === CODIGO_BOGOTA);
  private readonly localidadActual = toSignal(this.form.controls.localidad.valueChanges, { initialValue: this.form.controls.localidad.value });
  protected readonly nombreCategoria = computed(() => this.categorias().find((c) => c.id === this.categoriaActual())?.nombre ?? '');
  protected readonly centroMapa = computed<[number, number] | null>(() => {
    if (this.ubicacion()) return this.ubicacion();
    if (this.esBogota() && CENTRO_LOCALIDAD[this.localidadActual()]) return CENTRO_LOCALIDAD[this.localidadActual()]!;
    const m = this.municipio();
    return m?.latitud != null && m.longitud != null ? [m.latitud, m.longitud] : null;
  });
  protected readonly subiendo = computed(() => this.fotos().some((f) => f.progreso < 100 && !f.error));
  protected readonly fotosListas = computed(() => this.fotos().filter((f) => f.url && !f.error).map((f) => f.url!));

  private readonly vistasLocales = new Set<string>();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.vistasLocales.forEach((u) => URL.revokeObjectURL(u)));

    // El precio es obligatorio solo para Compra (regla del backend).
    effect(() => {
      const modo = this.modoActual();
      untracked(() => {
        const c = this.form.controls.precioReferenciaCop;
        c.setValidators(
          modo === 'Compra' ? [Validators.required, Validators.min(1), Validators.max(1_000_000_000)] : [Validators.min(0), Validators.max(1_000_000_000)],
        );
        if (modo === 'Donacion') c.setValue(null);
        c.updateValueAndValidity();
      });
    });

    // El detalle del estado es obligatorio para "con detalles", "reparado" y "para repuestos" (regla del backend).
    effect(() => {
      const requiere = this.requiereDetalle();
      untracked(() => {
        const c = this.form.controls.detalleCondicion;
        c.setValidators(requiere ? [Validators.required, Validators.minLength(5), Validators.maxLength(300)] : [Validators.maxLength(300)]);
        c.updateValueAndValidity();
      });
    });

    // Al cambiar de municipio, la localidad de Bogotá (lista) deja de aplicar y viceversa.
    effect(() => {
      const actual = this.municipioActual();
      untracked(() => {
        if (this.municipioPrevio && actual !== this.municipioPrevio) {
          this.form.controls.localidad.setValue('');
          this.ubicacion.set(null);
        }
        this.municipioPrevio = actual;
      });
    });

    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.cargarParaEditar(id));
    });
  }

  private async cargarParaEditar(id: string): Promise<void> {
    this.cargandoEdicion.set(true);
    try {
      const p = await firstValueFrom(this.api.obtener(id));
      if (!p.esMia) {
        this.avisos.error('Solo puedes editar tus propias publicaciones.');
        await this.router.navigate(['/publicacion', id]);
        return;
      }
      this.form.patchValue({
        modo: (p.modo as ModoDto) ?? 'Trueke',
        titulo: p.titulo ?? '',
        categoriaId: p.categoria?.id ?? null,
        condicion: (p.condicion as CondicionDto) ?? null,
        detalleCondicion: p.detalleCondicion ?? '',
        descripcion: p.descripcion ?? '',
        precioReferenciaCop: p.precioReferenciaCop ?? null,
        municipioCodigo: p.municipioCodigo ?? CODIGO_BOGOTA,
        localidad: p.localidad ?? '',
      });
      this.municipioPrevio = p.municipioCodigo ?? CODIGO_BOGOTA;
      this.fotos.set((p.imagenes ?? []).map((url) => ({ clave: url, vista: urlPublica(url), url, progreso: 100 })));
      if (p.latitud != null && p.longitud != null && !p.coordenadasAproximadas) this.ubicacion.set([p.latitud, p.longitud]);
      this.pasoMaximo.set(PASOS.length - 1);
      this.paso.set(1);
    } catch (e) {
      this.avisos.error(mensajeDe(e));
      await this.router.navigate(['/mis-publicaciones']);
    } finally {
      this.cargandoEdicion.set(false);
    }
  }

  protected irA(i: number): void {
    if (i <= this.pasoMaximo()) this.paso.set(i);
  }

  protected anterior(): void {
    this.paso.update((p) => Math.max(0, p - 1));
  }

  protected siguiente(): void {
    if (!this.pasoValido(this.paso())) return;
    const nuevo = Math.min(PASOS.length - 1, this.paso() + 1);
    this.paso.set(nuevo);
    this.pasoMaximo.update((m) => Math.max(m, nuevo));
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  private pasoValido(paso: number): boolean {
    const c = this.form.controls;
    const marcar = (...ctrls: (typeof c)[keyof typeof c][]) => {
      ctrls.forEach((x) => x.markAsTouched());
      return ctrls.every((x) => x.valid);
    };
    switch (paso) {
      case 1:
        return marcar(c.titulo, c.categoriaId, c.condicion, c.detalleCondicion, c.descripcion, c.precioReferenciaCop);
      case 3:
        return marcar(c.municipioCodigo, c.localidad);
      default:
        return true;
    }
  }

  protected elegir(e: Event): void {
    const input = e.target as HTMLInputElement;
    void this.agregar(Array.from(input.files ?? []));
    input.value = '';
  }

  protected soltar(e: DragEvent): void {
    e.preventDefault();
    this.arrastrando.set(false);
    void this.agregar(Array.from(e.dataTransfer?.files ?? []));
  }

  private async agregar(archivos: File[]): Promise<void> {
    const libres = this.config.maxImagenes() - this.fotos().length;
    if (archivos.length > libres) this.avisos.info(`Solo caben ${libres} fotos más.`);
    for (const original of archivos.slice(0, Math.max(0, libres))) {
      const clave = crypto.randomUUID();
      const vista = URL.createObjectURL(original);
      this.vistasLocales.add(vista);
      this.fotos.update((l) => [...l, { clave, vista, progreso: 0, local: true }]);
      try {
        const archivo = await comprimirImagen(original);
        const invalido = this.subidas.validar(archivo);
        if (invalido) throw new Error(invalido);
        this.subidas.subir(archivo).subscribe({
          next: (p) => this.actualizarFoto(clave, { progreso: p.progreso, url: p.url }),
          error: (err) => this.actualizarFoto(clave, { error: mensajeDe(err) === 'Ocurrió un error inesperado.' ? 'No se pudo subir' : mensajeDe(err) }),
        });
      } catch (err) {
        this.actualizarFoto(clave, { error: (err as Error).message || 'Archivo no válido' });
      }
    }
  }

  private actualizarFoto(clave: string, cambios: Partial<Foto>): void {
    this.fotos.update((l) => l.map((f) => (f.clave === clave ? { ...f, ...cambios } : f)));
  }

  protected quitar(i: number): void {
    this.fotos.update((l) => l.filter((_, j) => j !== i));
  }

  protected hacerPortada(i: number): void {
    this.fotos.update((l) => [l[i]!, ...l.filter((_, j) => j !== i)]);
  }

  protected usarMiUbicacion(): void {
    navigator.geolocation?.getCurrentPosition(
      (pos) => this.ubicacion.set([Number(pos.coords.latitude.toFixed(6)), Number(pos.coords.longitude.toFixed(6))]),
      () => this.avisos.error('No pudimos obtener tu ubicación', 'Revisa los permisos del navegador o marca el punto en el mapa.'),
      { timeout: 10_000 },
    );
  }

  protected async publicar(): Promise<void> {
    for (const p of [1, 3]) {
      if (!this.pasoValido(p)) {
        this.paso.set(p);
        return;
      }
    }
    const v = this.form.getRawValue();
    const u = this.ubicacion();
    const cuerpo: CrearPublicacionRequest = {
      titulo: v.titulo.trim(),
      descripcion: v.descripcion.trim(),
      categoriaId: v.categoriaId ?? undefined,
      modo: v.modo,
      condicion: v.condicion!,
      detalleCondicion: this.requiereDetalle() || v.detalleCondicion.trim() ? v.detalleCondicion.trim() || null : null,
      municipioCodigo: v.municipioCodigo,
      localidad: v.localidad.trim(),
      precioReferenciaCop: v.modo === 'Donacion' ? null : v.precioReferenciaCop || null,
      latitud: u?.[0] ?? null,
      longitud: u?.[1] ?? null,
      imagenes: this.fotosListas(),
    };
    this.enviando.set(true);
    this.error.set(null);
    try {
      const id = this.id();
      const p = await firstValueFrom(id ? this.api.editar(id, cuerpo) : this.api.crear(cuerpo));
      this.avisos.exito(id ? 'Cambios guardados' : '¡Tu publicación ya está visible!', id ? undefined : 'Te avisaremos cuando alguien la solicite.');
      await this.router.navigate(['/publicacion', p.id]);
    } catch (e) {
      const errores = erroresDeCampos(e);
      if (aplicarErroresServidor(this.form.controls, errores)) {
        this.paso.set(errores['localidad'] || errores['municipioCodigo'] ? 3 : 1);
      } else {
        this.error.set(mensajeDe(e));
      }
    } finally {
      this.enviando.set(false);
    }
  }
}
