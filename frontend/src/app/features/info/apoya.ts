import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { ConfigService } from '../../core/config.service';
import { NumeroPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { CompartirProyecto } from './compartir-proyecto';

/**
 * Apoya el proyecto: aportes voluntarios por un enlace externo (Donaciones:Url). No es una compra: no da Eco-Puntos ni
 * beneficios, y no hay certificado de donación deducible de impuestos. Solo cifras reales de la comunidad.
 */
@Component({
  imports: [RouterLink, Icono, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Portada: un pulso que termina en un corazón que late. El único movimiento de la página. -->
    <section class="relative isolate overflow-hidden bg-bosque-950 text-white">
      <div class="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_70%_40%,#7a2e1d_0%,transparent_45%),radial-gradient(ellipse_at_bottom_left,#1f7a4d_0%,transparent_55%)] opacity-60"></div>

      <div class="contenedor grid grid-cols-1 items-center gap-10 pt-14 pb-24 lg:grid-cols-[1.15fr_1fr] lg:pt-20 lg:pb-32">
        <div class="animate-aparecer">
          <h1 class="font-display text-4xl leading-[1.05] font-extrabold text-white sm:text-5xl lg:text-6xl">
            Trueke es gratis porque la comunidad lo sostiene
          </h1>
          <p class="mt-6 max-w-xl text-lg text-bosque-100/85">
            No cobramos por publicar, intercambiar ni donar, y no vendemos tus datos. Mantener la plataforma en línea sí cuesta.
            Si te ha servido, un aporte del valor que quieras la mantiene viva para tus vecinos.
          </p>
          @if (url(); as enlace) {
            <div class="mt-8 flex flex-wrap items-center gap-x-5 gap-y-3">
              <a [href]="enlace" target="_blank" rel="noopener noreferrer" class="btn btn-sol btn-lg px-8 text-base">
                <app-icono nombre="heart" [tamano]="20" [relleno]="true" />Hacer un aporte<span class="sr-only"> (abre otra pestaña)</span>
              </a>
              <p class="text-sm text-bosque-100/75">Tú eliges el valor. Cada peso cuenta.</p>
            </div>
          }
        </div>

        <div class="relative mx-auto w-full max-w-md" aria-hidden="true">
          <svg viewBox="0 0 400 220" class="w-full overflow-visible">
            <path
              d="M0 130 H120 l14 -34 l18 74 l16 -98 l16 58 H226"
              pathLength="1"
              stroke-dasharray="1"
              class="animate-trazo fill-none stroke-sol-300"
              stroke-width="5"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
          <div class="absolute top-[16%] right-[2%] w-[44%] origin-center animate-latido [animation-delay:1.5s]">
            <svg viewBox="0 0 24 24" class="w-full drop-shadow-[0_18px_40px_rgb(226_92_60/0.45)]">
              <path fill="#e25c3c" d="M12 21s-7.5-4.6-9.6-9.3C1 8.4 3.1 4.5 6.8 4.5c2.1 0 3.6 1.1 4.2 2.3h2c.6-1.2 2.1-2.3 4.2-2.3 3.7 0 5.8 3.9 4.4 7.2C19.5 16.4 12 21 12 21Z" />
              <path fill="#ffffff" fill-opacity="0.25" d="M6.6 6.6c-1.6 0-2.7 1.4-2.6 3 .1.6.6.9 1.1.6.7-.5.9-1.6 1.9-2.2.5-.3.4-1.4-.4-1.4Z" />
            </svg>
          </div>
        </div>
      </div>
      <svg class="absolute -bottom-px left-0 w-full text-fondo" viewBox="0 0 1440 60" preserveAspectRatio="none" aria-hidden="true">
        <path d="M0 60V30c240-40 480-40 720-10s480 30 720-10v50Z" fill="currentColor" />
      </svg>
    </section>

    @if (url(); as enlace) {
      <!-- Lo que se paga con los aportes -->
      <section class="contenedor mt-16 grid grid-cols-1 gap-10 lg:grid-cols-[1fr_1.3fr] lg:gap-16" aria-labelledby="titulo-uso">
        <div>
          <h2 id="titulo-uso" class="titulo-seccion">Lo que tu aporte mantiene encendido</h2>
          <p class="mt-3 max-w-md text-tenue">
            Todo va a los costos de tener la plataforma funcionando, día y noche, para que cualquiera pueda darle una segunda vida a lo que ya no usa.
          </p>
        </div>
        <ul class="divide-y divide-borde rounded-tarjeta border border-borde bg-superficie">
          @for (c of costos; track c.titulo) {
            <li class="flex gap-4 p-5">
              <span class="grid size-11 shrink-0 place-items-center rounded-2xl bg-bosque-100 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200">
                <app-icono [nombre]="c.icono" [tamano]="22" />
              </span>
              <div>
                <p class="font-display font-bold">{{ c.titulo }}</p>
                <p class="mt-0.5 text-sm text-tenue">{{ c.texto }}</p>
              </div>
            </li>
          }
        </ul>
      </section>

      <!-- Cifras reales: solo si ya hay intercambios completados -->
      @if (impacto(); as d) {
        <section class="contenedor mt-20" aria-labelledby="titulo-logrado">
          <div class="rounded-[2rem] border border-bosque-200 bg-bosque-50 p-8 text-center sm:p-12 dark:border-bosque-800 dark:bg-bosque-950/50">
            <h2 id="titulo-logrado" class="titulo-seccion">Esto es lo que hemos sostenido entre todos</h2>
            <dl class="mx-auto mt-8 grid max-w-2xl grid-cols-1 gap-8 sm:grid-cols-2 [&>div]:flex [&>div]:flex-col">
              <div>
                <dt class="text-sm text-tenue">{{ d.objetosConNuevoHogar === 1 ? 'Objeto con nuevo hogar' : 'Objetos con nuevo hogar' }}</dt>
                <dd class="order-first font-display text-5xl font-extrabold text-bosque-700 tabular-nums dark:text-bosque-300">{{ d.objetosConNuevoHogar | numero }}</dd>
              </div>
              <div>
                <dt class="text-sm text-tenue">{{ d.personasQueIntercambiaron === 1 ? 'Persona que ha intercambiado' : 'Personas que han intercambiado' }}</dt>
                <dd class="order-first font-display text-5xl font-extrabold text-bosque-700 tabular-nums dark:text-bosque-300">{{ d.personasQueIntercambiaron | numero }}</dd>
              </div>
            </dl>
            <p class="mx-auto mt-6 max-w-lg text-sm text-tenue">Sin cobrarle un peso a nadie. Cada aporte hace que la cifra siga creciendo.</p>
          </div>
        </section>
      }

      <!-- Transparencia antes de aportar -->
      <section class="contenedor mt-20 max-w-3xl" aria-labelledby="titulo-claro">
        <h2 id="titulo-claro" class="titulo-seccion">Antes de aportar, para que sea claro</h2>
        <ul class="mt-6 space-y-4">
          <li class="flex gap-3"><app-icono nombre="heart" [tamano]="20" class="mt-0.5 shrink-0 text-tierra-600 dark:text-tierra-500" /><span>Es voluntario. Usar la plataforma no depende de aportar.</span></li>
          <li class="flex gap-3"><app-icono nombre="moneda" [tamano]="20" class="mt-0.5 shrink-0 text-tierra-600 dark:text-tierra-500" /><span>No es una compra: no da Eco-Puntos, planes ni ventajas dentro de la plataforma.</span></li>
          <li class="flex gap-3"><app-icono nombre="factura" [tamano]="20" class="mt-0.5 shrink-0 text-tierra-600 dark:text-tierra-500" /><span>No expedimos certificado de donación: el aporte no se puede descontar de impuestos.</span></li>
          <li class="flex gap-3"><app-icono nombre="candado" [tamano]="20" class="mt-0.5 shrink-0 text-tierra-600 dark:text-tierra-500" /><span>El pago se hace en la página segura de Wompi, fuera de Trueke Bogotá Solidario. Nunca vemos los datos de tu tarjeta o cuenta.</span></li>
        </ul>
      </section>

      <!-- Cierre -->
      <section class="contenedor mt-20 pb-16">
        <div class="relative overflow-hidden rounded-[2rem] bg-gradient-to-br from-sol-300 via-sol-400 to-sol-500 p-8 text-bosque-950 sm:p-12">
          <svg class="absolute -right-8 -bottom-12 size-72 text-white/25" viewBox="0 0 24 24" aria-hidden="true">
            <path fill="currentColor" d="M12 21s-7.5-4.6-9.6-9.3C1 8.4 3.1 4.5 6.8 4.5c2.1 0 3.6 1.1 4.2 2.3h2c.6-1.2 2.1-2.3 4.2-2.3 3.7 0 5.8 3.9 4.4 7.2C19.5 16.4 12 21 12 21Z" />
          </svg>
          <div class="relative max-w-xl">
            <h2 class="text-3xl font-extrabold text-bosque-950 sm:text-4xl">Hoy puedes ser quien la mantiene viva</h2>
            <p class="mt-3 text-bosque-950/80">El valor lo pones tú. Al terminar, Wompi te trae de vuelta a Trueke.</p>
            <a [href]="enlace" target="_blank" rel="noopener noreferrer" class="btn btn-lg mt-6 bg-bosque-950 px-8 text-white hover:bg-bosque-900">
              <app-icono nombre="heart" [tamano]="18" [relleno]="true" class="text-sol-300" />Hacer un aporte<span class="sr-only"> (abre otra pestaña)</span>
            </a>
          </div>
        </div>

        <div class="mt-10 text-center">
          <h2 class="font-display text-xl font-bold">Otras formas de apoyar</h2>
          <div class="mt-4 flex flex-wrap justify-center gap-3">
            <button type="button" class="btn btn-secundario" (click)="compartir()"><app-icono nombre="usuarios" [tamano]="18" />Invitar a tus vecinos</button>
            <a routerLink="/publicar" class="btn btn-secundario"><app-icono nombre="mas" [tamano]="18" />Publicar algo que ya no uses</a>
          </div>
        </div>
      </section>
    } @else {
      <div class="contenedor max-w-3xl py-16">
        <div class="tarjeta p-6 text-center sm:p-8">
          <p class="text-tenue">Por ahora no estamos recibiendo aportes. La mejor forma de apoyar es usar la plataforma e invitar a tus vecinos.</p>
          <div class="mt-6 flex flex-wrap justify-center gap-3">
            <a routerLink="/publicar" class="btn btn-primario">Publicar un objeto</a>
            <button type="button" class="btn btn-secundario" (click)="compartir()">Invitar a tus vecinos</button>
          </div>
        </div>
      </div>
    }
  `,
})
export default class Apoya {
  protected readonly url = inject(ConfigService).donacionUrl;
  private readonly compartirProyecto = inject(CompartirProyecto);
  private readonly datos = toSignal(inject(CatalogoApi).impacto$.pipe(catchError(() => of(null))));
  /** Solo cifras reales y con algo que mostrar (nada de presumir ceros). */
  protected readonly impacto = computed(() => {
    const d = this.datos();
    return d && (d.objetosConNuevoHogar ?? 0) > 0 ? d : null;
  });

  protected readonly costos = [
    { icono: 'nube', titulo: 'El servidor', texto: 'Donde vive la plataforma y responde cada búsqueda, mensaje y solicitud.' },
    { icono: 'cuadricula', titulo: 'La base de datos', texto: 'Guarda con cuidado las publicaciones, los intercambios y tus Eco-Puntos.' },
    { icono: 'imagen', titulo: 'Las fotos', texto: 'El almacenamiento de las imágenes de cada objeto publicado.' },
    { icono: 'correo', titulo: 'Los correos', texto: 'Los avisos de verificación, seguridad y solicitudes que te llegan.' },
    { icono: 'brujula', titulo: 'El dominio', texto: 'La dirección con la que todos encuentran la plataforma.' },
  ];

  protected compartir(): void {
    void this.compartirProyecto.compartir('Trueke Bogotá Solidario: intercambia, compra y vende de segunda mano o dona con tus vecinos. Es gratis.');
  }
}
