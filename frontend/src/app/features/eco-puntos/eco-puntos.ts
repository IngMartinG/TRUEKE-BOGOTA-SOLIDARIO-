import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of } from 'rxjs';
import { INFO_MODO, type ConceptoPagoDto, type IniciarPagoRequest, type ModoDto } from '../../api/tipos';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { ConfigService } from '../../core/config.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { SubidasService } from '../../core/subidas.service';
import { CopPipe, FechaPipe, NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { abrirCheckoutWompi } from './wompi';

interface Servicio {
  concepto: Exclude<ConceptoPagoDto, 'Recarga'>;
  titulo: string;
  descripcion: string;
  icono: string;
  precio: number;
  extra: string;
  beneficios: string[];
  destacado?: boolean;
}

@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, CopPipe, NumeroPipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Encabezado con saldo -->
    <section class="relative overflow-hidden bg-gradient-to-br from-sol-300 via-sol-400 to-sol-500 text-bosque-950">
      <svg class="absolute -top-20 -right-20 size-96 text-white/20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width=".8" aria-hidden="true">
        <circle cx="8" cy="8" r="6" /><path d="M18.09 10.37A6 6 0 1 1 10.34 18" /><path d="M7 6h1v4" />
      </svg>
      <div class="contenedor relative grid grid-cols-1 gap-8 py-12 lg:grid-cols-[1.2fr_1fr] lg:items-center">
        <div>
          <p class="insignia bg-bosque-950/10 text-bosque-950"><app-icono nombre="destello" [tamano]="12" />Moneda de la comunidad</p>
          <h1 class="mt-4 text-4xl font-extrabold text-bosque-950 sm:text-5xl">Eco-Puntos</h1>
          <p class="mt-3 max-w-xl text-lg text-bosque-950/80">
            Gana puntos cada vez que un objeto encuentra un nuevo hogar y úsalos para obtener descuentos en los servicios de la plataforma.
          </p>
        </div>
        @if (sesion.autenticado()) {
          <div class="rounded-[1.75rem] bg-bosque-950 p-6 text-white shadow-elevada">
            <p class="text-sm text-bosque-200">Tu saldo</p>
            <p class="mt-1 flex items-center gap-2 font-display text-5xl font-extrabold text-sol-300">
              <app-icono nombre="moneda" [tamano]="40" />{{ resumen.value()?.saldo ?? sesion.usuario()?.saldoEcoPuntos | numero }}
            </p>
            <dl class="mt-5 grid grid-cols-3 gap-3 text-center text-xs">
              <div class="rounded-xl bg-white/5 p-3"><dt class="text-bosque-200">Reputación</dt><dd class="mt-1 text-lg font-bold">{{ (resumen.value()?.reputacion ?? 0).toFixed(1) }}</dd></div>
              <div class="rounded-xl bg-white/5 p-3"><dt class="text-bosque-200">Canjes libres hoy</dt><dd class="mt-1 text-lg font-bold">{{ resumen.value()?.transaccionesConPuntosRestantes ?? '–' }}</dd></div>
              <div class="rounded-xl bg-white/5 p-3"><dt class="text-bosque-200">Cuenta</dt><dd class="mt-1 text-lg font-bold">{{ resumen.value()?.tipoCuenta ?? '–' }}</dd></div>
            </dl>
            @if (resumen.value()?.planVigenteHasta) {
              <p class="mt-3 text-xs text-bosque-200">Plan vigente hasta el {{ resumen.value()?.planVigenteHasta | fecha }} · {{ resumen.value()?.destacadosGratisRestantes }} destacados gratis</p>
            }
          </div>
        } @else {
          <div class="rounded-[1.75rem] bg-white/70 p-6 backdrop-blur">
            <p class="font-display text-xl font-bold">Empieza con {{ politica()?.puntosBienvenida ?? 10 }} Eco-Puntos de regalo</p>
            <p class="mt-1 text-sm text-bosque-950/70">Crea tu cuenta gratis y recibe puntos de bienvenida.</p>
            <a routerLink="/registro" class="btn btn-lg mt-4 bg-bosque-950 text-white hover:bg-bosque-900">Crear mi cuenta</a>
          </div>
        }
      </div>
    </section>

    <!-- Cómo ganar -->
    <section class="contenedor mt-14" aria-labelledby="titulo-ganar">
      <h2 id="titulo-ganar" class="titulo-seccion">Cómo ganar puntos</h2>
      <p class="mt-1 text-tenue">Los puntos se acreditan cuando ambas partes confirman la entrega.</p>
      <div class="mt-6 grid grid-cols-2 gap-3 md:grid-cols-4">
        <div class="tarjeta p-5">
          <app-icono nombre="regalo" [tamano]="28" class="text-sol-500" />
          <p class="mt-3 font-display text-3xl font-extrabold">+{{ politica()?.puntosBienvenida ?? 10 }}</p>
          <p class="text-sm text-tenue">Al confirmar tu correo</p>
        </div>
        @for (g of politica()?.ganancias ?? []; track g.modo) {
          <div class="tarjeta p-5">
            <app-icono [nombre]="iconoModo(g.modo)" [tamano]="28" class="text-bosque-600 dark:text-bosque-300" />
            <p class="mt-3 font-display text-3xl font-extrabold">+{{ g.puntos }}</p>
            <p class="text-sm text-tenue">Por cada {{ etiquetaModo(g.modo) }} · +{{ g.reputacion }} reputación</p>
          </div>
        }
      </div>
      @if (politica(); as p) {
        <p class="mt-4 text-sm text-tenue">
          Ganas puntos en hasta {{ p.maxTransaccionesConPuntosPorDia }} intercambios al día, y con una misma persona una vez cada
          {{ p.diasEntreTransaccionesConPuntosMismaPareja }} días (así nadie puede inflar sus puntos con cuentas de amigos).
          La reputación máxima es {{ p.reputacionMaxima }}. Los Eco-Puntos no se convierten en dinero.
        </p>
      }
    </section>

    <!-- Usar puntos: impulsar -->
    <section class="contenedor mt-14" aria-labelledby="titulo-impulsar">
      <div class="tarjeta grid grid-cols-1 gap-6 p-6 sm:p-8 md:grid-cols-[auto_1fr] md:items-center">
        <span class="grid size-16 place-items-center rounded-2xl bg-sol-100 text-sol-600 dark:bg-sol-500/15"><app-icono nombre="cohete" [tamano]="32" /></span>
        <div>
          <h2 id="titulo-impulsar" class="titulo-seccion">Impulsa tus publicaciones con puntos</h2>
          <p class="mt-2 text-tenue">
            Por <strong>{{ politica()?.puntosImpulsar ?? 20 }} Eco-Puntos</strong> tu publicación vuelve al primer lugar de «Más recientes».
            Puedes hacerlo una vez cada {{ politica()?.horasEntreImpulsos ?? 24 }} horas por publicación, desde
            <a routerLink="/mis-publicaciones" class="enlace">Mis publicaciones</a>.
          </p>
        </div>
      </div>
    </section>

    <!-- Servicios -->
    <section class="contenedor mt-16" aria-labelledby="titulo-servicios">
      <h2 id="titulo-servicios" class="titulo-seccion">Servicios y planes</h2>
      <p class="mt-1 text-tenue">
        Mientras más Eco-Puntos tengas, mayor es tu descuento. Publicar, intercambiar y donar es gratis: el plan Individual permite
        {{ politica()?.maxPublicacionesIndividual ?? 50 }} publicaciones activas.
      </p>
      <div class="mt-6 grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        @for (s of servicios(); track s.concepto) {
          <article class="tarjeta relative flex flex-col p-6" [class.ring-2]="s.destacado" [class.ring-sol-400]="s.destacado">
            @if (s.destacado) {
              <span class="absolute -top-3 left-6 insignia bg-sol-400 text-bosque-950 shadow">Más popular</span>
            }
            <span class="grid size-12 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200"><app-icono [nombre]="s.icono" [tamano]="24" /></span>
            <h3 class="mt-4 text-lg font-bold">{{ s.titulo }}</h3>
            <p class="mt-1 text-sm text-tenue">{{ s.descripcion }}</p>
            <p class="mt-4 font-display text-3xl font-extrabold">{{ s.precio | cop }}<span class="text-sm font-medium text-tenue">{{ s.extra }}</span></p>
            <ul class="mt-4 flex-1 space-y-2 text-sm">
              @for (b of s.beneficios; track b) {
                <li class="flex gap-2"><app-icono nombre="check" [tamano]="16" class="mt-0.5 shrink-0 text-bosque-600" />{{ b }}</li>
              }
            </ul>
            <button type="button" class="btn mt-6 w-full" [class]="s.destacado ? 'btn-sol' : 'btn-primario'" (click)="abrirCompra(s.concepto)">
              {{ s.concepto === 'Destacar' ? 'Destacar una publicación' : s.concepto === 'Verificar' ? 'Verificar mi cuenta' : planActual() === s.concepto ? 'Renovar plan' : 'Elegir plan' }}
            </button>
          </article>
        }
      </div>
      <p class="mt-4 text-xs text-tenue">
        Precios en pesos colombianos{{ politica()?.preciosIncluyenIva ? ', IVA (' + politica()?.ivaPorcentaje + ' %) incluido' : '' }}. Cada pago genera factura electrónica
        (agrega tus datos en <a routerLink="/cuenta/facturacion" class="enlace">Facturación</a>). Los planes no se renuevan solos: te avisamos
        3 días antes de que venzan y, si renuevas antes, los días se suman. Tienes derecho de retracto dentro de los 5 días hábiles siguientes a la compra
        (<a routerLink="/cuenta/soporte" [queryParams]="{ tipo: 'Retracto' }" class="enlace">solicitarlo</a>).
      </p>
    </section>

    <!-- Recargar -->
    <section class="contenedor mt-16" aria-labelledby="titulo-recarga">
      <div class="tarjeta grid grid-cols-1 gap-6 p-6 sm:p-8 lg:grid-cols-2 lg:items-center">
        <div>
          <h2 id="titulo-recarga" class="titulo-seccion">Recarga Eco-Puntos</h2>
          <p class="mt-2 text-tenue">
            {{ politica()?.copPorEcoPunto ?? 100 | cop }} = 1 Eco-Punto. Úsalos para impulsar tus publicaciones ({{ politica()?.puntosImpulsar ?? 20 }} puntos cada vez)
            o para alcanzar un escalón de descuento. El pago se procesa de forma segura con Wompi.
          </p>
        </div>
        <form class="flex flex-col gap-3 sm:flex-row sm:items-end" (ngSubmit)="recargar()">
          <div class="campo flex-1">
            <label for="monto" class="etiqueta">Monto en pesos</label>
            <input id="monto" name="monto" type="number" inputmode="numeric" class="entrada text-lg" [min]="politica()?.recargaMinimaCop ?? 0" [max]="politica()?.recargaMaximaCop ?? 0" step="1000" [(ngModel)]="montoRecarga" />
            <p class="ayuda">
              Entre {{ politica()?.recargaMinimaCop | cop }} y {{ politica()?.recargaMaximaCop | cop }} · recibes
              <strong class="text-sol-600 dark:text-sol-300">{{ puntosRecarga() | numero }} Eco-Puntos</strong>
            </p>
          </div>
          <button type="submit" class="btn btn-sol btn-lg" [disabled]="!montoValido() || pagando()"><app-icono nombre="billetera" [tamano]="18" />Recargar</button>
        </form>
      </div>
    </section>

    <!-- Confirmar compra -->
    <app-modal [(abierto)]="compraAbierta" [titulo]="tituloCompra()" subtitulo="Revisa el detalle antes de pagar.">
      <div class="space-y-5">
        @if (conceptoCompra() === 'Destacar') {
          <div class="campo">
            <label for="pub-destacar" class="etiqueta">¿Qué publicación quieres destacar?</label>
            <select id="pub-destacar" class="entrada" [(ngModel)]="publicacionId">
              <option value="">Elige una publicación disponible</option>
              @for (p of disponibles(); track p.id) {
                <option [value]="p.id">{{ p.titulo }}</option>
              }
            </select>
            @if (!disponibles().length) {
              <p class="ayuda">No tienes publicaciones disponibles. <a routerLink="/publicar" class="enlace">Publica algo</a>.</p>
            }
          </div>
        }
        @if (conceptoCompra() === 'Verificar') {
          <div class="campo">
            <span class="etiqueta">Documento de identidad</span>
            @if (config.subidasHabilitadas()) {
              <label class="flex cursor-pointer items-center gap-3 rounded-2xl border-2 border-dashed border-borde p-4 hover:border-bosque-300">
                <app-icono [nombre]="documentoUrl() ? 'checkCirculo' : 'subir'" [class]="documentoUrl() ? 'text-bosque-600' : 'text-tenue'" />
                <span class="text-sm">{{ subiendoDocumento() ? 'Subiendo…' : documentoUrl() ? 'Documento cargado. Toca para cambiarlo.' : 'Sube una foto o PDF de tu cédula' }}</span>
                <input type="file" class="sr-only" accept="application/pdf,image/jpeg,image/png,image/webp" (change)="subirDocumento($event)" />
              </label>
              <p class="ayuda">Es privado: solo lo ve el equipo de moderación y se elimina al resolver la solicitud.</p>
            } @else {
              <p class="rounded-xl bg-sol-50 p-3 text-sm dark:bg-sol-500/10">La carga de documentos no está disponible en este momento.</p>
            }
          </div>
        }

        @if (esPlan()) {
          <dl class="space-y-2 rounded-2xl bg-superficie-2 p-4 text-sm">
            <div class="flex justify-between"><dt>{{ tituloCompra() }} · {{ politica()?.duracionSuscripcionDias ?? 30 }} días</dt><dd>{{ precioPlan() | cop }}</dd></div>
            @if (politica()?.preciosIncluyenIva) {
              <div class="flex justify-between text-tenue"><dt>IVA incluido</dt><dd>{{ ivaDe(precioPlan()) | cop }}</dd></div>
            }
            <div class="flex justify-between border-t border-borde pt-2 text-base font-bold"><dt>Total</dt><dd>{{ precioPlan() | cop }}</dd></div>
          </dl>
          @if (planActual() === conceptoCompra()) {
            <p class="text-sm text-tenue">Tu plan vence el {{ resumen.value()?.planVigenteHasta | fecha }}: los {{ politica()?.duracionSuscripcionDias ?? 30 }} días se suman a partir de esa fecha.</p>
          }
        } @else if (cotizacion.value(); as c) {
          <dl class="space-y-2 rounded-2xl bg-superficie-2 p-4 text-sm">
            <div class="flex justify-between"><dt>Precio</dt><dd>{{ c.precioBaseCop | cop }}</dd></div>
            @if (c.descuentoPorcentaje) {
              <div class="flex justify-between text-bosque-700 dark:text-bosque-300">
                <dt>Descuento ({{ c.descuentoPorcentaje }}%{{ (resumen.value()?.descuentoPlanPorcentaje ?? 0) > 0 ? ', incluye ' + resumen.value()?.descuentoPlanPorcentaje + '% de tu plan' : '' }})</dt>
                <dd>−{{ (c.precioBaseCop ?? 0) - (c.totalCop ?? 0) | cop }}</dd>
              </div>
              @if (c.puntosACanjear) {
                <div class="flex justify-between text-tenue"><dt>Eco-Puntos que se canjean</dt><dd>{{ c.puntosACanjear | numero }}</dd></div>
              }
            }
            @if (c.ivaIncluidoCop) {
              <div class="flex justify-between text-tenue"><dt>IVA incluido</dt><dd>{{ c.ivaIncluidoCop | cop }}</dd></div>
            }
            <div class="flex justify-between border-t border-borde pt-2 text-base font-bold"><dt>Total</dt><dd>{{ c.totalCop | cop }}</dd></div>
          </dl>
        } @else if (cotizacion.isLoading()) {
          <div class="esqueleto h-28"></div>
        }
        <p class="text-xs text-tenue">Al pagar aceptas los <a routerLink="/terminos" class="enlace" target="_blank">términos</a>. Recibirás factura electrónica.</p>
        @if (errorCompra()) {
          <p class="error-campo" role="alert">{{ errorCompra() }}</p>
        }
      </div>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="compraAbierta.set(false)">Cancelar</button>
        <button type="button" class="btn btn-primario" [disabled]="!compraLista() || pagando()" (click)="pagar()">
          <app-icono nombre="candado" [tamano]="16" />{{ pagando() ? 'Procesando…' : 'Ir a pagar' }}
        </button>
      </div>
    </app-modal>
  `,
})
export default class EcoPuntos {
  private readonly api = inject(CuentaApi);
  private readonly catalogo = inject(CatalogoApi);
  private readonly subidas = inject(SubidasService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  protected readonly sesion = inject(SesionService);
  protected readonly config = inject(ConfigService);

  /** `?destacar=<id>` llega desde el detalle de una publicación propia. */
  readonly destacar = input<string>();

  protected readonly politica = toSignal(this.api.politica$.pipe(catchError(() => of(null))));
  protected readonly resumen = rxResource({
    params: () => (this.sesion.autenticado() ? true : undefined),
    stream: () => this.api.resumenPuntos(),
  });
  private readonly mias = rxResource({
    params: () => (this.sesion.autenticado() && this.conceptoCompra() === 'Destacar' ? true : undefined),
    stream: () => this.catalogo.mias(),
  });
  protected readonly disponibles = computed(() => (this.mias.value() ?? []).filter((p) => p.estado === 'Disponible' && !p.destacada));

  protected readonly servicios = computed<Servicio[]>(() => {
    const p = this.politica();
    const escalones = (e?: { puntosMinimos?: number; descuentoPorcentaje?: number }[]) =>
      (e ?? []).map((x) => `${x.descuentoPorcentaje}% de descuento con ${x.puntosMinimos} puntos`);
    return [
      {
        concepto: 'Destacar',
        titulo: 'Destacar publicación',
        descripcion: 'Aparece primero en el catálogo y llega a más vecinos.',
        icono: 'destello',
        precio: p?.precioDestacarCop ?? 6000,
        extra: '',
        beneficios: [`Visible arriba durante ${p?.duracionDestacadoDias ?? 7} días`, ...escalones(p?.escalonesDestacar)],
      },
      {
        concepto: 'Verificar',
        titulo: 'Cuenta verificada',
        descripcion: 'Muestra la insignia de verificación y genera más confianza.',
        icono: 'verificado',
        precio: p?.precioVerificarCop ?? 20000,
        extra: ' pago único',
        beneficios: ['Insignia visible en tu perfil', 'Apareces en el filtro de verificados', ...escalones(p?.escalonesVerificar)],
      },
      {
        concepto: 'Premium',
        titulo: 'Premium',
        descripcion: 'Para quienes intercambian seguido.',
        icono: 'corona',
        precio: p?.precioPremiumCop ?? 15000,
        extra: ' / mes',
        beneficios: [
          `${p?.destacadosGratisPremium ?? 3} destacados gratis al mes`,
          `${p?.descuentoPremiumPorcentaje ?? 15}% de descuento adicional en destacar y verificar`,
          `Hasta ${p?.maxPublicacionesPremium ?? 150} publicaciones activas`,
          'Gráfica diaria de vistas de tus publicaciones',
          'Insignia Premium en tu perfil',
        ],
        destacado: true,
      },
      {
        concepto: 'Empresa',
        titulo: 'Empresa',
        descripcion: 'Para tiendas de segunda mano, reparadores, fundaciones y emprendimientos circulares.',
        icono: 'edificio',
        precio: p?.precioEmpresaCop ?? 50000,
        extra: ' / mes',
        beneficios: [
          `${p?.destacadosGratisEmpresa ?? 10} destacados gratis al mes`,
          `${p?.descuentoEmpresaPorcentaje ?? 20}% de descuento adicional`,
          `Hasta ${p?.maxPublicacionesEmpresa ?? 1000} publicaciones activas`,
          'Nombre comercial y NIT en tus publicaciones',
          'Vende sin el límite de artículos para cuentas sin verificar',
          'Gráfica diaria de vistas y factura a nombre de tu empresa',
        ],
      },
    ];
  });

  protected readonly planActual = computed(() => {
    const t = this.resumen.value()?.tipoCuenta;
    return t === 'Premium' || t === 'Empresa' ? t : null;
  });
  protected readonly esPlan = computed(() => this.conceptoCompra() === 'Premium' || this.conceptoCompra() === 'Empresa');
  protected readonly precioPlan = computed(() =>
    this.conceptoCompra() === 'Empresa' ? (this.politica()?.precioEmpresaCop ?? 50000) : (this.politica()?.precioPremiumCop ?? 15000),
  );

  /** Parte del precio que corresponde al IVA (los precios lo incluyen). */
  protected ivaDe(total: number): number {
    const iva = this.politica()?.ivaPorcentaje ?? 0;
    return iva > 0 ? total - Math.round((total / (1 + iva / 100)) * 100) / 100 : 0;
  }

  // Recarga
  protected readonly montoRecarga = signal<number | null>(20000);
  protected readonly puntosRecarga = computed(() => Math.floor((this.montoRecarga() ?? 0) / (this.politica()?.copPorEcoPunto ?? 100)));
  protected readonly montoValido = computed(() => {
    const m = this.montoRecarga() ?? 0;
    const p = this.politica();
    return !!p && m >= (p.recargaMinimaCop ?? 0) && m <= (p.recargaMaximaCop ?? Infinity);
  });

  // Compra
  protected readonly compraAbierta = signal(false);
  protected readonly conceptoCompra = signal<Exclude<ConceptoPagoDto, 'Recarga'> | null>(null);
  protected readonly publicacionId = signal('');
  protected readonly documentoUrl = signal<string | null>(null);
  protected readonly subiendoDocumento = signal(false);
  protected readonly pagando = signal(false);
  protected readonly errorCompra = signal<string | null>(null);
  protected readonly tituloCompra = computed(() => this.servicios().find((s) => s.concepto === this.conceptoCompra())?.titulo ?? '');
  /** Solo Destacar y Verificar tienen descuento por Eco-Puntos (los planes tienen precio fijo). */
  protected readonly cotizacion = rxResource({
    params: () => {
      const c = this.conceptoCompra();
      return this.compraAbierta() && (c === 'Destacar' || c === 'Verificar') ? c : undefined;
    },
    stream: ({ params }) => this.api.cotizacion(params),
  });
  protected readonly compraLista = computed(() => {
    switch (this.conceptoCompra()) {
      case 'Destacar':
        return !!this.publicacionId();
      case 'Verificar':
        return !!this.documentoUrl();
      default:
        return !!this.conceptoCompra();
    }
  });

  constructor() {
    effect(() => {
      const id = this.destacar();
      if (id && this.sesion.autenticado()) {
        untracked(() => {
          this.publicacionId.set(id);
          this.abrirCompra('Destacar');
        });
      }
    });
  }

  protected iconoModo(m?: string): string {
    return INFO_MODO[m as ModoDto]?.icono ?? 'hoja';
  }

  protected etiquetaModo(m?: string): string {
    return (INFO_MODO[m as ModoDto]?.etiqueta ?? m ?? '').toLowerCase();
  }

  protected abrirCompra(concepto: Exclude<ConceptoPagoDto, 'Recarga'>): void {
    if (!this.exigirSesion()) return;
    this.conceptoCompra.set(concepto);
    this.errorCompra.set(null);
    if (concepto !== 'Destacar') this.publicacionId.set(this.destacar() ?? '');
    this.compraAbierta.set(true);
  }

  protected async subirDocumento(e: Event): Promise<void> {
    const input = e.target as HTMLInputElement;
    const archivo = input.files?.[0];
    input.value = '';
    if (!archivo) return;
    const invalido = this.subidas.validar(archivo, 'Documento');
    if (invalido) {
      this.errorCompra.set(invalido);
      return;
    }
    this.subiendoDocumento.set(true);
    this.errorCompra.set(null);
    try {
      this.documentoUrl.set(await this.subidas.subirTodo(archivo, 'Documento'));
    } catch (err) {
      this.errorCompra.set(mensajeDe(err));
    } finally {
      this.subiendoDocumento.set(false);
    }
  }

  protected async pagar(): Promise<void> {
    const concepto = this.conceptoCompra();
    if (!concepto) return;
    await this.iniciar({
      concepto,
      publicacionId: concepto === 'Destacar' ? this.publicacionId() : null,
      documentoUrl: concepto === 'Verificar' ? this.documentoUrl() : null,
    });
  }

  protected async recargar(): Promise<void> {
    if (!this.exigirSesion() || !this.montoValido()) return;
    await this.iniciar({ concepto: 'Recarga', montoRecargaCop: this.montoRecarga() });
  }

  private async iniciar(r: IniciarPagoRequest): Promise<void> {
    this.pagando.set(true);
    this.errorCompra.set(null);
    try {
      const pago = await firstValueFrom(this.api.iniciarPago(r));
      this.compraAbierta.set(false);
      if (pago.proveedor === 'Wompi' && pago.llavePublica) {
        abrirCheckoutWompi(pago, `${window.location.origin}/pagos/${encodeURIComponent(pago.referencia ?? '')}`);
      } else {
        // Pasarela simulada (solo en desarrollo: el backend no arranca con ella en producción).
        await this.router.navigate(['/pagos', pago.referencia], { queryParams: { simulado: 1 } });
      }
    } catch (e) {
      if (this.compraAbierta()) this.errorCompra.set(mensajeDe(e));
      else this.avisos.error(mensajeDe(e));
    } finally {
      this.pagando.set(false);
    }
  }

  private exigirSesion(): boolean {
    if (this.sesion.autenticado()) return true;
    void this.router.navigate(['/ingresar'], { queryParams: { volver: '/eco-puntos' } });
    return false;
  }
}
