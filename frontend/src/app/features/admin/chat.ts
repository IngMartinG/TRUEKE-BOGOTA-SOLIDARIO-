import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, map } from 'rxjs';
import type { AccionMensajeMarcadoDto, ContextoChatDto, MensajeContextoDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { HacePipe } from '../../shared/pipes';
import { ErrorCarga } from '../../shared/ui/error-carga';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

/**
 * Revisión del chat. Nadie lee conversaciones al azar: la fila trae solo lo que el filtro marcó, y la conversación se
 * abre únicamente alrededor de un mensaje marcado o denunciado (la API lo exige y registra cada lectura).
 */
@Component({
  imports: [ErrorCarga, FormsModule, RouterLink, Icono, Modal, EstadoVacio, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (mensajeId(); as id) {
      <a routerLink="." class="btn btn-fantasma btn-sm -ml-3"><app-icono nombre="izquierda" [tamano]="14" />Volver a los mensajes marcados</a>
      <app-error-carga [recurso]="contexto" />
      @if (contexto.isLoading()) {
        <div class="esqueleto mt-4 h-96 rounded-tarjeta"></div>
      }
      @if (ctx(); as c) {
        <div class="mt-3">
          <h2 class="text-xl font-bold">Conversación{{ c.publicacion ? ' sobre «' + c.publicacion + '»' : '' }}</h2>
          <p class="mt-1 text-sm text-tenue">
            Entre {{ c.participantes?.[0]?.nombreCompleto }} (publicó) y {{ c.participantes?.[1]?.nombreCompleto }} (pidió).
            Ves hasta 10 mensajes antes y después del revisado; esta lectura queda registrada.
          </p>
        </div>

        <ol class="tarjeta mt-4 space-y-3 p-4 sm:p-5" aria-label="Mensajes de la conversación">
          @for (m of c.mensajes ?? []; track m.id) {
            <li class="flex" [class.justify-end]="!esDelDuenio(c, m)">
              <div class="max-w-[85%] rounded-2xl px-3.5 py-2 text-sm sm:max-w-[70%]"
                [class]="m.esElRevisado
                  ? 'bg-sol-100 ring-2 ring-sol-400 dark:bg-sol-500/20'
                  : esDelDuenio(c, m) ? 'bg-superficie-2' : 'bg-bosque-100 dark:bg-bosque-900'">
                <p class="text-xs font-semibold text-tenue">{{ nombre(c, m.autorId) }}</p>
                @if (m.fotoUrl) {
                  <a [href]="m.fotoUrl" target="_blank" rel="noopener noreferrer" class="mt-1 block overflow-hidden rounded-xl">
                    <img [src]="m.fotoUrl" alt="Foto enviada en el chat" loading="lazy" class="max-h-48 w-auto" />
                  </a>
                }
                @if (m.texto) {
                  <p class="mt-0.5 break-words whitespace-pre-line" [class.line-through]="m.oculto" [class.opacity-70]="m.oculto">{{ m.texto }}</p>
                }
                <p class="mt-1 flex flex-wrap items-center gap-x-2 text-[11px] text-tenue">
                  <span>{{ m.fechaUtc | hace }}</span>
                  @if (m.oculto) {
                    <span class="font-semibold">Oculto por moderación</span>
                  }
                  @if (m.motivoRevision) {
                    <span class="font-semibold text-sol-700 dark:text-sol-300">Marcado: {{ m.motivoRevision }}</span>
                  }
                </p>
              </div>
            </li>
          }
        </ol>

        @if (c.marcaPendiente) {
          <div class="mt-4 flex flex-wrap items-center justify-end gap-2">
            <button type="button" class="btn btn-fantasma btn-sm" (click)="abrir('Descartar')">No es un problema</button>
            <button type="button" class="btn btn-peligro btn-sm" (click)="abrir('Ocultar')"><app-icono nombre="ojoNo" [tamano]="14" />Ocultar el mensaje</button>
          </div>
        } @else if (c.denuncias) {
          <p class="mt-4 text-sm text-tenue">
            Este mensaje tiene {{ c.denuncias === 1 ? '1 denuncia' : c.denuncias + ' denuncias' }}: decide en
            <a routerLink="/admin/denuncias" class="enlace">Denuncias</a>, donde se avisa a quien reportó y se abre la apelación.
          </p>
        }
      }
    } @else {
      <h2 class="text-xl font-bold">Chat</h2>
      <p class="mt-1 max-w-2xl text-sm text-tenue">
        Mensajes que el filtro marcó por compartir datos de contacto, enlaces o un posible artículo prohibido. Se enviaron igual;
        solo puedes abrir la conversación alrededor de un mensaje marcado o denunciado.
      </p>
      <app-error-carga [recurso]="marcados" />
      <ul class="mt-6 space-y-3">
        @if (marcados.isLoading()) {
          @for (i of [1, 2]; track i) {
            <li class="esqueleto h-32 rounded-tarjeta"></li>
          }
        }
        @for (m of lista(); track m.mensajeId) {
          <li class="tarjeta p-5">
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="font-semibold">{{ m.autor }}</p>
                <p class="text-xs text-tenue">Marcado {{ m.marcadoUtc | hace }}</p>
              </div>
              <a routerLink="." [queryParams]="{ mensaje: m.mensajeId }" class="btn btn-secundario btn-sm"><app-icono nombre="mensaje" [tamano]="14" />Ver la conversación</a>
            </div>
            <p class="mt-3 text-sm font-semibold text-sol-700 dark:text-sol-300">{{ m.motivo }}</p>
            <blockquote class="mt-2 rounded-xl border-l-4 border-sol-400 bg-superficie-2 p-3 text-sm break-words whitespace-pre-line">{{ m.texto || '📷 Foto' }}</blockquote>
          </li>
        }
      </ul>
      @if (!marcados.isLoading() && !lista().length) {
        <app-estado-vacio icono="checkCirculo" titulo="¡Todo en orden!" descripcion="No hay mensajes marcados por revisar." />
      }
    }

    <app-modal [(abierto)]="abierto" [titulo]="accion() === 'Ocultar' ? 'Ocultar el mensaje' : 'Quitar la marca'"
      [subtitulo]="accion() === 'Ocultar' ? 'Deja de verse en el chat y su autor recibe un aviso con el motivo.' : 'El mensaje sigue igual y sale de esta fila.'">
      <form id="form-mensaje" (ngSubmit)="resolver()" class="campo">
        <label for="nota-mensaje" class="etiqueta">{{ accion() === 'Ocultar' ? 'Motivo que verá el autor' : 'Nota (opcional)' }}</label>
        <textarea id="nota-mensaje" name="nota" class="entrada" maxlength="300" [(ngModel)]="nota"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-mensaje" class="btn" [class]="accion() === 'Ocultar' ? 'btn-peligro' : 'btn-primario'"
          [disabled]="(accion() === 'Ocultar' && nota().trim().length < 3) || trabajando()">
          {{ accion() === 'Ocultar' ? 'Ocultar el mensaje' : 'Quitar la marca' }}
        </button>
      </div>
    </app-modal>
  `,
})
export default class Chat {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);

  protected readonly mensajeId = toSignal(this.ruta.queryParamMap.pipe(map((q) => q.get('mensaje'))), { initialValue: null });
  protected readonly marcados = rxResource({ stream: () => this.api.mensajesMarcados() });
  protected readonly lista = computed(() => (this.marcados.hasValue() ? this.marcados.value() : []));
  protected readonly contexto = rxResource({
    params: () => this.mensajeId() ?? undefined,
    stream: ({ params }) => this.api.contextoChat(params),
  });
  protected readonly ctx = computed(() => (this.mensajeId() && this.contexto.hasValue() ? this.contexto.value() : undefined));

  protected readonly abierto = signal(false);
  protected readonly accion = signal<AccionMensajeMarcadoDto>('Descartar');
  protected readonly nota = signal('');
  protected readonly trabajando = signal(false);

  protected esDelDuenio(c: ContextoChatDto, m: MensajeContextoDto): boolean {
    return m.autorId === c.participantes?.[0]?.id;
  }

  protected nombre(c: ContextoChatDto, autorId: string | undefined): string {
    return c.participantes?.find((p) => p.id === autorId)?.nombreCompleto ?? 'Cuenta eliminada';
  }

  protected abrir(accion: AccionMensajeMarcadoDto): void {
    this.accion.set(accion);
    this.nota.set('');
    this.abierto.set(true);
  }

  protected async resolver(): Promise<void> {
    const id = this.mensajeId();
    if (!id) return;
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.resolverMensajeMarcado(id, this.accion(), this.nota().trim() || null));
      this.abierto.set(false);
      this.avisos.exito(this.accion() === 'Ocultar' ? 'Mensaje ocultado' : 'Marca quitada');
      this.marcados.reload();
      void this.router.navigate([], { relativeTo: this.ruta, queryParams: {} });
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }
}
