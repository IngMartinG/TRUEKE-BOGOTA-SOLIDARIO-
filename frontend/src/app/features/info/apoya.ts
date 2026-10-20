import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ConfigService } from '../../core/config.service';
import { Icono } from '../../shared/ui/icono';

/**
 * Apoya el proyecto: aportes voluntarios por un enlace externo (Donaciones:Url). No es una compra: no da Eco-Puntos ni
 * beneficios, y no hay certificado de donación deducible de impuestos.
 */
@Component({
  imports: [RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-b from-bosque-50 to-fondo py-14 dark:from-bosque-950/60">
      <div class="contenedor max-w-3xl text-center">
        <h1 class="text-4xl font-extrabold sm:text-5xl">Apoya el proyecto</h1>
        <p class="mt-4 text-lg text-tenue">
          Trueke Bogotá Solidario es gratis para todos. Si te sirve y quieres que siga, puedes hacer un aporte voluntario, del valor que quieras.
        </p>
      </div>
    </section>

    <div class="contenedor max-w-3xl pb-16">
      @if (url(); as enlace) {
        <div class="tarjeta p-6 sm:p-8">
          <h2 class="titulo-seccion">En qué se usa</h2>
          <p class="mt-2 text-tenue">
            En lo que cuesta mantener la plataforma en línea: el servidor, la base de datos, el almacenamiento de las fotos, los correos y el dominio.
          </p>
          <ul class="mt-6 space-y-3 text-sm">
            <li class="flex gap-3"><app-icono nombre="heart" [tamano]="18" class="mt-0.5 shrink-0 text-bosque-600" />Es voluntario. Usar la plataforma no depende de aportar.</li>
            <li class="flex gap-3"><app-icono nombre="moneda" [tamano]="18" class="mt-0.5 shrink-0 text-bosque-600" />No es una compra: no da Eco-Puntos, planes ni ventajas dentro de la plataforma.</li>
            <li class="flex gap-3"><app-icono nombre="factura" [tamano]="18" class="mt-0.5 shrink-0 text-bosque-600" />No expedimos certificado de donación: el aporte no se puede descontar de impuestos.</li>
          </ul>
          <a [href]="enlace" target="_blank" rel="noopener noreferrer" class="btn btn-sol btn-lg mt-8 w-full sm:w-auto">
            <app-icono nombre="heart" [tamano]="18" />Hacer un aporte<span class="sr-only"> (abre otra pestaña)</span>
          </a>
          <p class="mt-3 text-xs text-tenue">El pago se hace en la página del medio de pago, fuera de Trueke Bogotá Solidario.</p>
        </div>
        <p class="mt-8 text-center text-sm text-tenue">
          Otra forma de apoyar: <a routerLink="/publicar" class="enlace">publica algo que ya no uses</a> o invita a tus vecinos.
        </p>
      } @else {
        <div class="tarjeta p-6 text-center sm:p-8">
          <p class="text-tenue">Por ahora no estamos recibiendo aportes. La mejor forma de apoyar es usar la plataforma e invitar a tus vecinos.</p>
          <a routerLink="/publicar" class="btn btn-primario mt-6">Publicar un objeto</a>
        </div>
      }
    </div>
  `,
})
export default class Apoya {
  protected readonly url = inject(ConfigService).donacionUrl;
}
