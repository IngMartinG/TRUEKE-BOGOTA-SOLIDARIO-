import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { SesionService } from '../../core/sesion.service';
import { NumeroPipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Icono } from '../../shared/ui/icono';
import { Invitar } from '../eco-puntos/invitar';

/**
 * Bienvenida guiada después de confirmar el correo: en vez de varias franjas de aviso, tres pasos claros
 * (foto, barrio y primer paso) con su progreso. Se puede volver cuando se quiera desde el aviso o la cuenta.
 */
@Component({
  imports: [RouterLink, Icono, Avatar, NumeroPipe, Invitar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-3xl py-8 sm:py-12">
      <p class="font-semibold text-bosque-700 dark:text-bosque-300">¡Hola, {{ sesion.primerNombre() }}!</p>
      <h1 class="mt-1 text-3xl font-extrabold sm:text-4xl">Bienvenida a la comunidad</h1>
      <p class="mt-2 text-tenue">
        Ya tienes <strong class="text-tinta">{{ sesion.usuario()?.saldoEcoPuntos ?? 0 | numero }} Eco-Puntos</strong> de regalo.
        Completa estos pasos y empieza a darle una segunda vida a tus cosas.
      </p>

      <div class="mt-6">
        <div class="flex items-center justify-between text-sm">
          <span class="font-semibold">{{ hechos() }} de 2 pasos listos</span>
          @if (hechos() === 2) {
            <span class="insignia-trueke"><app-icono nombre="check" [tamano]="12" />Tu cuenta está completa</span>
          }
        </div>
        <div class="mt-2 h-2 overflow-hidden rounded-full bg-superficie-2" role="progressbar" aria-label="Progreso de tu cuenta"
          [attr.aria-valuenow]="hechos()" aria-valuemin="0" aria-valuemax="2">
          <div class="h-full rounded-full bg-bosque-500 transition-[width] duration-500" [style.width.%]="hechos() * 50"></div>
        </div>
      </div>

      <ol class="mt-8 space-y-4">
        <!-- 1. Foto -->
        <li class="tarjeta flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
          <span class="relative shrink-0">
            <app-avatar [nombre]="sesion.usuario()?.nombreCompleto" [foto]="sesion.usuario()?.fotoUrl" [tamano]="56" />
            @if (sesion.tieneFoto()) {
              <span class="absolute -right-1 -bottom-1 grid size-6 place-items-center rounded-full bg-bosque-600 text-white ring-2 ring-superficie"><app-icono nombre="check" [tamano]="12" [grosor]="3" /></span>
            }
          </span>
          <div class="min-w-0 flex-1">
            <h2 class="font-bold">1. Tu foto de perfil</h2>
            <p class="text-sm text-tenue">Es obligatoria para publicar, solicitar y chatear: así la comunidad sabe con quién intercambia. Le quitamos la ubicación y los datos ocultos.</p>
          </div>
          @if (sesion.tieneFoto()) {
            <span class="text-sm font-semibold text-bosque-700 dark:text-bosque-300">Lista</span>
          } @else {
            <a routerLink="/cuenta/perfil" fragment="foto" [queryParams]="{ volver: '/bienvenida' }" class="btn btn-primario shrink-0">
              <app-icono nombre="camara" [tamano]="16" />Subir foto
            </a>
          }
        </li>

        <!-- 2. Barrio -->
        <li class="tarjeta flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
          <span class="grid size-14 shrink-0 place-items-center rounded-2xl bg-agua-100 text-agua-700 dark:bg-agua-700/30 dark:text-agua-100">
            <app-icono nombre="pin" [tamano]="26" />
          </span>
          <div class="min-w-0 flex-1">
            <h2 class="font-bold">2. Tu barrio</h2>
            <p class="text-sm text-tenue">
              @if (sesion.usuario()?.localidad) {
                Tus publicaciones aparecerán en <strong class="text-tinta">{{ sesion.usuario()?.localidad }}</strong>. Solo mostramos la zona aproximada, nunca tu dirección.
              } @else {
                Elige dónde vives para que tus vecinos encuentren tus publicaciones.
              }
            </p>
          </div>
          <a routerLink="/cuenta/perfil" class="btn btn-secundario shrink-0">{{ sesion.usuario()?.localidad ? 'Cambiar' : 'Elegir' }}</a>
        </li>

        <!-- 3. Primer paso -->
        <li class="tarjeta p-5">
          <h2 class="font-bold">3. ¿Qué quieres hacer primero?</h2>
          <div class="mt-4 grid gap-3 sm:grid-cols-3">
            <a [routerLink]="sesion.tieneFoto() ? '/publicar' : '/cuenta/perfil'" [queryParams]="sesion.tieneFoto() ? null : { volver: '/publicar' }" [fragment]="sesion.tieneFoto() ? undefined : 'foto'"
              class="group flex flex-col gap-2 rounded-2xl border-2 border-borde p-4 transition hover:border-bosque-300">
              <app-icono nombre="mas" [tamano]="22" class="text-bosque-600 dark:text-bosque-300" />
              <span class="font-semibold">Publicar algo</span>
              <span class="text-xs text-tenue">Ese objeto que ya no usas. Con buenas fotos llegan más solicitudes.</span>
            </a>
            <a routerLink="/explorar" class="group flex flex-col gap-2 rounded-2xl border-2 border-borde p-4 transition hover:border-bosque-300">
              <app-icono nombre="brujula" [tamano]="22" class="text-bosque-600 dark:text-bosque-300" />
              <span class="font-semibold">Explorar</span>
              <span class="text-xs text-tenue">Mira qué ofrecen cerca y propone un trueke, compra o pide una donación.</span>
            </a>
            <a routerLink="/como-funciona" class="group flex flex-col gap-2 rounded-2xl border-2 border-borde p-4 transition hover:border-bosque-300">
              <app-icono nombre="info" [tamano]="22" class="text-bosque-600 dark:text-bosque-300" />
              <span class="font-semibold">Ver cómo funciona</span>
              <span class="text-xs text-tenue">Trueke, compra y donación en 3 pasos, y cómo ganar Eco-Puntos.</span>
            </a>
          </div>
        </li>
      </ol>

      @if (sesion.autenticado()) {
        <div class="mt-6"><app-invitar /></div>
      }

      <p class="mt-8 text-center text-sm text-tenue">
        Cada intercambio completado te suma Eco-Puntos:
        @for (g of ganancias(); track g.modo; let ultimo = $last) {
          <strong class="text-tinta">+{{ g.puntos }}</strong> {{ g.texto }}{{ ultimo ? '.' : ' · ' }}
        }
      </p>
    </div>
  `,
})
export default class Bienvenida {
  protected readonly sesion = inject(SesionService);
  private readonly politica = toSignal(inject(CuentaApi).politica$.pipe(catchError(() => of(null))));

  /** Pasos obligatorios listos: la foto y el barrio (el tercero es elegir por dónde empezar). */
  protected readonly hechos = computed(() => (this.sesion.tieneFoto() ? 1 : 0) + (this.sesion.usuario()?.localidad ? 1 : 0));

  protected readonly ganancias = computed(() => {
    const textos: Record<string, string> = { Donacion: 'por donación', Trueke: 'por trueke', Compra: 'por compra' };
    return (this.politica()?.ganancias ?? []).map((g) => ({ modo: g.modo ?? '', puntos: g.puntos ?? 0, texto: textos[g.modo ?? ''] ?? '' }));
  });

  constructor() {
    this.sesion.recargarUsuario();
  }
}
