import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import type { PreferenciasAvisosDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { NotificacionesPushService } from '../../core/notificaciones-push.service';
import { esIos, PwaService } from '../../core/pwa.service';
import { Icono } from '../../shared/ui/icono';

type Clave = keyof Required<PreferenciasAvisosDto>;

/** Avisos fuera de la app: notificaciones push en este dispositivo y correos. Las notificaciones dentro de la app siempre se muestran. */
@Component({
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Avisos y notificaciones</h1>
    <p class="mt-1 text-tenue">
      Te avisamos solo cuando no tienes Trueke abierto. Dentro de la app siempre verás todas tus notificaciones.
    </p>

    <!-- ===== En este dispositivo: instalar la app y notificaciones push ===== -->
    <h2 class="mt-8 text-lg font-bold">En este dispositivo</h2>
    <section class="tarjeta mt-3 divide-y divide-borde">
      <div class="flex items-start gap-4 p-5">
        <span class="grid size-10 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900/40 dark:text-bosque-200">
          <app-icono nombre="descargar" [tamano]="20" />
        </span>
        <div class="min-w-0 flex-1">
          <p class="font-semibold">Trueke como app</p>
          @if (pwa.instalada()) {
            <p class="flex items-center gap-1.5 text-sm text-bosque-700 dark:text-bosque-200"><app-icono nombre="checkCirculo" [tamano]="16" />Ya la estás usando como app.</p>
          } @else if (pwa.puedeInstalar()) {
            <p class="text-sm text-tenue">Ábrela desde tu pantalla de inicio, sin buscarla en el navegador.</p>
            <button type="button" class="btn btn-primario btn-sm mt-3" (click)="pwa.instalar()"><app-icono nombre="descargar" [tamano]="16" />Instalar la app</button>
          } @else if (ios) {
            <p class="text-sm text-tenue">En Safari toca <strong>Compartir</strong> (el cuadro con la flecha) y luego <strong>Agregar a pantalla de inicio</strong>.</p>
          } @else {
            <p class="text-sm text-tenue">Búscala en el menú del navegador como <strong>Instalar Trueke</strong> o <strong>Agregar a la pantalla principal</strong>.</p>
          }
        </div>
      </div>

      @if (push.estado() !== 'no-disponible') {
        <div class="flex items-start gap-4 p-5">
          <span class="grid size-10 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900/40 dark:text-bosque-200">
            <app-icono nombre="campana" [tamano]="20" />
          </span>
          <div class="min-w-0 flex-1">
            <p class="font-semibold">Notificaciones en este dispositivo</p>
            @switch (push.estado()) {
              @case ('activo') {
                <p class="text-sm text-tenue">Te avisamos de solicitudes, entregas y mensajes nuevos aunque Trueke esté cerrado. Nunca mostramos el texto de un mensaje.</p>
              }
              @case ('inactivo') {
                <p class="text-sm text-tenue">Recibe un aviso en este dispositivo cuando te escriban o respondan tus solicitudes, aunque Trueke esté cerrado.</p>
              }
              @case ('bloqueado') {
                <p class="text-sm text-tierra-700 dark:text-tierra-100">Las bloqueaste en este navegador. Para activarlas, toca el candado junto a la dirección de la página y permite las notificaciones.</p>
              }
              @case ('instalar-ios') {
                <p class="text-sm text-tenue">En iPhone y iPad, Apple solo permite notificaciones con Trueke instalado: agrégalo a tu pantalla de inicio (arriba) y ábrelo desde ahí.</p>
              }
              @default {
                <p class="text-sm text-tenue">Este navegador no permite notificaciones. Seguirás recibiendo los avisos por correo.</p>
              }
            }
          </div>
          @if (push.estado() === 'activo' || push.estado() === 'inactivo') {
            <label class="mt-1 shrink-0 cursor-pointer">
              <input type="checkbox" role="switch" class="peer sr-only" [checked]="push.estado() === 'activo'" [disabled]="push.ocupado()"
                (change)="cambiarPush($any($event.target).checked)" aria-label="Notificaciones en este dispositivo" />
              <span class="relative block h-6 w-11 rounded-full bg-borde transition peer-checked:bg-bosque-600 peer-focus-visible:ring-2 peer-focus-visible:ring-bosque-400 peer-disabled:opacity-60
                after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:shadow after:transition peer-checked:after:translate-x-5" aria-hidden="true"></span>
            </label>
          }
        </div>
      }
    </section>

    <h2 class="mt-8 text-lg font-bold">Por correo</h2>
    <section class="tarjeta mt-3 divide-y divide-borde">
      @for (o of opciones; track o.clave) {
        <label class="flex cursor-pointer items-start gap-4 p-5">
          <span class="grid size-10 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900/40 dark:text-bosque-200">
            <app-icono [nombre]="o.icono" [tamano]="20" />
          </span>
          <span class="min-w-0 flex-1">
            <span class="block font-semibold">{{ o.titulo }}</span>
            <span class="block text-sm text-tenue">{{ o.descripcion }}</span>
          </span>
          <!-- Interruptor accesible: un checkbox con apariencia de switch -->
          <input type="checkbox" role="switch" class="peer sr-only" [checked]="valores()[o.clave]" [disabled]="guardando() || !cargado()"
            (change)="cambiar(o.clave, $any($event.target).checked)" [attr.aria-label]="o.titulo" />
          <span class="relative mt-1 h-6 w-11 shrink-0 rounded-full bg-borde transition peer-checked:bg-bosque-600 peer-focus-visible:ring-2 peer-focus-visible:ring-bosque-400 peer-disabled:opacity-60
            after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:shadow after:transition peer-checked:after:translate-x-5" aria-hidden="true"></span>
        </label>
      }
    </section>

    <section class="mt-6 rounded-2xl border border-borde bg-superficie-2/60 p-5 text-sm">
      <p class="flex items-center gap-2 font-semibold"><app-icono nombre="candado" [tamano]="16" />Siempre te escribiremos por</p>
      <ul class="mt-2 list-disc space-y-1 pl-6 text-tenue">
        <li>seguridad de tu cuenta (confirmar el correo, recuperar la contraseña);</li>
        <li>pagos y facturas;</li>
        <li>respuestas a tus PQR;</li>
        <li>decisiones de moderación sobre tu cuenta.</li>
      </ul>
    </section>
  `,
})
export default class Avisos {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  protected readonly pwa = inject(PwaService);
  protected readonly push = inject(NotificacionesPushService);
  protected readonly ios = esIos();
  private readonly recurso = rxResource({ stream: () => this.api.preferenciasAvisos() });
  protected readonly valores = signal<Required<PreferenciasAvisosDto>>({ intercambios: true, mensajes: true, planes: true, novedades: false, resumenSemanal: false });
  protected readonly cargado = signal(false);
  protected readonly guardando = signal(false);

  protected readonly opciones: { clave: Clave; titulo: string; descripcion: string; icono: string }[] = [
    { clave: 'intercambios', titulo: 'Solicitudes e intercambios', descripcion: 'Cuando alguien solicita tu publicación, te responden, se confirma una entrega o te califican, y cuando te preguntamos si una publicación sigue disponible.', icono: 'apreton' },
    { clave: 'mensajes', titulo: 'Mensajes nuevos', descripcion: 'Un aviso (sin el texto del mensaje) como mucho cada 2 horas por conversación.', icono: 'mensaje' },
    { clave: 'planes', titulo: 'Recordatorios de tu plan', descripcion: 'Tres días antes de que venza tu plan Premium o Negocio.', icono: 'corona' },
    { clave: 'novedades', titulo: 'Novedades y consejos', descripcion: 'Mejoras de la plataforma e ideas para darle una segunda vida a tus cosas. Solo con tu autorización.', icono: 'hoja' },
    { clave: 'resumenSemanal', titulo: 'Resumen semanal', descripcion: 'Un correo a la semana con lo nuevo en tu localidad y lo que coincide con «Lo que busco». Solo si hay novedades.', icono: 'calendario' },
  ];

  constructor() {
    effect(() => {
      const v = this.recurso.value();
      untracked(() => {
        if (!v) return;
        this.valores.set({ intercambios: !!v.intercambios, mensajes: !!v.mensajes, planes: !!v.planes, novedades: !!v.novedades, resumenSemanal: !!v.resumenSemanal });
        this.cargado.set(true);
      });
    });
  }

  protected async cambiarPush(activar: boolean): Promise<void> {
    try {
      if (activar) await this.push.activar();
      else await this.push.desactivar();
      if (this.push.estado() === 'activo') this.avisos.exito('Notificaciones activadas en este dispositivo');
      else if (!activar) this.avisos.exito('Notificaciones desactivadas en este dispositivo');
    } catch {
      if (this.push.estado() !== 'bloqueado') this.avisos.error('No se pudieron activar las notificaciones', 'Inténtalo de nuevo en unos minutos.');
    }
  }

  protected async cambiar(clave: Clave, valor: boolean): Promise<void> {
    const anterior = this.valores();
    this.valores.set({ ...anterior, [clave]: valor });
    this.guardando.set(true);
    try {
      await firstValueFrom(this.api.guardarPreferenciasAvisos(this.valores()));
      this.avisos.exito('Preferencias guardadas');
    } catch {
      this.valores.set(anterior); // el interceptor ya mostró el error
    } finally {
      this.guardando.set(false);
    }
  }
}
