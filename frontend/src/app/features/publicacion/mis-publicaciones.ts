import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import { ETIQUETA_ESTADO_PUBLICACION, infoCondicion, type PublicacionDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CopPipe, FechaPipe, HacePipe, NumeroPipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { ImagenPublicacion } from '../../shared/ui/imagen-publicacion';
import { InsigniaModo } from '../../shared/ui/insignia-modo';
import { Modal } from '../../shared/ui/modal';
import { Volver } from '../../shared/ui/volver';

type Filtro = 'activas' | 'negociacion' | 'cerradas';

@Component({
  imports: [Volver, RouterLink, Icono, ImagenPublicacion, InsigniaModo, EstadoVacio, Modal, CopPipe, HacePipe, FechaPipe, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor py-8 sm:py-10">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <app-volver respaldo="/" class="mb-1" />
          <h1 class="text-3xl font-extrabold">Mis publicaciones</h1>
          <p class="mt-1 text-tenue">Administra lo que ofreces y mira cuántas personas lo ven.</p>
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
                    <span class="insignia-neutra">{{ condicion(p) }}</span>
                    @if (p.destacada) {
                      <span class="insignia-sol"><app-icono nombre="destello" [tamano]="12" />Destacada</span>
                    }
                    @if (p.oculta) {
                      <span class="insignia bg-tierra-100 text-tierra-700"><app-icono nombre="ojoNo" [tamano]="12" />Oculta</span>
                    }
                  </div>
                  <p class="mt-1.5 truncate font-display font-bold">{{ p.titulo }}</p>
                  <p class="text-xs text-tenue">
                    {{ p.localidad }}, {{ p.municipio }} · publicada {{ p.fechaPublicacion | hace }}
                    @if (p.precioReferenciaCop) {
                      · {{ p.precioReferenciaCop | cop }}
                    }
                  </p>
                  <p class="mt-1 flex items-center gap-1 text-xs font-semibold text-bosque-700 dark:text-bosque-300">
                    <app-icono nombre="ojo" [tamano]="12" />{{ p.vistas ?? 0 | numero }} vistas
                    @if ((p.interesados ?? 0) > 0) {
                      <span class="ml-2 flex items-center gap-1 text-tierra-600 dark:text-tierra-500">
                        <app-icono nombre="usuarios" [tamano]="12" />{{ p.interesados }} {{ p.interesados === 1 ? 'interesada' : 'interesadas' }}
                      </span>
                    }
                  </p>
                </div>
              </a>
              <div class="flex shrink-0 flex-wrap gap-2">
                <button type="button" class="btn btn-secundario btn-sm" (click)="abrirEstadisticas(p.id!)"><app-icono nombre="grafica" [tamano]="14" />Estadísticas</button>
                @if (p.estado === 'Disponible') {
                  <button type="button" class="btn btn-secundario btn-sm" (click)="impulsar(p)" [disabled]="trabajando() || !puedeImpulsar(p)"
                    [title]="puedeImpulsar(p) ? 'Sube al primer lugar de «Más recientes»' : 'Disponible ' + (p.proximoImpulsoUtc | hace)">
                    <app-icono nombre="cohete" [tamano]="14" />Impulsar
                  </button>
                  @if (!p.interesados) {
                    <a [routerLink]="['/publicacion', p.id, 'editar']" class="btn btn-secundario btn-sm"><app-icono nombre="editar" [tamano]="14" />Editar</a>
                  }
                }
                <a routerLink="/intercambios" [queryParams]="{ tab: 'recibidas' }" class="btn btn-secundario btn-sm"><app-icono nombre="apreton" [tamano]="14" />Solicitudes</a>
              </div>
            </li>
          }
        }
      </ul>

      @if (!recurso.isLoading() && visibles().length === 0) {
        @if (filtro() === 'activas') {
          <app-estado-vacio icono="paquete" titulo="No tienes publicaciones activas" descripcion="Publica lo que ya no usas: alguien cerca de ti lo está buscando.">
            <a routerLink="/publicar" class="btn btn-primario">Publicar mi primer objeto</a>
          </app-estado-vacio>
        } @else {
          <app-estado-vacio icono="hoja" titulo="Nada por aquí" descripcion="Cuando tus publicaciones cambien de estado, aparecerán en esta pestaña." />
        }
      }
    </div>

    <app-modal [(abierto)]="estadisticasAbiertas" titulo="Estadísticas" [subtitulo]="tituloEstadisticas()">
      @if (estadisticas.value(); as s) {
        <div class="grid grid-cols-3 gap-3 text-center">
          <div class="rounded-xl bg-superficie-2 p-3"><p class="font-display text-2xl font-extrabold">{{ s.vistasTotales | numero }}</p><p class="text-xs text-tenue">Vistas</p></div>
          <div class="rounded-xl bg-superficie-2 p-3"><p class="font-display text-2xl font-extrabold">{{ s.favoritos | numero }}</p><p class="text-xs text-tenue">Guardada</p></div>
          <div class="rounded-xl bg-superficie-2 p-3"><p class="font-display text-2xl font-extrabold">{{ s.solicitudes | numero }}</p><p class="text-xs text-tenue">Solicitudes</p></div>
        </div>
        <p class="mt-3 text-xs text-tenue">Una persona cuenta como una vista cada pocas horas. Tus propias visitas no cuentan.</p>
        @if (s.destacada) {
          <p class="mt-2 text-sm"><app-icono nombre="destello" [tamano]="14" class="text-sol-500" /> Destacada hasta el {{ s.destacadaHasta | fecha }}</p>
        }

        <h3 class="mt-5 text-sm font-bold">Vistas de los últimos 30 días</h3>
        @if (s.serieDisponible) {
          <div class="mt-3 flex h-32 items-end gap-0.5" role="img" [attr.aria-label]="'Vistas diarias: ' + s.vistasUltimos30Dias + ' en 30 días'">
            @for (d of s.serie ?? []; track d.fecha) {
              <div class="flex-1 rounded-t bg-bosque-500/80" [style.height.%]="altura(d.vistas ?? 0)" [title]="(d.fecha | fecha) + ': ' + d.vistas + ' vistas'"></div>
            }
          </div>
          <p class="mt-2 text-xs text-tenue">{{ s.vistasUltimos30Dias | numero }} vistas en 30 días.</p>
        } @else {
          <div class="mt-3 rounded-2xl bg-sol-50 p-4 text-sm dark:bg-sol-500/10">
            <p class="font-semibold">La gráfica diaria es un beneficio de los planes Premium y Empresa.</p>
            <p class="mt-1 text-tenue">Mira qué días te visitan más y si destacar tu publicación funcionó.</p>
            <a routerLink="/eco-puntos" class="btn btn-sol btn-sm mt-3" (click)="estadisticasAbiertas.set(false)"><app-icono nombre="corona" [tamano]="14" />Ver planes</a>
          </div>
        }
      } @else {
        <div class="esqueleto h-40"></div>
      }
    </app-modal>
  `,
})
export default class MisPublicaciones {
  private readonly api = inject(CatalogoApi);
  private readonly cuentaApi = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly sesion = inject(SesionService);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly etiquetaEstado = ETIQUETA_ESTADO_PUBLICACION;
  protected readonly filtro = signal<Filtro>('activas');
  protected readonly filtros: { valor: Filtro; texto: string }[] = [
    { valor: 'activas', texto: 'Disponibles' },
    { valor: 'negociacion', texto: 'Reservadas' },
    { valor: 'cerradas', texto: 'Cerradas' },
  ];
  protected readonly recurso = rxResource({ stream: () => this.api.mias() });
  protected readonly trabajando = signal(false);
  private readonly politica = toSignal(this.cuentaApi.politica$.pipe(catchError(() => of(null))), { initialValue: null });

  protected readonly estadisticasAbiertas = signal(false);
  private readonly idEstadisticas = signal<string | null>(null);
  protected readonly estadisticas = rxResource({
    params: () => this.idEstadisticas() ?? undefined,
    stream: ({ params }) => this.api.estadisticas(params),
  });
  protected readonly tituloEstadisticas = computed(
    () => (this.recurso.value() ?? []).find((p) => p.id === this.idEstadisticas())?.titulo ?? '',
  );

  private readonly grupo = (estado: string | undefined): Filtro =>
    estado === 'Disponible' ? 'activas' : estado === 'EnNegociacion' ? 'negociacion' : 'cerradas';

  protected readonly visibles = computed(() => (this.recurso.value() ?? []).filter((p) => this.grupo(p.estado) === this.filtro()));
  protected readonly conteo = computed(() => {
    const c: Record<Filtro, number> = { activas: 0, negociacion: 0, cerradas: 0 };
    for (const p of this.recurso.value() ?? []) c[this.grupo(p.estado)]++;
    return c;
  });

  constructor() {
    // Enlace directo desde el detalle: /mis-publicaciones?estadisticas=<id>
    const id = this.ruta.snapshot.queryParamMap.get('estadisticas');
    if (id) this.abrirEstadisticas(id);
    effect(() => {
      if (!this.estadisticasAbiertas()) untracked(() => void this.router.navigate([], { queryParams: { estadisticas: null }, queryParamsHandling: 'merge', replaceUrl: true }));
    });
  }

  protected condicion(p: PublicacionDto): string {
    return infoCondicion(p.condicion).etiqueta;
  }

  protected puedeImpulsar(p: PublicacionDto): boolean {
    return !p.proximoImpulsoUtc || Date.parse(p.proximoImpulsoUtc) <= Date.now();
  }

  protected altura(vistas: number): number {
    const max = Math.max(1, ...(this.estadisticas.value()?.serie ?? []).map((d) => d.vistas ?? 0));
    return Math.max(2, Math.round((vistas / max) * 100));
  }

  protected abrirEstadisticas(id: string): void {
    this.idEstadisticas.set(id);
    this.estadisticasAbiertas.set(true);
    this.estadisticas.reload();
  }

  protected async impulsar(p: PublicacionDto): Promise<void> {
    const costo = this.politica()?.puntosImpulsar ?? 20;
    if ((this.sesion.usuario()?.saldoEcoPuntos ?? 0) < costo) {
      this.avisos.info(`Necesitas ${costo} Eco-Puntos para impulsar`, 'Gánalos completando intercambios o recárgalos en Eco-Puntos.');
      return;
    }
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.impulsar(p.id!));
      this.avisos.puntos('¡Publicación impulsada!', `Se descontaron ${costo} Eco-Puntos.`);
      this.sesion.recargarUsuario();
      this.recurso.reload();
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.trabajando.set(false);
    }
  }
}
