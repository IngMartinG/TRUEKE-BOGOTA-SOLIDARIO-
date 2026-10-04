import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { iconoCategoria, INFO_MODO, MODOS } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { TarjetaEsqueleto, TarjetaPublicacion } from '../../shared/ui/tarjeta-publicacion';

@Component({
  imports: [RouterLink, FormsModule, Icono, TarjetaPublicacion, TarjetaEsqueleto, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- ============ HERO ============ -->
    <section class="relative isolate overflow-hidden bg-bosque-950 text-white">
      <div class="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_top_right,#269763_0%,transparent_55%),radial-gradient(ellipse_at_bottom_left,#0f8a86_0%,transparent_50%)] opacity-70"></div>
      <svg class="absolute -top-24 -left-24 -z-10 size-96 text-bosque-700/40 animate-flotar" viewBox="0 0 200 200" aria-hidden="true">
        <path fill="currentColor" d="M100 10c50 15 78 52 72 102-38 4-70-12-88-46-7-15-8-35 16-56Z" />
      </svg>
      <svg class="absolute right-0 bottom-0 -z-10 h-full w-1/2 text-bosque-900/60" viewBox="0 0 400 400" preserveAspectRatio="none" aria-hidden="true">
        <path fill="currentColor" d="M400 0v400H80c60-40 100-110 90-190C160 120 260 30 400 0Z" />
      </svg>

      <div class="contenedor grid grid-cols-1 items-center gap-12 pt-14 pb-24 lg:grid-cols-[1.1fr_1fr] lg:pt-20 lg:pb-32">
        <div class="animate-aparecer">
          <p class="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-4 py-1.5 text-xs font-semibold tracking-wide text-bosque-200 backdrop-blur">
            <app-icono nombre="reciclar" [tamano]="14" class="text-bosque-300" /> Economía circular en los 1.122 municipios de Colombia
          </p>
          <h1 class="mt-6 font-display text-4xl leading-[1.05] font-extrabold text-white sm:text-5xl lg:text-6xl">
            Dale una <span class="relative whitespace-nowrap text-sol-300">segunda vida<svg class="absolute -bottom-2 left-0 w-full" viewBox="0 0 200 12" aria-hidden="true"><path d="M2 9c50-6 140-8 196-3" stroke="#f2b632" stroke-width="3" fill="none" stroke-linecap="round" /></svg></span>
            a lo que ya no usas
          </h1>
          <p class="mt-6 max-w-xl text-lg text-bosque-100/85">
            Intercambia, compra de segunda mano o dona con tus vecinos. Cada objeto que circula es un residuo menos y te suma
            <strong class="text-sol-300">Eco-Puntos</strong>.
          </p>

          <form class="mt-8 flex max-w-xl gap-2 rounded-full bg-white p-1.5 shadow-elevada" role="search" (ngSubmit)="buscar()">
            <label for="buscar-hero" class="sr-only">¿Qué estás buscando?</label>
            <div class="flex flex-1 items-center gap-2 pl-4 text-bosque-900">
              <app-icono nombre="buscar" [tamano]="20" class="text-bosque-600" />
              <input
                id="buscar-hero"
                name="texto"
                type="search"
                [(ngModel)]="texto"
                maxlength="100"
                placeholder="Bicicleta, libros del colegio, coche de bebé..."
                class="w-full bg-transparent py-2 text-base text-bosque-950 outline-none placeholder:text-bosque-900/40"
              />
            </div>
            <button type="submit" class="btn btn-primario btn-lg">Buscar</button>
          </form>

          <div class="mt-6 flex flex-wrap items-center gap-3">
            <a [routerLink]="sesion.autenticado() ? '/publicar' : '/registro'" class="btn btn-sol btn-lg">
              <app-icono nombre="mas" [tamano]="20" /> Publicar gratis
            </a>
            <a routerLink="/como-funciona" class="btn btn-lg text-white hover:bg-white/10">
              Cómo funciona <app-icono nombre="flechaDerecha" [tamano]="18" />
            </a>
          </div>

          <dl class="mt-12 grid max-w-lg grid-cols-3 gap-6 border-t border-white/10 pt-8">
            <div>
              <dt class="text-xs text-bosque-200/70">Publicaciones activas</dt>
              <dd class="font-display text-3xl font-extrabold">{{ totalPublicaciones() | numero }}</dd>
            </div>
            <div>
              <dt class="text-xs text-bosque-200/70">Localidades</dt>
              <dd class="font-display text-3xl font-extrabold">20</dd>
            </div>
            <div>
              <dt class="text-xs text-bosque-200/70">Eco-Puntos de bienvenida</dt>
              <dd class="font-display text-3xl font-extrabold text-sol-300">+{{ politica()?.puntosBienvenida ?? 10 }}</dd>
            </div>
          </dl>
        </div>

        <!-- Ilustración: tarjetas flotantes de los tres modos -->
        <div class="relative mx-auto hidden h-[30rem] w-full max-w-md lg:block" aria-hidden="true">
          <div class="absolute inset-8 rounded-full bg-bosque-500/20 blur-3xl"></div>
          @for (m of tarjetasHero; track m.modo; let i = $index) {
            <div
              class="absolute w-64 rounded-3xl border border-white/10 bg-white p-3 text-bosque-950 shadow-2xl animate-flotar"
              [style.top]="m.top"
              [style.left]="m.left"
              [style.rotate]="m.giro"
              [style.animationDelay]="i * 1.3 + 's'"
            >
              <div class="grid h-36 place-items-center rounded-2xl" [class]="m.fondo">
                <app-icono [nombre]="m.icono" [tamano]="56" [grosor]="1.3" />
              </div>
              <div class="mt-3 flex items-center justify-between px-1">
                <div>
                  <p class="font-display text-sm font-bold">{{ m.titulo }}</p>
                  <p class="text-xs text-bosque-900/60">{{ m.lugar }}</p>
                </div>
                <span [class]="info[m.modo].clase">{{ info[m.modo].etiqueta }}</span>
              </div>
            </div>
          }
          <div class="absolute right-2 bottom-6 flex items-center gap-2 rounded-2xl bg-sol-400 px-4 py-3 font-bold text-bosque-950 shadow-xl">
            <app-icono nombre="destello" [tamano]="20" /> +20 Eco-Puntos
          </div>
        </div>
      </div>
      <svg class="absolute -bottom-px left-0 w-full text-fondo" viewBox="0 0 1440 60" preserveAspectRatio="none" aria-hidden="true">
        <path d="M0 60V30c240-40 480-40 720-10s480 30 720-10v50Z" fill="currentColor" />
      </svg>
    </section>

    <!-- ============ MODOS ============ -->
    <section class="contenedor -mt-6 relative z-10" aria-labelledby="titulo-modos">
      <h2 id="titulo-modos" class="sr-only">Tres formas de participar</h2>
      <div class="grid grid-cols-1 gap-4 md:grid-cols-3">
        @for (m of modos; track m) {
          <a
            [routerLink]="['/explorar']"
            [queryParams]="{ modo: m }"
            class="group tarjeta-interactiva relative overflow-hidden p-6"
          >
            <div class="absolute -top-10 -right-10 size-32 rounded-full opacity-60 transition group-hover:scale-125" [class]="fondoModo[m]"></div>
            <span class="relative grid size-12 place-items-center rounded-2xl text-white shadow-lg" [class]="solidoModo[m]">
              <app-icono [nombre]="info[m].icono" [tamano]="24" />
            </span>
            <h3 class="relative mt-5 text-xl font-bold">{{ info[m].etiqueta }}</h3>
            <p class="relative mt-2 text-sm text-tenue">{{ info[m].descripcion }}</p>
            <p class="relative mt-5 flex items-center justify-between text-sm font-semibold">
              <span class="insignia-sol"><app-icono nombre="moneda" [tamano]="12" />+{{ puntosModo(m) }} Eco-Puntos</span>
              <span class="flex items-center gap-1 text-bosque-700 transition group-hover:gap-2 dark:text-bosque-300">Ver <app-icono nombre="flechaDerecha" [tamano]="16" /></span>
            </p>
          </a>
        }
      </div>
    </section>

    <!-- ============ RECIENTES ============ -->
    <section class="contenedor mt-20" aria-labelledby="titulo-recientes">
      <div class="flex items-end justify-between gap-4">
        <div>
          <p class="text-sm font-bold tracking-wide text-bosque-600 uppercase dark:text-bosque-400">Recién publicado</p>
          <h2 id="titulo-recientes" class="titulo-seccion mt-1">Lo nuevo cerca de ti</h2>
        </div>
        <a routerLink="/explorar" class="btn btn-secundario hidden sm:inline-flex">Ver todo <app-icono nombre="flechaDerecha" [tamano]="16" /></a>
      </div>
      <div class="mt-8 grid grid-cols-2 gap-3 sm:gap-5 lg:grid-cols-4">
        @if (recientes(); as lista) {
          @for (p of lista; track p.id) {
            <app-tarjeta-publicacion [publicacion]="p" />
          } @empty {
            <div class="col-span-full rounded-tarjeta border-2 border-dashed border-borde p-10 text-center">
              <p class="font-semibold">Aún no hay publicaciones. ¡Sé la primera persona en publicar!</p>
              <a routerLink="/publicar" class="btn btn-primario mt-4">Publicar algo</a>
            </div>
          }
        } @else {
          @for (i of [1, 2, 3, 4]; track i) {
            <app-tarjeta-esqueleto />
          }
        }
      </div>
    </section>

    <!-- ============ CATEGORÍAS ============ -->
    <section class="contenedor mt-20" aria-labelledby="titulo-categorias">
      <h2 id="titulo-categorias" class="titulo-seccion">Explora por categoría</h2>
      <div class="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
        @for (c of categorias(); track c.id) {
          <a
            [routerLink]="['/explorar']"
            [queryParams]="{ categoriaId: c.id }"
            class="group flex flex-col items-center gap-3 rounded-tarjeta border border-borde bg-superficie p-5 text-center transition hover:-translate-y-1 hover:border-bosque-300 hover:shadow-suave"
          >
            <span class="grid size-14 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 transition group-hover:bg-bosque-600 group-hover:text-white dark:bg-bosque-900 dark:text-bosque-200">
              <app-icono [nombre]="iconoCategoria(c.id)" [tamano]="26" [grosor]="1.7" />
            </span>
            <span class="text-sm font-semibold leading-tight">{{ c.nombre }}</span>
          </a>
        }
      </div>
    </section>

    <!-- ============ CÓMO FUNCIONA ============ -->
    <section class="mt-24 bg-superficie-2/60 py-20" aria-labelledby="titulo-pasos">
      <div class="contenedor">
        <div class="mx-auto max-w-2xl text-center">
          <p class="text-sm font-bold tracking-wide text-bosque-600 uppercase dark:text-bosque-400">Así de fácil</p>
          <h2 id="titulo-pasos" class="titulo-seccion mt-1">Tres pasos para hacer circular tus cosas</h2>
        </div>
        <ol class="mt-12 grid grid-cols-1 gap-6 md:grid-cols-3">
          @for (paso of pasos; track paso.titulo; let i = $index) {
            <li class="relative rounded-tarjeta bg-superficie p-7 shadow-suave">
              <span class="absolute -top-4 left-7 grid size-9 place-items-center rounded-full bg-sol-400 font-display font-extrabold text-bosque-950 shadow">{{ i + 1 }}</span>
              <app-icono [nombre]="paso.icono" [tamano]="36" [grosor]="1.6" class="text-bosque-600 dark:text-bosque-300" />
              <h3 class="mt-4 text-lg font-bold">{{ paso.titulo }}</h3>
              <p class="mt-2 text-sm text-tenue">{{ paso.texto }}</p>
            </li>
          }
        </ol>
      </div>
    </section>

    <!-- ============ ECO-PUNTOS ============ -->
    <section class="contenedor mt-20">
      <div class="relative overflow-hidden rounded-[2rem] bg-gradient-to-br from-sol-300 via-sol-400 to-sol-500 p-8 text-bosque-950 sm:p-12">
        <svg class="absolute -right-10 -bottom-16 size-80 text-white/25" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1" aria-hidden="true">
          <circle cx="8" cy="8" r="6" /><path d="M18.09 10.37A6 6 0 1 1 10.34 18" /><path d="M7 6h1v4" />
        </svg>
        <div class="relative grid grid-cols-1 items-center gap-8 lg:grid-cols-2">
          <div>
            <p class="insignia bg-bosque-950/10 text-bosque-950"><app-icono nombre="destello" [tamano]="12" /> Eco-Puntos</p>
            <h2 class="mt-4 text-3xl font-extrabold text-bosque-950 sm:text-4xl">Tu buena acción vale puntos</h2>
            <p class="mt-3 max-w-lg text-bosque-950/80">
              Gana puntos cada vez que completas un intercambio y úsalos para destacar tus publicaciones, verificar tu cuenta
              o acceder a planes con beneficios.
            </p>
            <a routerLink="/eco-puntos" class="btn btn-lg mt-6 bg-bosque-950 text-white hover:bg-bosque-900">Conocer Eco-Puntos</a>
          </div>
          <ul class="grid grid-cols-2 gap-3">
            @for (g of ganancias(); track g.modo) {
              <li class="rounded-2xl bg-white/60 p-4 backdrop-blur">
                <p class="text-sm font-semibold text-bosque-900/70">{{ g.modo }}</p>
                <p class="font-display text-3xl font-extrabold">+{{ g.puntos }}</p>
              </li>
            }
          </ul>
        </div>
      </div>
    </section>

    <!-- ============ CTA FINAL ============ -->
    @if (!sesion.autenticado()) {
      <section class="contenedor mt-20 text-center">
        <h2 class="titulo-seccion">¿Listo para empezar?</h2>
        <p class="mx-auto mt-3 max-w-xl text-tenue">Crea tu cuenta gratis en menos de un minuto y recibe tus primeros Eco-Puntos.</p>
        <div class="mt-6 flex flex-wrap justify-center gap-3">
          <a routerLink="/registro" class="btn btn-primario btn-lg">Crear mi cuenta</a>
          <a routerLink="/explorar" class="btn btn-secundario btn-lg">Solo quiero mirar</a>
        </div>
      </section>
    }
  `,
})
export default class Inicio {
  private readonly catalogo = inject(CatalogoApi);
  private readonly cuenta = inject(CuentaApi);
  private readonly router = inject(Router);
  protected readonly sesion = inject(SesionService);
  protected readonly texto = signal('');
  protected readonly info = INFO_MODO;
  protected readonly modos = MODOS;

  private readonly pagina = toSignal(
    this.catalogo.listar({ orden: 'Recientes', tamano: 8 }).pipe(catchError(() => of({ items: [], total: 0 }))),
  );
  protected readonly recientes = () => this.pagina()?.items;
  protected readonly totalPublicaciones = () => this.pagina()?.total ?? 0;
  protected readonly categorias = toSignal(this.catalogo.categorias$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly politica = toSignal(this.cuenta.politica$.pipe(catchError(() => of(null))));
  protected readonly ganancias = () =>
    [
      { modo: 'Bienvenida', puntos: this.politica()?.puntosBienvenida ?? 10 },
      ...MODOS.map((m) => ({ modo: INFO_MODO[m].etiqueta, puntos: this.puntosModo(m) })),
    ];

  protected readonly fondoModo: Record<string, string> = {
    Trueke: 'bg-bosque-100 dark:bg-bosque-900',
    Compra: 'bg-cielo-100 dark:bg-cielo-700/30',
    Donacion: 'bg-tierra-100 dark:bg-tierra-700/30',
  };
  protected readonly solidoModo: Record<string, string> = {
    Trueke: 'bg-bosque-600',
    Compra: 'bg-cielo-600',
    Donacion: 'bg-tierra-600',
  };

  protected readonly tarjetasHero = [
    { modo: 'Trueke' as const, titulo: 'Bicicleta urbana', lugar: 'Chapinero', icono: 'repeat', top: '0', left: '10%', giro: '-6deg', fondo: 'bg-bosque-100 text-bosque-700' },
    { modo: 'Donacion' as const, titulo: 'Libros escolares', lugar: 'Kennedy', icono: 'paquete', top: '33%', left: '42%', giro: '5deg', fondo: 'bg-tierra-100 text-tierra-700' },
    { modo: 'Compra' as const, titulo: 'Silla de escritorio', lugar: 'Suba', icono: 'inicio', top: '58%', left: '0', giro: '-3deg', fondo: 'bg-cielo-100 text-cielo-700' },
  ];

  protected readonly pasos = [
    { icono: 'camara', titulo: 'Publica en un minuto', texto: 'Toma fotos, elige si quieres intercambiar, vender o donar, y marca tu ciudad y barrio.' },
    { icono: 'mensaje', titulo: 'Acuerda por el chat', texto: 'Recibe solicitudes, acepta la que más te convenga y coordina la entrega sin compartir tu número.' },
    { icono: 'apreton', titulo: 'Entrega y gana', texto: 'Cuando ambas partes confirman la entrega, reciben Eco-Puntos y suben su reputación.' },
  ];

  protected puntosModo(m: string): number {
    const g = this.politica()?.ganancias?.find((x) => x.modo === m);
    return g?.puntos ?? INFO_MODO[m as keyof typeof INFO_MODO].puntos;
  }

  protected readonly iconoCategoria = iconoCategoria;

  protected buscar(): void {
    const texto = this.texto().trim();
    void this.router.navigate(['/explorar'], { queryParams: texto ? { texto } : {} });
  }
}
