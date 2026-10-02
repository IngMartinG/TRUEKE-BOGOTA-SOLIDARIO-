import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { filter, firstValueFrom, type Observable } from 'rxjs';
import type { SolicitudDto } from '../../api/tipos';
import { IntercambiosApi } from '../../core/api/intercambios.api';
import { AvisosService } from '../../core/avisos.service';
import { SesionService } from '../../core/sesion.service';
import { TiempoRealService } from '../../core/tiempo-real.service';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Estrellas } from '../../shared/ui/estrellas';
import { Modal } from '../../shared/ui/modal';
import { TarjetaSolicitud, type AccionSolicitud } from './tarjeta-solicitud';

type Pestana = 'recibidas' | 'enviadas';
const TIPOS_SOLICITUD = /^(Solicitud|Entrega|Intercambio|Calificacion)/;

@Component({
  imports: [FormsModule, RouterLink, TarjetaSolicitud, EstadoVacio, Modal, Estrellas],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-4xl py-8 sm:py-10">
      <h1 class="text-3xl font-extrabold">Mis intercambios</h1>
      <p class="mt-1 text-tenue">Responde solicitudes, coordina entregas y califica a tu comunidad.</p>

      <div class="mt-6 flex flex-wrap items-center justify-between gap-3">
        <div class="pestanas" role="tablist">
          <button type="button" role="tab" class="pestana" [class.pestana-activa]="pestana() === 'recibidas'" [attr.aria-selected]="pestana() === 'recibidas'" (click)="cambiar('recibidas')">
            Recibidas
            @if (pendientesRecibidas() > 0) {
              <span class="ml-1 rounded-full bg-tierra-600 px-1.5 text-[10px] text-white">{{ pendientesRecibidas() }}</span>
            }
          </button>
          <button type="button" role="tab" class="pestana" [class.pestana-activa]="pestana() === 'enviadas'" [attr.aria-selected]="pestana() === 'enviadas'" (click)="cambiar('enviadas')">
            Enviadas
          </button>
        </div>
        <div class="flex gap-1 text-sm" role="group" aria-label="Filtrar por estado">
          @for (f of filtros; track f.valor) {
            <button type="button" class="rounded-full px-3 py-1 font-medium transition" [class]="filtroEstado() === f.valor ? 'bg-superficie-2 text-tinta' : 'text-tenue hover:text-tinta'" (click)="filtroEstado.set(f.valor)">
              {{ f.texto }}
            </button>
          }
        </div>
      </div>

      <div class="mt-6 space-y-4">
        @if (lista.isLoading() && !lista.value()) {
          @for (i of [1, 2]; track i) {
            <div class="esqueleto h-56 rounded-tarjeta"></div>
          }
        } @else {
          @for (s of visibles(); track s.id) {
            <app-tarjeta-solicitud class="block animate-aparecer" [solicitud]="s" [ocupado]="ocupado() === s.id" (accion)="ejecutar(s, $event)" />
          } @empty {
            @if (lista.value()?.length) {
              <app-estado-vacio icono="checkCirculo" titulo="Nada pendiente por aquí" descripcion="No tienes intercambios en este estado.">
                <button type="button" class="btn btn-secundario" (click)="filtroEstado.set('todas')">Ver todos</button>
              </app-estado-vacio>
            } @else if (pestana() === 'recibidas') {
              <app-estado-vacio icono="apreton" titulo="Aún no recibes solicitudes" descripcion="Cuando alguien quiera tu objeto, lo verás aquí. Las publicaciones con buenas fotos reciben más solicitudes.">
                <a routerLink="/publicar" class="btn btn-primario">Publicar algo</a>
              </app-estado-vacio>
            } @else {
              <app-estado-vacio icono="brujula" titulo="No has enviado solicitudes" descripcion="Explora el catálogo y propone un trueke, compra o pide una donación.">
                <a routerLink="/explorar" class="btn btn-primario">Explorar el catálogo</a>
              </app-estado-vacio>
            }
          }
        }
      </div>
    </div>

    <!-- Diálogo con motivo (rechazar / no concretada) -->
    <app-modal [(abierto)]="motivoAbierto" [titulo]="accionMotivo() === 'rechazar' ? 'Rechazar solicitud' : 'El intercambio no se concretó'"
      [subtitulo]="accionMotivo() === 'rechazar' ? 'La publicación seguirá disponible para otras personas.' : 'La publicación volverá a estar disponible.'">
      <form id="form-motivo" (ngSubmit)="confirmarMotivo()" class="campo">
        <label for="motivo" class="etiqueta">Motivo {{ accionMotivo() === 'rechazar' ? '(opcional)' : '' }}</label>
        <textarea id="motivo" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivo"
          [placeholder]="accionMotivo() === 'rechazar' ? 'Ej: ya acordé con otra persona' : 'Ej: no llegamos a un acuerdo sobre el lugar'"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="motivoAbierto.set(false)">Volver</button>
        <button type="submit" form="form-motivo" class="btn btn-peligro" [disabled]="accionMotivo() === 'noConcretada' && motivo().trim().length < 3">Confirmar</button>
      </div>
    </app-modal>

    <!-- Calificar -->
    <app-modal [(abierto)]="calificarAbierto" titulo="¿Cómo te fue?" subtitulo="Tu calificación ayuda a que la comunidad sea más confiable.">
      <form id="form-calificar" (ngSubmit)="enviarCalificacion()" class="space-y-5">
        <div class="flex flex-col items-center gap-2">
          <app-estrellas [(valor)]="estrellas" [editable]="true" [tamano]="36" />
          <p class="text-sm font-semibold text-tenue">{{ textoEstrellas[estrellas()] }}</p>
        </div>
        <div class="campo">
          <label for="comentario-calificacion" class="etiqueta">Comentario (opcional)</label>
          <textarea id="comentario-calificacion" name="comentario" class="entrada" maxlength="500" [(ngModel)]="comentario" placeholder="¿Fue puntual? ¿El objeto era como en la publicación?"></textarea>
        </div>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="calificarAbierto.set(false)">Después</button>
        <button type="submit" form="form-calificar" class="btn btn-sol" [disabled]="estrellas() === 0">Enviar calificación</button>
      </div>
    </app-modal>
  `,
})
export default class Intercambios {
  private readonly api = inject(IntercambiosApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionService);

  readonly tab = input<string>();
  protected readonly pestana = computed<Pestana>(() => (this.tab() === 'enviadas' ? 'enviadas' : 'recibidas'));
  protected readonly filtroEstado = signal<'activas' | 'todas' | 'cerradas'>('activas');
  protected readonly filtros = [
    { valor: 'activas' as const, texto: 'En curso' },
    { valor: 'cerradas' as const, texto: 'Cerradas' },
    { valor: 'todas' as const, texto: 'Todas' },
  ];
  protected readonly ocupado = signal<string | null>(null);

  protected readonly lista = rxResource({
    params: () => this.pestana(),
    stream: ({ params }) => (params === 'enviadas' ? this.api.enviadas() : this.api.recibidas()),
  });
  /** Solo hace falta pedirlas aparte en la pestaña "Enviadas" (para el contador de pendientes). */
  private readonly recibidas = rxResource({
    params: () => (this.pestana() === 'enviadas' ? true : undefined),
    stream: () => this.api.recibidas(),
  });
  protected readonly pendientesRecibidas = computed(() =>
    ((this.pestana() === 'recibidas' ? this.lista.value() : this.recibidas.value()) ?? []).filter((s) => s.estado === 'Pendiente').length,
  );

  protected readonly visibles = computed(() => {
    const todas = [...(this.lista.value() ?? [])].sort(
      (a, b) => Date.parse(b.fechaSolicitud ?? '') - Date.parse(a.fechaSolicitud ?? ''),
    );
    const activa = (s: SolicitudDto) => s.estado === 'Pendiente' || s.estado === 'Aceptada' || !!s.puedoCalificar;
    switch (this.filtroEstado()) {
      case 'activas':
        return todas.filter(activa);
      case 'cerradas':
        return todas.filter((s) => !activa(s));
      default:
        return todas;
    }
  });

  // Diálogos
  protected readonly motivoAbierto = signal(false);
  protected readonly accionMotivo = signal<'rechazar' | 'noConcretada'>('rechazar');
  protected readonly motivo = signal('');
  protected readonly calificarAbierto = signal(false);
  protected readonly estrellas = signal(0);
  protected readonly comentario = signal('');
  protected readonly textoEstrellas = ['Toca una estrella', 'Mala experiencia', 'Regular', 'Buena', 'Muy buena', '¡Excelente!'];
  private seleccionada: SolicitudDto | null = null;

  constructor() {
    const tiempoReal = inject(TiempoRealService);
    tiempoReal.notificacion$
      .pipe(
        filter((n) => TIPOS_SOLICITUD.test(n.tipo ?? '')),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(() => this.recargar());
    tiempoReal.resincronizar$.pipe(takeUntilDestroyed()).subscribe(() => this.recargar());
  }

  protected cambiar(p: Pestana): void {
    void this.router.navigate([], { queryParams: { tab: p }, replaceUrl: true });
  }

  protected ejecutar(s: SolicitudDto, accion: AccionSolicitud): void {
    this.seleccionada = s;
    switch (accion) {
      case 'aceptar':
        void this.correr(s, () => this.api.aceptar(s.id!), '¡Solicitud aceptada!', 'Ya pueden coordinar la entrega por el chat.');
        break;
      case 'cancelar':
        void this.correr(s, () => this.api.cancelar(s.id!), 'Solicitud retirada');
        break;
      case 'confirmar':
        void this.correr(s, () => this.api.confirmarEntrega(s.id!), 'Entrega confirmada', 'Cuando la otra persona también confirme, ambos recibirán Eco-Puntos.');
        break;
      case 'rechazar':
      case 'noConcretada':
        this.accionMotivo.set(accion);
        this.motivo.set('');
        this.motivoAbierto.set(true);
        break;
      case 'calificar':
        this.estrellas.set(0);
        this.comentario.set('');
        this.calificarAbierto.set(true);
        break;
    }
  }

  protected async confirmarMotivo(): Promise<void> {
    const s = this.seleccionada;
    if (!s?.id) return;
    const motivo = this.motivo().trim();
    this.motivoAbierto.set(false);
    if (this.accionMotivo() === 'rechazar') {
      await this.correr(s, () => this.api.rechazar(s.id!, motivo || null), 'Solicitud rechazada');
    } else {
      await this.correr(s, () => this.api.noConcretada(s.id!, motivo), 'Intercambio marcado como no concretado');
    }
  }

  protected async enviarCalificacion(): Promise<void> {
    const s = this.seleccionada;
    if (!s?.id || this.estrellas() === 0) return;
    this.calificarAbierto.set(false);
    await this.correr(s, () => this.api.calificar(s.id!, this.estrellas(), this.comentario().trim() || null), '¡Gracias por calificar!');
  }

  private async correr(
    s: SolicitudDto,
    llamada: () => Observable<unknown>,
    titulo: string,
    detalle?: string,
  ): Promise<void> {
    this.ocupado.set(s.id ?? null);
    try {
      await firstValueFrom(llamada());
      this.avisos.exito(titulo, detalle);
      this.recargar();
      this.sesion.recargarUsuario();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.ocupado.set(null);
    }
  }

  private recargar(): void {
    this.lista.reload();
    if (this.pestana() === 'enviadas') this.recibidas.reload();
  }
}
