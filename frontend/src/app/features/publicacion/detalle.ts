import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, map, of } from 'rxjs';
import { ETIQUETA_ESTADO_PUBLICACION, infoCondicion, INFO_MODO, type ModoDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { IntercambiosApi } from '../../core/api/intercambios.api';
import { AvisosService } from '../../core/avisos.service';
import { FavoritosService } from '../../core/favoritos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { urlPublica } from '../../shared/imagenes';
import { CopPipe, FechaPipe, HacePipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Denunciar } from '../../shared/ui/denunciar';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Icono } from '../../shared/ui/icono';
import { ImagenPublicacion } from '../../shared/ui/imagen-publicacion';
import { InsigniaModo } from '../../shared/ui/insignia-modo';
import { Mapa } from '../../shared/ui/mapa';
import { Modal } from '../../shared/ui/modal';
import { Comentarios } from './comentarios';
import { Volver } from '../../shared/ui/volver';

@Component({
  imports: [Volver, 
    RouterLink,
    FormsModule,
    Icono,
    ImagenPublicacion,
    InsigniaModo,
    Avatar,
    Estrellas,
    Mapa,
    Modal,
    Denunciar,
    EstadoVacio,
    Comentarios,
    CopPipe,
    HacePipe,
    FechaPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (recurso.isLoading() && !p()) {
      <div class="contenedor grid grid-cols-1 gap-8 py-8 lg:grid-cols-[1.3fr_1fr]" aria-busy="true">
        <div class="esqueleto aspect-[4/3] rounded-tarjeta"></div>
        <div class="space-y-4">
          <div class="esqueleto h-6 w-1/3"></div>
          <div class="esqueleto h-10 w-4/5"></div>
          <div class="esqueleto h-24 w-full"></div>
          <div class="esqueleto h-14 w-full rounded-full"></div>
        </div>
      </div>
    } @else if (recurso.error()) {
      <app-estado-vacio icono="buscar" titulo="Esta publicación no está disponible" descripcion="Puede que se haya retirado o que el enlace no sea correcto.">
        <a routerLink="/explorar" class="btn btn-primario">Ver el catálogo</a>
      </app-estado-vacio>
    } @else if (p(); as p) {
      <div class="contenedor py-6 sm:py-8">
        <nav class="mb-5 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-tenue" aria-label="Ruta de navegación">
          <app-volver respaldo="/explorar" />
          <span class="hidden sm:inline" aria-hidden="true">·</span>
          <a routerLink="/explorar" class="hidden hover:text-tinta sm:inline">Catálogo</a>
          <span class="hidden sm:inline" aria-hidden="true">/</span>
          <a routerLink="/explorar" [queryParams]="{ categoriaId: p.categoria?.id }" class="hover:text-tinta max-sm:ml-auto">{{ p.categoria?.nombre }}</a>
        </nav>

        @if (p.oculta) {
          <div class="mb-5 flex items-start gap-3 rounded-2xl border border-tierra-500/30 bg-tierra-50 p-4 text-sm dark:bg-tierra-700/20" role="alert">
            <app-icono nombre="ojoNo" class="text-tierra-600" />
            <div><strong>Publicación oculta por moderación.</strong> {{ p.motivoOcultamiento }}</div>
          </div>
        }

        <div class="grid grid-cols-1 gap-8 lg:grid-cols-[1.3fr_1fr]">
          <!-- Galería -->
          <div>
            <div class="relative overflow-hidden rounded-tarjeta bg-superficie-2 shadow-suave">
              <app-imagen-publicacion class="aspect-[4/3] w-full" [src]="imagenes()[indice()]" [alt]="p.titulo ?? ''" [modo]="p.modo" [categoriaId]="p.categoria?.id" />
              @if (imagenes().length > 1) {
                <button type="button" class="absolute top-1/2 left-3 grid size-10 -translate-y-1/2 place-items-center rounded-full bg-white/90 text-bosque-900 shadow-lg hover:bg-white" (click)="mover(-1)" aria-label="Foto anterior">
                  <app-icono nombre="izquierda" />
                </button>
                <button type="button" class="absolute top-1/2 right-3 grid size-10 -translate-y-1/2 place-items-center rounded-full bg-white/90 text-bosque-900 shadow-lg hover:bg-white" (click)="mover(1)" aria-label="Foto siguiente">
                  <app-icono nombre="derecha" />
                </button>
                <span class="absolute right-3 bottom-3 insignia bg-black/60 text-white">{{ indice() + 1 }} / {{ imagenes().length }}</span>
              }
            </div>
            @if (imagenes().length > 1) {
              <div class="mt-3 flex gap-2 overflow-x-auto pb-1">
                @for (img of imagenes(); track img; let i = $index) {
                  <button type="button" class="size-20 shrink-0 overflow-hidden rounded-xl border-2 transition"
                    [class]="i === indice() ? 'border-bosque-500' : 'border-transparent opacity-70 hover:opacity-100'"
                    (click)="indice.set(i)" [attr.aria-label]="'Ver foto ' + (i + 1)">
                    <img [src]="urlPublica(img)" alt="" class="size-full object-cover" loading="lazy" referrerpolicy="no-referrer" />
                  </button>
                }
              </div>
            }

            <section class="mt-8" aria-labelledby="titulo-descripcion">
              <h2 id="titulo-descripcion" class="text-lg font-bold">Descripción</h2>
              <p class="mt-3 leading-relaxed break-words whitespace-pre-line text-tinta/90">{{ p.descripcion }}</p>
            </section>

            @if (p.latitud !== null && p.latitud !== undefined && p.longitud !== null && p.longitud !== undefined) {
              <section class="mt-8" aria-labelledby="titulo-ubicacion">
                <h2 id="titulo-ubicacion" class="text-lg font-bold">Ubicación</h2>
                <p class="mt-1 text-sm text-tenue">
                  {{ p.coordenadasAproximadas ? 'Zona aproximada en ' + p.localidad + '. La ubicación exacta se comparte al aceptar la solicitud.' : 'Ubicación exacta de entrega.' }}
                </p>
                <app-mapa class="mt-3 h-72 rounded-tarjeta border border-borde"
                  [area]="p.coordenadasAproximadas ? { lat: p.latitud, lon: p.longitud, radioM: 700 } : null"
                  [puntos]="p.coordenadasAproximadas ? [] : [{ id: p.id!, lat: p.latitud, lon: p.longitud, titulo: p.titulo ?? '', modo: p.modo }]"
                  [centro]="[p.latitud, p.longitud]" [zoom]="14" etiqueta="Ubicación de la publicación" />
              </section>
            }

            @if (escritorio()) {
              <div class="mt-8">
                <app-comentarios [publicacionId]="p.id!" [propietarioId]="p.propietario?.id" />
              </div>
            }
          </div>

          <!-- Panel de acción -->
          <aside class="space-y-5 lg:sticky lg:top-24 lg:self-start">
            <div class="tarjeta p-6">
              <div class="flex flex-wrap items-center gap-2">
                <app-insignia-modo [modo]="p.modo" />
                @if (p.destacada) {
                  <span class="insignia-sol"><app-icono nombre="destello" [tamano]="12" />Destacada</span>
                }
                <span class="insignia-neutra" [class.!bg-bosque-100]="p.estado === 'Disponible'" [class.!text-bosque-800]="p.estado === 'Disponible'">
                  {{ etiquetaEstado[p.estado ?? ''] ?? p.estado }}
                </span>
                <span [class]="condicion().clase"><app-icono nombre="etiqueta" [tamano]="12" />{{ condicion().etiqueta }}</span>
              </div>
              <h1 class="mt-4 text-2xl leading-tight font-extrabold break-words sm:text-3xl">{{ p.titulo }}</h1>

              <div class="mt-4">
                @if (p.modo === 'Compra' && p.precioReferenciaCop) {
                  <p class="font-display text-3xl font-extrabold text-cielo-700 dark:text-cielo-100">{{ p.precioReferenciaCop | cop }}</p>
                } @else if (p.modo === 'Donacion') {
                  <p class="font-display text-3xl font-extrabold text-tierra-600 dark:text-tierra-500">Gratis · donación</p>
                } @else {
                  <p class="font-display text-2xl font-extrabold text-bosque-700 dark:text-bosque-300">Disponible para trueke</p>
                  @if (p.precioReferenciaCop) {
                    <p class="text-sm text-tenue">Valor de referencia: {{ p.precioReferenciaCop | cop }}</p>
                  }
                }
                @if (p.estado === 'Disponible' && (p.interesados ?? 0) > 0) {
                  <p class="mt-2 flex items-center gap-1.5 text-sm font-semibold text-tierra-600 dark:text-tierra-500">
                    <app-icono nombre="usuarios" [tamano]="16" />
                    {{ p.interesados === 1 ? '1 persona interesada' : p.interesados + ' personas interesadas' }}
                  </p>
                }
              </div>

              <dl class="mt-5 grid grid-cols-2 gap-3 text-sm">
                <div class="rounded-xl bg-superficie-2 p-3">
                  <dt class="flex items-center gap-1 text-xs text-tenue"><app-icono nombre="pin" [tamano]="12" />Ubicación</dt>
                  <dd class="mt-0.5 font-semibold">{{ p.localidad }}<span class="block text-xs font-normal text-tenue">{{ p.municipio }}</span></dd>
                </div>
                <div class="rounded-xl bg-superficie-2 p-3">
                  <dt class="flex items-center gap-1 text-xs text-tenue"><app-icono nombre="calendario" [tamano]="12" />Publicado</dt>
                  <dd class="mt-0.5 font-semibold" [title]="p.fechaPublicacion | fecha">{{ p.fechaPublicacion | hace }}</dd>
                </div>
                <div class="col-span-2 rounded-xl bg-superficie-2 p-3">
                  <dt class="flex items-center gap-1 text-xs text-tenue"><app-icono nombre="etiqueta" [tamano]="12" />Estado del producto</dt>
                  <dd class="mt-0.5 font-semibold">{{ condicion().etiqueta }} <span class="font-normal text-tenue">· {{ condicion().descripcion }}</span></dd>
                  @if (p.detalleCondicion) {
                    <dd class="mt-1 whitespace-pre-line text-tenue">{{ p.detalleCondicion }}</dd>
                  }
                </div>
              </dl>

              <div class="mt-6 space-y-2.5">
                @if (p.esMia) {
                  <a [routerLink]="['/intercambios']" [queryParams]="{ tab: 'recibidas' }" class="btn btn-primario btn-lg w-full">
                    <app-icono nombre="apreton" [tamano]="20" /> Ver solicitudes recibidas
                  </a>
                  @if (p.estado === 'Disponible') {
                    <div class="grid gap-2" [class.grid-cols-2]="!p.interesados">
                      @if (!p.interesados) {
                        <a [routerLink]="['/publicacion', p.id, 'editar']" class="btn btn-secundario"><app-icono nombre="editar" [tamano]="16" />Editar</a>
                      }
                      <button type="button" class="btn btn-secundario" (click)="cancelarAbierto.set(true)"><app-icono nombre="basura" [tamano]="16" />Retirar</button>
                    </div>
                    @if (p.interesados) {
                      <p class="text-center text-xs text-tenue">Ya hay personas interesadas: no se puede editar para que nadie reciba algo distinto de lo que pidió.</p>
                    }
                    @if (!p.destacada) {
                      @if ((sesion.usuario()?.destacadosGratisRestantes ?? 0) > 0) {
                        <button type="button" class="btn btn-sol w-full" (click)="destacarGratis()" [disabled]="trabajando()">
                          <app-icono nombre="destello" [tamano]="16" /> Destacar gratis ({{ sesion.usuario()?.destacadosGratisRestantes }} disponibles)
                        </button>
                      } @else {
                        <a routerLink="/eco-puntos" [queryParams]="{ destacar: p.id }" class="btn btn-sol w-full">
                          <app-icono nombre="destello" [tamano]="16" /> Destacar para llegar a más personas
                        </a>
                      }
                    }
                    <div class="grid grid-cols-2 gap-2">
                      <button type="button" class="btn btn-secundario" (click)="impulsar()" [disabled]="trabajando() || !puedeImpulsar()"
                        [title]="puedeImpulsar() ? 'Sube tu publicación al primer lugar de «Más recientes»' : 'Podrás impulsarla de nuevo ' + (p.proximoImpulsoUtc | hace)">
                        <app-icono nombre="cohete" [tamano]="16" />Impulsar ({{ puntosImpulsar() }} pts)
                      </button>
                      <a routerLink="/mis-publicaciones" [queryParams]="{ estadisticas: p.id }" class="btn btn-secundario">
                        <app-icono nombre="grafica" [tamano]="16" />{{ p.vistas ?? 0 }} vistas
                      </a>
                    </div>
                  }
                } @else if (p.estado === 'Disponible' && p.yaSolicite) {
                  <div class="rounded-xl bg-bosque-50 p-4 text-center text-sm dark:bg-bosque-900/40">
                    <p class="font-semibold">Ya enviaste tu solicitud</p>
                    <p class="mt-1 text-tenue">El dueño elegirá a quién entregarlo. Mientras tanto pueden conversar por el chat.</p>
                    <a routerLink="/intercambios" [queryParams]="{ tab: 'enviadas' }" class="btn btn-secundario btn-sm mt-3">Ver mi solicitud</a>
                  </div>
                } @else if (p.estado === 'Disponible') {
                  <button type="button" class="btn btn-primario btn-lg w-full" (click)="abrirSolicitud()">
                    <app-icono [nombre]="infoModo().icono" [tamano]="20" /> {{ infoModo().verbo }}
                  </button>
                  <p class="flex items-center justify-center gap-1.5 text-center text-xs text-tenue">
                    <app-icono nombre="moneda" [tamano]="14" class="text-sol-500" />
                    Ambos ganan Eco-Puntos al completar el intercambio
                  </p>
                } @else {
                  <p class="rounded-xl bg-superficie-2 p-4 text-center text-sm">
                    @switch (p.estado) {
                      @case ('EnNegociacion') { El dueño está concretando con otra persona. Guárdalo en favoritos: si no se concreta, vuelve a estar disponible. }
                      @case ('Intercambiada') { ¡Este objeto ya encontró un nuevo hogar! }
                      @default { Esta publicación ya no está disponible. }
                    }
                  </p>
                }

                <div class="flex gap-2">
                  @if (!p.esMia) {
                    <button type="button" class="btn btn-secundario flex-1" [attr.aria-pressed]="favorita()" (click)="favoritos.alternar(p.id!, favorita())">
                      <app-icono nombre="heart" [tamano]="16" [relleno]="favorita()" [class.text-tierra-600]="favorita()" />
                      {{ favorita() ? 'Guardado' : 'Guardar' }}
                    </button>
                  }
                  <button type="button" class="btn btn-secundario flex-1" (click)="compartir()">
                    <app-icono nombre="externo" [tamano]="16" /> Compartir
                  </button>
                </div>
              </div>
            </div>

            <!-- Propietario -->
            <a [routerLink]="['/usuarios', p.propietario?.id]" class="tarjeta-interactiva flex items-center gap-4 p-5">
              <app-avatar [nombre]="p.propietario?.nombre" [foto]="p.propietario?.fotoUrl" [tamano]="56" [verificado]="!!p.propietario?.verificado" />
              <div class="min-w-0 flex-1">
                <p class="text-xs text-tenue">Publicado por</p>
                <p class="truncate font-display font-bold">{{ p.propietario?.nombre }}</p>
                <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-tenue">
                  @if (p.propietario?.calificacionPromedio) {
                    <span class="flex items-center gap-1"><app-estrellas [valor]="p.propietario!.calificacionPromedio!" [tamano]="12" />({{ p.propietario!.totalCalificaciones }})</span>
                  }
                  <span class="flex items-center gap-1"><app-icono nombre="hoja" [tamano]="12" class="text-bosque-500" />Reputación {{ p.propietario?.reputacion?.toFixed(1) }}</span>
                  @if (p.propietario?.tipoCuenta && p.propietario.tipoCuenta !== 'Individual') {
                    <span class="insignia-sol">{{ p.propietario.tipoCuenta }}</span>
                  }
                </div>
              </div>
              <app-icono nombre="derecha" class="text-tenue" />
            </a>

            <div class="rounded-tarjeta border border-bosque-200 bg-bosque-50 p-5 text-sm dark:border-bosque-800 dark:bg-bosque-950/50">
              <p class="flex items-center gap-2 font-bold text-bosque-800 dark:text-bosque-200"><app-icono nombre="escudo" [tamano]="18" />Intercambia con confianza</p>
              <ul class="mt-2 space-y-1.5 text-bosque-900/80 dark:text-bosque-100/80">
                <li>• Coordina todo por el chat de Trueke; nunca compartas tus contraseñas.</li>
                <li>• Encuéntrate en lugares públicos y concurridos.</li>
                <li>• Revisa el objeto antes de confirmar la entrega.</li>
              </ul>
            </div>

            <div class="flex flex-wrap justify-center gap-4 text-xs">
              @if (sesion.autenticado() && !p.esMia) {
                <button type="button" class="flex items-center gap-1 text-tenue hover:text-tierra-600" (click)="denunciaAbierta.set(true)">
                  <app-icono nombre="bandera" [tamano]="14" /> Reportar publicación
                </button>
              }
              @if (sesion.esModerador()) {
                <button type="button" class="flex items-center gap-1 font-semibold text-agua-700 hover:underline dark:text-agua-100" (click)="moderar()">
                  <app-icono nombre="escudo" [tamano]="14" /> {{ p.oculta ? 'Volver a mostrar' : 'Ocultar (moderación)' }}
                </button>
              }
            </div>
          </aside>
        </div>

        @if (!escritorio()) {
          <div class="mt-8">
            <app-comentarios [publicacionId]="p.id!" [propietarioId]="p.propietario?.id" />
          </div>
        }
      </div>

      <!-- Solicitar -->
      <app-modal [(abierto)]="solicitudAbierta" [titulo]="infoModo().verbo" [subtitulo]="'A ' + (p.propietario?.nombre ?? '') + ' le llegará tu mensaje'">
        <form id="form-solicitud" (ngSubmit)="enviarSolicitud()" class="space-y-4">
          <div class="flex items-center gap-3 rounded-2xl bg-superficie-2 p-3">
            <app-imagen-publicacion class="size-16 shrink-0 rounded-xl" [src]="imagenes()[0]" [modo]="p.modo" [categoriaId]="p.categoria?.id" />
            <div class="min-w-0">
              <p class="truncate font-semibold">{{ p.titulo }}</p>
              <app-insignia-modo [modo]="p.modo" />
            </div>
          </div>
          <div class="campo">
            <label for="mensaje-solicitud" class="etiqueta">Tu mensaje</label>
            <textarea id="mensaje-solicitud" name="mensaje" class="entrada" maxlength="500" required [(ngModel)]="mensaje" [placeholder]="sugerencia()"></textarea>
            <p class="ayuda">{{ p.modo === 'Trueke' ? 'Cuenta qué ofreces a cambio.' : 'Preséntate y di cuándo podrías recogerlo.' }} No compartas tu teléfono: el chat se abre al aceptar.</p>
          </div>
          @if (errorSolicitud()) {
            <p class="error-campo" role="alert">{{ errorSolicitud() }}</p>
          }
        </form>
        <div pie class="flex justify-end gap-2 border-t border-borde p-4">
          <button type="button" class="btn btn-fantasma" (click)="solicitudAbierta.set(false)">Cancelar</button>
          <button type="submit" form="form-solicitud" class="btn btn-primario" [disabled]="!mensaje().trim() || trabajando()">
            <app-icono nombre="enviar" [tamano]="16" /> {{ trabajando() ? 'Enviando…' : 'Enviar solicitud' }}
          </button>
        </div>
      </app-modal>

      <!-- Retirar publicación -->
      <app-modal [(abierto)]="cancelarAbierto" titulo="Retirar publicación" subtitulo="Dejará de aparecer en el catálogo. Las solicitudes pendientes se cancelarán.">
        <form id="form-cancelar" (ngSubmit)="cancelar()" class="campo">
          <label for="motivo-cancelar" class="etiqueta">Motivo (opcional)</label>
          <input id="motivo-cancelar" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivoCancelar" placeholder="Por ejemplo: ya lo entregué por fuera" />
        </form>
        <div pie class="flex justify-end gap-2 border-t border-borde p-4">
          <button type="button" class="btn btn-fantasma" (click)="cancelarAbierto.set(false)">Volver</button>
          <button type="submit" form="form-cancelar" class="btn btn-peligro" [disabled]="trabajando()">Retirar publicación</button>
        </div>
      </app-modal>

      <!-- Moderación -->
      <app-modal [(abierto)]="ocultarAbierto" titulo="Ocultar publicación" subtitulo="El dueño verá el motivo.">
        <form id="form-ocultar" (ngSubmit)="confirmarOcultar()" class="campo">
          <label for="motivo-ocultar-pub" class="etiqueta">Motivo</label>
          <textarea id="motivo-ocultar-pub" name="motivo" class="entrada" maxlength="300" required [(ngModel)]="motivoOcultar"></textarea>
        </form>
        <div pie class="flex justify-end gap-2 border-t border-borde p-4">
          <button type="button" class="btn btn-fantasma" (click)="ocultarAbierto.set(false)">Cancelar</button>
          <button type="submit" form="form-ocultar" class="btn btn-peligro" [disabled]="motivoOcultar().trim().length < 3">Ocultar</button>
        </div>
      </app-modal>

      <app-denunciar [(abierto)]="denunciaAbierta" tipo="Publicacion" [objetivoId]="p.id!" />
    }
  `,
})
export default class Detalle {
  private readonly api = inject(CatalogoApi);
  private readonly intercambios = inject(IntercambiosApi);
  private readonly adminApi = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  private readonly cuentaApi = inject(CuentaApi);
  private readonly router = inject(Router);
  private readonly titulo = inject(Title);
  protected readonly sesion = inject(SesionService);
  protected readonly favoritos = inject(FavoritosService);

  readonly id = input.required<string>();
  /** Una sola instancia de comentarios: en escritorio va bajo la galería y en móvil al final. */
  protected readonly escritorio = toSignal(
    inject(BreakpointObserver).observe('(min-width: 1024px)').pipe(map((r) => r.matches)),
    { initialValue: window.matchMedia('(min-width: 1024px)').matches },
  );
  protected readonly etiquetaEstado = ETIQUETA_ESTADO_PUBLICACION;

  protected readonly recurso = rxResource({
    params: () => ({ id: this.id(), sesion: this.sesion.autenticado() }),
    stream: ({ params }) => this.api.obtener(params.id),
  });
  protected readonly p = computed(() => this.recurso.value());
  protected readonly imagenes = computed(() => this.p()?.imagenes ?? []);
  protected readonly urlPublica = urlPublica;
  protected readonly indice = signal(0);
  protected readonly infoModo = computed(() => INFO_MODO[(this.p()?.modo as ModoDto) ?? 'Trueke'] ?? INFO_MODO.Trueke);
  protected readonly favorita = computed(() => this.favoritos.esFavorita(this.p()?.id, this.p()?.esFavorita));
  protected readonly condicion = computed(() => infoCondicion(this.p()?.condicion));
  private readonly politica = toSignal(this.cuentaApi.politica$.pipe(catchError(() => of(null))), { initialValue: null });
  protected readonly puntosImpulsar = computed(() => this.politica()?.puntosImpulsar ?? 20);
  protected readonly puedeImpulsar = computed(() => {
    const proximo = this.p()?.proximoImpulsoUtc;
    return !proximo || Date.parse(proximo) <= Date.now();
  });
  protected readonly sugerencia = computed(() => {
    switch (this.p()?.modo) {
      case 'Compra':
        return '¡Hola! Me interesa comprarlo. ¿Podríamos encontrarnos esta semana?';
      case 'Donacion':
        return '¡Hola! Me serviría mucho. Puedo recogerlo cuando te quede bien.';
      default:
        return '¡Hola! Te ofrezco a cambio...';
    }
  });

  protected readonly solicitudAbierta = signal(false);
  protected readonly cancelarAbierto = signal(false);
  protected readonly denunciaAbierta = signal(false);
  protected readonly ocultarAbierto = signal(false);
  protected readonly mensaje = signal('');
  protected readonly motivoCancelar = signal('');
  protected readonly motivoOcultar = signal('');
  protected readonly errorSolicitud = signal<string | null>(null);
  protected readonly trabajando = signal(false);

  constructor() {
    effect(() => {
      const t = this.p()?.titulo;
      if (t) this.titulo.setTitle(`${t} · Trueke Bogotá Solidario`);
    });
    effect(() => {
      this.id();
      this.indice.set(0);
    });
  }

  protected mover(delta: number): void {
    const n = this.imagenes().length;
    this.indice.update((i) => (i + delta + n) % n);
  }

  protected abrirSolicitud(): void {
    if (!this.sesion.autenticado()) {
      void this.router.navigate(['/ingresar'], { queryParams: { volver: this.router.url } });
      return;
    }
    if (!this.sesion.correoVerificado()) {
      void this.router.navigate(['/verifica-tu-correo']);
      return;
    }
    this.errorSolicitud.set(null);
    this.solicitudAbierta.set(true);
  }

  protected async enviarSolicitud(): Promise<void> {
    const p = this.p();
    const mensaje = this.mensaje().trim();
    if (!p?.id || !mensaje) return;
    this.trabajando.set(true);
    this.errorSolicitud.set(null);
    try {
      await firstValueFrom(this.intercambios.solicitar({ publicacionId: p.id, mensaje }));
      this.solicitudAbierta.set(false);
      this.mensaje.set('');
      this.avisos.exito('¡Solicitud enviada!', 'Te avisaremos cuando te respondan.');
      await this.router.navigate(['/intercambios'], { queryParams: { tab: 'enviadas' } });
    } catch (e) {
      this.errorSolicitud.set(mensajeDe(e));
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async cancelar(): Promise<void> {
    const id = this.p()?.id;
    if (!id) return;
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.cancelar(id, this.motivoCancelar().trim() || null));
      this.cancelarAbierto.set(false);
      this.avisos.exito('Publicación retirada');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async destacarGratis(): Promise<void> {
    const id = this.p()?.id;
    if (!id) return;
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.destacarGratis(id));
      this.avisos.puntos('¡Publicación destacada!', 'Aparecerá primero en el catálogo.');
      this.sesion.recargarUsuario();
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async impulsar(): Promise<void> {
    const id = this.p()?.id;
    if (!id) return;
    if ((this.sesion.usuario()?.saldoEcoPuntos ?? 0) < this.puntosImpulsar()) {
      this.avisos.info(`Necesitas ${this.puntosImpulsar()} Eco-Puntos para impulsar`, 'Gánalos completando intercambios o recárgalos en Eco-Puntos.');
      return;
    }
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.impulsar(id));
      this.avisos.puntos('¡Publicación impulsada!', 'Ahora aparece entre las primeras de «Más recientes».');
      this.sesion.recargarUsuario();
      this.recurso.reload();
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.trabajando.set(false);
    }
  }

  protected async compartir(): Promise<void> {
    const url = window.location.href;
    const titulo = this.p()?.titulo ?? 'Trueke Bogotá Solidario';
    try {
      if (navigator.share) {
        await navigator.share({ title: titulo, text: `Mira esto en Trueke Bogotá Solidario: ${titulo}`, url });
      } else {
        await navigator.clipboard.writeText(url);
        this.avisos.exito('Enlace copiado');
      }
    } catch (e) {
      if ((e as DOMException)?.name !== 'AbortError') this.avisos.error('No se pudo compartir el enlace.');
    }
  }

  protected async moderar(): Promise<void> {
    const p = this.p();
    if (!p?.id) return;
    if (!p.oculta) {
      this.motivoOcultar.set('');
      this.ocultarAbierto.set(true);
      return;
    }
    try {
      await firstValueFrom(this.adminApi.mostrarPublicacion(p.id));
      this.avisos.exito('Publicación visible de nuevo');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error (o guió a activar 2FA).
    }
  }

  protected async confirmarOcultar(): Promise<void> {
    const id = this.p()?.id;
    const motivo = this.motivoOcultar().trim();
    if (!id || motivo.length < 3) return;
    try {
      await firstValueFrom(this.adminApi.ocultarPublicacion(id, motivo));
      this.ocultarAbierto.set(false);
      this.avisos.exito('Publicación ocultada');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    }
  }
}
