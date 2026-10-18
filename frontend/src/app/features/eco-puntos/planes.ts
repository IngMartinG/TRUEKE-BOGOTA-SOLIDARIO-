import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { CopPipe, FechaPipe } from '../../shared/pipes';
import { preguntasDe } from '../ayuda/conocimiento';
import { Icono } from '../../shared/ui/icono';

type Plan = 'Individual' | 'Premium' | 'Empresa';
interface Fila {
  texto: string;
  valores: Record<Plan, string | boolean>;
}

/**
 * Planes: Gratis, Premium y Empresa lado a lado, con lo que cada uno cumple (sale de la política, no de textos fijos),
 * la prueba gratis de Premium y las preguntas que más frenan la compra. El pago se hace en Eco-Puntos (?plan=).
 */
@Component({
  imports: [RouterLink, Icono, CopPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-b from-sol-50 to-fondo py-14 dark:from-sol-500/10">
      <div class="contenedor max-w-3xl text-center">
        <h1 class="text-4xl font-extrabold sm:text-5xl">Planes para mover más cosas</h1>
        <p class="mt-4 text-lg text-tenue">
          Publicar, intercambiar y donar es gratis para siempre. Los planes suman visibilidad y herramientas para quien publica seguido.
          Sin cobros automáticos: pagas solo cuando quieres renovar.
        </p>
        @if (resumen.value()?.enPruebaPremium) {
          <p class="mt-5 inline-flex items-center gap-2 rounded-full bg-sol-100 px-4 py-2 text-sm font-semibold text-bosque-950 dark:bg-sol-500/20 dark:text-sol-100">
            <app-icono nombre="corona" [tamano]="16" />Estás probando Premium gratis hasta el {{ resumen.value()?.planVigenteHasta | fecha }}
          </p>
        }
      </div>
    </section>

    <div class="contenedor max-w-6xl pb-16">
      <div class="mb-8 flex flex-wrap items-center justify-center gap-3">
        <div role="radiogroup" aria-label="Periodo de pago" class="inline-flex rounded-full border border-borde bg-superficie-2 p-1">
          @for (o of periodos; track o.valor) {
            <button type="button" role="radio" [attr.aria-checked]="periodo() === o.valor" [attr.tabindex]="periodo() === o.valor ? 0 : -1"
              class="rounded-full px-5 py-2 text-sm font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sol-400 motion-reduce:transition-none"
              [class]="periodo() === o.valor ? 'bg-bosque-950 text-white shadow dark:bg-sol-400 dark:text-bosque-950' : 'text-tenue hover:text-tinta'"
              (click)="periodo.set(o.valor)" (keydown)="teclaPeriodo($event)">{{ o.texto }}</button>
          }
        </div>
        <span class="insignia bg-sol-100 text-bosque-950 dark:bg-sol-500/20 dark:text-sol-100">2 meses gratis con el plan anual</span>
      </div>
      <div class="grid grid-cols-1 gap-5 md:grid-cols-3">
        @for (t of tarjetas(); track t.plan) {
          <article class="tarjeta relative flex flex-col p-6" [class.ring-2]="t.plan === 'Premium'" [class.ring-sol-400]="t.plan === 'Premium'">
            @if (t.plan === 'Premium') {
              <span class="absolute -top-3 left-6 insignia bg-sol-400 text-bosque-950 shadow">Para quienes publican seguido</span>
            }
            <h2 class="flex items-center gap-2 text-xl font-bold"><app-icono [nombre]="t.icono" [tamano]="22" class="text-sol-600 dark:text-sol-300" />{{ t.titulo }}</h2>
            <p class="mt-1 text-sm text-tenue">{{ t.para }}</p>
            <p class="mt-5 font-display text-4xl font-extrabold">
              @if (t.precio) {
                {{ precioDe(t) | cop }}<span class="text-base font-medium text-tenue"> / {{ periodo() === 'anual' ? 'año' : (politica()?.duracionSuscripcionDias ?? 30) + ' días' }}</span>
              } @else {
                Gratis
              }
            </p>
            @if (t.precio && periodo() === 'anual') {
              <p class="mt-1 text-sm font-semibold text-bosque-700 dark:text-bosque-300">Equivale a {{ cop(equivalenteMes(t)) }} al mes · ahorras {{ cop(ahorroAnual(t)) }}</p>
            }
            @if (t.ahorro) {
              <p class="mt-1 text-sm font-semibold text-bosque-700 dark:text-bosque-300">{{ t.ahorro }}</p>
            }
            <ul class="mt-5 flex-1 space-y-2 text-sm">
              @for (b of t.beneficios; track b) {
                <li class="flex gap-2"><app-icono nombre="check" [tamano]="16" class="mt-0.5 shrink-0 text-bosque-600" />{{ b }}</li>
              }
            </ul>
            <div class="mt-6 space-y-2">
              @if (t.plan === 'Individual') {
                @if (!sesion.autenticado()) {
                  <a routerLink="/registro" class="btn btn-secundario w-full">Crear mi cuenta gratis</a>
                } @else if (planActual() === 'Individual') {
                  <p class="rounded-xl bg-superficie-2 py-2.5 text-center text-sm font-semibold">Tu plan actual</p>
                }
              } @else if (bloqueado(t.plan); as otro) {
                <button type="button" class="btn btn-secundario w-full" disabled [attr.aria-describedby]="'bloqueo-' + t.plan">Elegir {{ t.titulo }}</button>
                <p [id]="'bloqueo-' + t.plan" class="text-xs text-tenue">Tienes {{ otro }} hasta el {{ resumen.value()?.planVigenteHasta | fecha }}: podrás cambiar cuando venza.</p>
              } @else {
                @if (t.plan === 'Premium' && pruebaDisponible()) {
                  <button type="button" class="btn btn-sol w-full" (click)="probarPremium()" [disabled]="activando()">
                    <app-icono nombre="regalo" [tamano]="16" />{{ activando() ? 'Activando…' : 'Probar ' + (politica()?.diasPruebaPremium ?? 7) + ' días gratis' }}
                  </button>
                  <p class="text-center text-xs text-tenue">Sin tarjeta y sin cobro al terminar. Una vez por cuenta.</p>
                }
                <button type="button" class="btn w-full" [class]="t.plan === 'Premium' && !pruebaDisponible() ? 'btn-sol' : 'btn-primario'" (click)="elegir(t.plan)">
                  {{ planActual() === t.plan && !resumen.value()?.enPruebaPremium ? 'Renovar ' + t.titulo : 'Elegir ' + t.titulo }}
                </button>
              }
            </div>
          </article>
        }
      </div>

      <section class="mt-16" aria-labelledby="titulo-comparar">
        <h2 id="titulo-comparar" class="titulo-seccion">Compara los planes</h2>
        <div class="mt-6 overflow-x-auto rounded-tarjeta border border-borde">
          <table class="w-full min-w-[36rem] text-left text-sm">
            <caption class="sr-only">Qué incluye cada plan</caption>
            <thead class="bg-superficie-2">
              <tr>
                <th scope="col" class="px-4 py-3 font-semibold">Incluye</th>
                <th scope="col" class="px-4 py-3 font-semibold">Gratis</th>
                <th scope="col" class="px-4 py-3 font-semibold">Premium</th>
                <th scope="col" class="px-4 py-3 font-semibold">Empresa</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-borde">
              @for (f of filas(); track f.texto) {
                <tr>
                  <th scope="row" class="px-4 py-3 font-medium">{{ f.texto }}</th>
                  @for (p of planes; track p) {
                    <td class="px-4 py-3">
                      @if (f.valores[p] === true) {
                        <app-icono nombre="check" [tamano]="18" class="text-bosque-600" /><span class="sr-only">Sí</span>
                      } @else if (f.valores[p] === false) {
                        <span class="text-tenue" aria-hidden="true">—</span><span class="sr-only">No</span>
                      } @else {
                        {{ f.valores[p] }}
                      }
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="mt-3 text-xs text-tenue">
          Precios en pesos colombianos{{ politica()?.preciosIncluyenIva ? ', IVA incluido' : '' }}. Cada pago genera factura electrónica.
          Destacar y verificar también se pueden pagar con Eco-Puntos desde <a routerLink="/eco-puntos" class="enlace">Eco-Puntos</a>.
        </p>
      </section>

      <section class="mx-auto mt-16 max-w-3xl" aria-labelledby="titulo-preguntas">
        <h2 id="titulo-preguntas" class="titulo-seccion text-center">Preguntas frecuentes</h2>
        <div class="mt-6 space-y-3">
          @for (f of preguntas(); track f.id) {
            <details class="group tarjeta p-5 open:shadow-elevada">
              <summary class="flex cursor-pointer list-none items-center justify-between gap-4 font-semibold">
                {{ f.p }}
                <app-icono nombre="abajo" class="shrink-0 text-tenue transition group-open:rotate-180" />
              </summary>
              <p class="mt-3 text-sm text-tenue">{{ f.r }}</p>
            </details>
          }
        </div>
      </section>
    </div>
  `,
})
export default class Planes {
  private readonly api = inject(CuentaApi);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);
  protected readonly planes: readonly Plan[] = ['Individual', 'Premium', 'Empresa'];

  protected readonly politica = toSignal(this.api.politica$.pipe(catchError(() => of(null))));
  protected readonly resumen = rxResource({
    params: () => (this.sesion.autenticado() ? true : undefined),
    stream: () => this.api.resumenPuntos(),
  });
  protected readonly activando = signal(false);
  protected readonly periodos = [
    { valor: 'mensual', texto: 'Mensual' },
    { valor: 'anual', texto: 'Anual' },
  ] as const;
  protected readonly periodo = signal<'mensual' | 'anual'>('mensual');

  protected teclaPeriodo(e: KeyboardEvent): void {
    const paso = e.key === 'ArrowRight' || e.key === 'ArrowDown' ? 1 : e.key === 'ArrowLeft' || e.key === 'ArrowUp' ? -1 : 0;
    if (!paso) return;
    e.preventDefault();
    const i = this.periodos.findIndex((o) => o.valor === this.periodo());
    const j = (i + paso + this.periodos.length) % this.periodos.length;
    this.periodo.set(this.periodos[j].valor);
    const grupo = (e.currentTarget as HTMLElement).parentElement;
    (grupo?.children[j] as HTMLElement | undefined)?.focus();
  }

  protected precioDe(t: { precio: number; precioAnual: number }): number {
    return this.periodo() === 'anual' ? t.precioAnual : t.precio;
  }
  /** Precio anual repartido en 12 meses. */
  protected equivalenteMes(t: { precioAnual: number }): number {
    return Math.round(t.precioAnual / 12);
  }
  /** Lo que se ahorra frente a pagar 12 meses sueltos. */
  protected ahorroAnual(t: { precio: number; precioAnual: number }): number {
    return 12 * t.precio - t.precioAnual;
  }

  protected readonly planActual = computed<Plan | null>(() => {
    if (!this.sesion.autenticado()) return null;
    return (this.resumen.value()?.tipoCuenta as Plan | undefined) ?? null;
  });
  /** Visitantes también ven la oferta: al tocarla, se registran primero. */
  protected readonly pruebaDisponible = computed(() => !this.sesion.autenticado() || !!this.resumen.value()?.pruebaPremiumDisponible);

  protected bloqueado(plan: Plan): string | null {
    const actual = this.planActual();
    if (!actual || actual === 'Individual' || actual === plan) return null;
    // Durante la prueba de Premium sí se puede pasar a Empresa (la prueba termina)
    return actual === 'Premium' && this.resumen.value()?.enPruebaPremium ? null : actual;
  }

  protected readonly tarjetas = computed(() => {
    const p = this.politica();
    const ciclo = p?.diasCicloDestacadosGratis || 30;
    const destacar = p?.precioDestacarCop ?? 6000;
    const premium = p?.precioPremiumCop ?? 15000;
    const empresa = p?.precioEmpresaCop ?? 50000;
    const premiumAnual = p?.precioPremiumAnualCop ?? 150000;
    const empresaAnual = p?.precioEmpresaAnualCop ?? 500000;
    const valorDestacados = (n: number) => n * destacar;
    return [
      {
        plan: 'Individual' as Plan, titulo: 'Gratis', icono: 'usuario', precio: 0, precioAnual: 0, ahorro: '',
        para: 'Para intercambiar, vender y donar lo que ya no usas.',
        beneficios: [
          `Hasta ${p?.maxPublicacionesIndividual ?? 50} publicaciones activas`,
          'Chat, Eco-Puntos y reputación',
          'Destacar e impulsar cuando lo necesites',
        ],
      },
      {
        plan: 'Premium' as Plan, titulo: 'Premium', icono: 'corona', precio: premium, precioAnual: premiumAnual,
        ahorro: `Los destacados incluidos valen ${this.cop(valorDestacados(p?.destacadosGratisPremium ?? 3))}`,
        para: 'Para quienes publican e intercambian seguido.',
        beneficios: [
          `${p?.destacadosGratisPremium ?? 3} destacados gratis cada ${ciclo} días (no se acumulan)`,
          `${p?.descuentoPremiumPorcentaje ?? 15}% de descuento en destacar y verificar`,
          `Hasta ${p?.maxPublicacionesPremium ?? 150} publicaciones activas`,
          'Gráfica diaria de vistas de tus publicaciones',
          'Insignia Premium en tu perfil',
        ],
      },
      {
        plan: 'Empresa' as Plan, titulo: 'Empresa', icono: 'edificio', precio: empresa, precioAnual: empresaAnual,
        ahorro: `Los destacados incluidos valen ${this.cop(valorDestacados(p?.destacadosGratisEmpresa ?? 10))}`,
        para: 'Para tiendas de segunda mano, reparadores, fundaciones y emprendimientos.',
        beneficios: [
          `${p?.destacadosGratisEmpresa ?? 10} destacados gratis cada ${ciclo} días (no se acumulan)`,
          `${p?.descuentoEmpresaPorcentaje ?? 20}% de descuento en destacar y verificar`,
          `Hasta ${p?.maxPublicacionesEmpresa ?? 1000} publicaciones activas`,
          'Nombre comercial y sello «Empresa con NIT registrado» en tus publicaciones',
          'Vende sin el límite de artículos para cuentas sin verificar',
          'Gráfica diaria de vistas y factura a nombre de tu empresa',
        ],
      },
    ];
  });

  protected readonly filas = computed<Fila[]>(() => {
    const p = this.politica();
    const ciclo = p?.diasCicloDestacadosGratis || 30;
    return [
      { texto: 'Publicaciones activas', valores: { Individual: `${p?.maxPublicacionesIndividual ?? 50}`, Premium: `${p?.maxPublicacionesPremium ?? 150}`, Empresa: `${p?.maxPublicacionesEmpresa ?? 1000}` } },
      { texto: `Destacados gratis cada ${ciclo} días`, valores: { Individual: false, Premium: `${p?.destacadosGratisPremium ?? 3}`, Empresa: `${p?.destacadosGratisEmpresa ?? 10}` } },
      { texto: 'Descuento en destacar y verificar', valores: { Individual: false, Premium: `${p?.descuentoPremiumPorcentaje ?? 15}%`, Empresa: `${p?.descuentoEmpresaPorcentaje ?? 20}%` } },
      { texto: 'Gráfica diaria de vistas', valores: { Individual: false, Premium: true, Empresa: true } },
      { texto: 'Insignia en el perfil', valores: { Individual: false, Premium: 'Premium', Empresa: 'Empresa' } },
      { texto: 'Nombre comercial y sello con NIT', valores: { Individual: false, Premium: false, Empresa: true } },
      { texto: 'Ventas sin el límite para cuentas sin verificar', valores: { Individual: false, Premium: false, Empresa: true } },
      { texto: 'Chat, Eco-Puntos e impulsar', valores: { Individual: true, Premium: true, Empresa: true } },
    ];
  });

  protected readonly preguntas = computed(() => preguntasDe('planes', this.politica()));

  protected cop(v: number): string {
    return `$${v.toLocaleString('es-CO')}`;
  }

  protected elegir(plan: Plan): void {
    if (!this.sesion.autenticado()) {
      void this.router.navigate(['/ingresar'], { queryParams: { volver: '/planes' } });
      return;
    }
    void this.router.navigate(['/eco-puntos'], { queryParams: this.periodo() === 'anual' && plan !== 'Individual' ? { plan, periodo: 'anual' } : { plan } });
  }

  protected async probarPremium(): Promise<void> {
    if (!this.sesion.autenticado()) {
      void this.router.navigate(['/registro'], { queryParams: { volver: '/planes' } });
      return;
    }
    this.activando.set(true);
    try {
      await firstValueFrom(this.api.activarPruebaPremium());
      this.resumen.reload();
      this.sesion.recargarUsuario();
      this.avisos.exito('¡Ya tienes Premium!', `Pruébalo ${this.politica()?.diasPruebaPremium ?? 7} días gratis. Al terminar no se cobra nada.`);
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.activando.set(false);
    }
  }
}
