import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { CONOCIMIENTO, respuestaDe, TEMAS } from '../ayuda/conocimiento';
import { coincide } from '../ayuda/motor';
import { Icono } from '../../shared/ui/icono';
import { Volver } from '../../shared/ui/volver';

/** Centro de ayuda: preguntas frecuentes con buscador y consejos para intercambiar con seguridad. */
@Component({
  imports: [FormsModule, RouterLink, Icono, Volver],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-3xl py-6 sm:py-10">
      <app-volver respaldo="/" class="mb-3" />
      <h1 class="text-3xl font-extrabold sm:text-4xl">Centro de ayuda</h1>
      <p class="mt-2 text-tenue">Respuestas rápidas sobre cómo funciona Trueke. ¿No encuentras lo que buscas? Pregúntale al asistente (botón de ayuda abajo a la derecha) o escríbenos desde Soporte.</p>

      <div class="relative mt-6">
        <app-icono nombre="buscar" [tamano]="18" class="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-tenue" />
        <input type="search" class="entrada rounded-full pl-11" placeholder="Busca: puntos, factura, bloquear, entrega…" aria-label="Buscar en la ayuda"
          [ngModel]="busqueda()" (ngModelChange)="busqueda.set($event)" maxlength="60" />
      </div>

      <!-- Seguridad primero: es lo más importante en un intercambio entre desconocidos -->
      <section class="mt-8 rounded-2xl border border-sol-500/30 bg-sol-50 p-5 dark:bg-sol-600/10" aria-labelledby="titulo-seguridad">
        <h2 id="titulo-seguridad" class="flex items-center gap-2 font-bold"><app-icono nombre="escudo" [tamano]="20" />Para entregar con seguridad</h2>
        <ul class="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
          @for (c of consejos; track c) {
            <li class="flex gap-2"><app-icono nombre="check" [tamano]="16" class="mt-0.5 shrink-0 text-bosque-600" />{{ c }}</li>
          }
        </ul>
      </section>

      @for (t of temasFiltrados(); track t.id) {
        <section class="mt-8">
          <h2 class="flex items-center gap-2 text-lg font-bold"><app-icono [nombre]="t.icono" [tamano]="20" class="text-bosque-600" />{{ t.titulo }}</h2>
          <div class="tarjeta mt-3 divide-y divide-borde">
            @for (q of t.preguntas; track q.id) {
              <details class="group" [open]="!!busqueda().trim()">
                <summary class="flex cursor-pointer list-none items-center justify-between gap-3 p-4 font-semibold">
                  {{ q.p }}<app-icono nombre="abajo" [tamano]="18" class="shrink-0 text-tenue transition group-open:rotate-180" />
                </summary>
                <p class="px-4 pb-4 text-sm leading-relaxed text-tenue">{{ q.r }}</p>
                @if (q.accion; as a) {
                  @if (!a.conSesion || sesion.autenticado()) {
                    <a [routerLink]="a.ruta" [fragment]="a.fragmento" class="enlace mx-4 mb-4 inline-flex items-center gap-1 text-sm">{{ a.texto }}<app-icono nombre="derecha" [tamano]="14" /></a>
                  }
                }
              </details>
            }
          </div>
        </section>
      } @empty {
        <p class="mt-10 text-center text-tenue">No encontramos respuestas para "{{ busqueda() }}".</p>
      }

      <div class="tarjeta mt-10 flex flex-col items-center gap-3 p-6 text-center sm:flex-row sm:text-left">
        <app-icono nombre="soporte" [tamano]="28" class="text-agua-600" />
        <div class="flex-1">
          <p class="font-semibold">¿Sigues con dudas?</p>
          <p class="text-sm text-tenue">Radica una petición, queja o reclamo y te respondemos por escrito.</p>
        </div>
        <a [routerLink]="sesion.autenticado() ? '/cuenta/soporte' : '/pqrs'" class="btn btn-primario">Ir a Soporte</a>
      </div>
    </div>
  `,
})
export default class Ayuda {
  protected readonly busqueda = signal('');
  protected readonly sesion = inject(SesionService);
  protected readonly politica = toSignal(inject(CuentaApi).politica$.pipe(catchError(() => of(null))));

  protected readonly consejos = [
    'Coordina todo por el chat de Trueke: no compartas tu número ni tu correo.',
    'Entrega de día en un lugar público y concurrido: una estación de TransMilenio, un centro comercial, una biblioteca pública o frente a un CAI.',
    'Revisa el objeto antes de confirmar la entrega.',
    'Nunca pagues por adelantado a alguien que no conoces.',
    'Si algo no te da confianza, cancela y repórtalo.',
    'Confirma la entrega en la app solo cuando de verdad la recibiste.',
  ];

  protected readonly temasFiltrados = computed(() => {
    const q = this.busqueda();
    const p = this.politica();
    return TEMAS.map((t) => ({
      ...t,
      preguntas: CONOCIMIENTO.filter((e) => e.tema === t.id && coincide(e, q, p)).map((e) => ({ id: e.id, p: e.pregunta, r: respuestaDe(e, p), accion: e.accion })),
    })).filter((t) => t.preguntas.length > 0);
  });
}
