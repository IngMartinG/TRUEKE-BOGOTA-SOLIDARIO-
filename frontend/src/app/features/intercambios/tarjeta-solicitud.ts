import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ETIQUETA_ESTADO_SOLICITUD, INFO_MODO, type ModoDto, type SolicitudDto } from '../../api/tipos';
import { FechaPipe, HacePipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Estrellas } from '../../shared/ui/estrellas';
import { Icono } from '../../shared/ui/icono';
import { InsigniaModo } from '../../shared/ui/insignia-modo';

export type AccionSolicitud = 'aceptar' | 'rechazar' | 'cancelar' | 'confirmar' | 'noConcretada' | 'calificar';

@Component({
  selector: 'app-tarjeta-solicitud',
  imports: [RouterLink, Avatar, Icono, InsigniaModo, Estrellas, HacePipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let s = solicitud();
    <article class="tarjeta overflow-hidden">
      <header class="flex flex-wrap items-center justify-between gap-3 border-b border-borde px-5 py-3">
        <div class="flex min-w-0 items-center gap-2">
          <app-insignia-modo [modo]="s.modo" />
          <a [routerLink]="['/publicacion', s.publicacionId]" class="truncate font-display font-bold hover:underline">{{ s.publicacionTitulo }}</a>
        </div>
        <span [class]="estado().clase">{{ estado().texto }}</span>
      </header>

      <div class="grid gap-5 p-5 md:grid-cols-[1fr_auto]">
        <div class="min-w-0">
          <a [routerLink]="['/usuarios', otra()?.id]" class="inline-flex items-center gap-3">
            <app-avatar [nombre]="otra()?.nombre" [tamano]="44" [verificado]="!!otra()?.verificado" />
            <span>
              <span class="block text-xs text-tenue">{{ s.soyDuenio ? 'Solicitado por' : 'Publicado por' }}</span>
              <span class="block font-semibold">{{ otra()?.nombre }}</span>
              @if (otra()?.calificacionPromedio) {
                <span class="flex items-center gap-1 text-xs text-tenue"><app-estrellas [valor]="otra()!.calificacionPromedio!" [tamano]="11" />({{ otra()?.totalCalificaciones }})</span>
              }
            </span>
          </a>
          <blockquote class="mt-4 rounded-2xl rounded-tl-sm bg-superficie-2 p-3 text-sm break-words whitespace-pre-line">{{ s.mensaje }}</blockquote>
          <p class="mt-2 text-xs text-tenue">Enviada {{ s.fechaSolicitud | hace }}</p>
          @if (s.motivoRechazo) {
            <p class="mt-3 text-sm"><span class="font-semibold">Motivo:</span> {{ s.motivoRechazo }}</p>
          }
        </div>

        <!-- Línea de tiempo -->
        <ol class="flex gap-4 text-xs md:w-56 md:flex-col md:gap-3" aria-label="Progreso del intercambio">
          @for (h of hitos(); track h.texto) {
            <li class="flex items-center gap-2" [class]="h.hecho ? 'text-tinta' : 'text-tenue'">
              <span class="grid size-6 shrink-0 place-items-center rounded-full" [class]="h.hecho ? 'bg-bosque-600 text-white' : 'border-2 border-borde'">
                @if (h.hecho) {
                  <app-icono nombre="check" [tamano]="12" [grosor]="3" />
                }
              </span>
              <span class="hidden font-medium sm:inline">{{ h.texto }}</span>
            </li>
          }
        </ol>
      </div>

      @if (s.estado === 'Aceptada') {
        <div class="mx-5 mb-4 rounded-2xl bg-agua-50 p-4 text-sm dark:bg-agua-700/20">
          <p class="font-semibold text-agua-700 dark:text-agua-100">Coordinen la entrega por el chat y confirmen cuando el objeto cambie de manos.</p>
          <div class="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-xs">
            <span class="flex items-center gap-1"><app-icono [nombre]="miConfirmacion() ? 'checkCirculo' : 'reloj'" [tamano]="14" />Tú: {{ miConfirmacion() ? 'confirmaste' : 'pendiente' }}</span>
            <span class="flex items-center gap-1"><app-icono [nombre]="suConfirmacion() ? 'checkCirculo' : 'reloj'" [tamano]="14" />{{ otra()?.nombre }}: {{ suConfirmacion() ? 'confirmó' : 'pendiente' }}</span>
          </div>
          @if (s.cierreAutomaticoUtc) {
            <p class="mt-2 text-xs text-tenue">Se cerrará automáticamente el {{ s.cierreAutomaticoUtc | fecha: true }} si nadie reporta un problema.</p>
          }
        </div>
      }

      @if (acciones().length || s.conversacionId) {
        <footer class="flex flex-wrap justify-end gap-2 border-t border-borde bg-superficie-2/40 px-5 py-3">
          @if (s.conversacionId) {
            <a [routerLink]="['/mensajes', s.conversacionId]" class="btn btn-secundario btn-sm"><app-icono nombre="mensaje" [tamano]="14" />Abrir chat</a>
          }
          @for (a of acciones(); track a.accion) {
            <button type="button" class="btn btn-sm" [class]="a.clase" [disabled]="ocupado()" (click)="accion.emit(a.accion)">
              <app-icono [nombre]="a.icono" [tamano]="14" />{{ a.texto }}
            </button>
          }
        </footer>
      }
    </article>
  `,
})
export class TarjetaSolicitud {
  readonly solicitud = input.required<SolicitudDto>();
  readonly ocupado = input(false);
  readonly accion = output<AccionSolicitud>();

  protected readonly otra = computed(() => (this.solicitud().soyDuenio ? this.solicitud().solicitante : this.solicitud().propietario));
  protected readonly estado = computed(
    () => ETIQUETA_ESTADO_SOLICITUD[this.solicitud().estado ?? ''] ?? { texto: this.solicitud().estado ?? '', clase: 'insignia-neutra' },
  );
  protected readonly miConfirmacion = computed(() =>
    this.solicitud().soyDuenio ? !!this.solicitud().confirmadaPorDuenio : !!this.solicitud().confirmadaPorSolicitante,
  );
  protected readonly suConfirmacion = computed(() =>
    this.solicitud().soyDuenio ? !!this.solicitud().confirmadaPorSolicitante : !!this.solicitud().confirmadaPorDuenio,
  );

  protected readonly hitos = computed(() => {
    const e = this.solicitud().estado;
    const aceptada = e === 'Aceptada' || e === 'Completada';
    return [
      { texto: 'Solicitud enviada', hecho: true },
      { texto: 'Aceptada', hecho: aceptada },
      { texto: 'Entrega confirmada', hecho: e === 'Completada' || (aceptada && this.miConfirmacion() && this.suConfirmacion()) },
      { texto: `+${INFO_MODO[this.solicitud().modo as ModoDto]?.puntos ?? 0} Eco-Puntos`, hecho: e === 'Completada' },
    ];
  });

  protected readonly acciones = computed(() => {
    const s = this.solicitud();
    const lista: { accion: AccionSolicitud; texto: string; icono: string; clase: string }[] = [];
    if (s.estado === 'Pendiente') {
      if (s.soyDuenio) {
        lista.push({ accion: 'rechazar', texto: 'Rechazar', icono: 'x', clase: 'btn-fantasma' });
        lista.push({ accion: 'aceptar', texto: 'Aceptar', icono: 'check', clase: 'btn-primario' });
      } else {
        lista.push({ accion: 'cancelar', texto: 'Retirar solicitud', icono: 'x', clase: 'btn-fantasma' });
      }
    }
    if (s.estado === 'Aceptada') {
      lista.push({ accion: 'noConcretada', texto: 'No se concretó', icono: 'alerta', clase: 'btn-fantasma' });
      if (!this.miConfirmacion()) lista.push({ accion: 'confirmar', texto: 'Confirmar entrega', icono: 'apreton', clase: 'btn-primario' });
    }
    if (s.puedoCalificar) lista.push({ accion: 'calificar', texto: 'Calificar', icono: 'estrella', clase: 'btn-sol' });
    return lista;
  });
}
