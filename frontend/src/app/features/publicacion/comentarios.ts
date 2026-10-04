import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { ComentarioDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { HacePipe } from '../../shared/pipes';
import { Avatar } from '../../shared/ui/avatar';
import { Denunciar } from '../../shared/ui/denunciar';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

const TAMANO = 10;

@Component({
  selector: 'app-comentarios',
  imports: [FormsModule, RouterLink, Avatar, Icono, Denunciar, Modal, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="titulo-comentarios" class="tarjeta p-6">
      <h2 id="titulo-comentarios" class="flex items-center gap-2 text-lg font-bold">
        <app-icono nombre="mensaje" [tamano]="20" class="text-bosque-600" /> Preguntas y comentarios
        <span class="insignia-neutra">{{ total() }}</span>
      </h2>

      @if (sesion.autenticado()) {
        @if (sesion.correoVerificado()) {
          <form class="mt-5 flex gap-3" (ngSubmit)="publicar()">
            <app-avatar [nombre]="sesion.usuario()?.nombreCompleto" [foto]="sesion.usuario()?.fotoUrl" [tamano]="38" class="hidden sm:inline-flex" />
            <div class="flex-1">
              <label for="nuevo-comentario" class="sr-only">Escribe un comentario</label>
              <textarea id="nuevo-comentario" name="texto" class="entrada min-h-20" maxlength="500" [(ngModel)]="texto"
                placeholder="¿Tienes alguna pregunta sobre este objeto? Evita compartir datos personales."></textarea>
              <div class="mt-2 flex items-center justify-between">
                <span class="ayuda">{{ texto().length }}/500</span>
                <button type="submit" class="btn btn-primario btn-sm" [disabled]="!texto().trim() || enviando()">
                  <app-icono nombre="enviar" [tamano]="14" /> Comentar
                </button>
              </div>
            </div>
          </form>
        } @else {
          <p class="mt-4 rounded-xl bg-sol-50 p-3 text-sm dark:bg-sol-500/10">
            Confirma tu correo para comentar. <a routerLink="/verifica-tu-correo" class="enlace">Ver cómo</a>
          </p>
        }
      } @else {
        <p class="mt-4 rounded-xl bg-superficie-2 p-3 text-sm">
          <a routerLink="/ingresar" [queryParams]="{ volver: '/publicacion/' + publicacionId() }" class="enlace">Ingresa</a> para preguntar o comentar.
        </p>
      }

      <ul class="mt-6 space-y-5">
        @for (c of comentarios(); track c.id) {
          <li class="flex gap-3" [class.opacity-60]="c.oculto">
            <a [routerLink]="['/usuarios', c.autor?.id]" class="shrink-0" [attr.aria-label]="'Perfil de ' + c.autor?.nombre">
              <app-avatar [nombre]="c.autor?.nombre" [foto]="c.autor?.fotoUrl" [tamano]="36" [verificado]="!!c.autor?.verificado" />
            </a>
            <div class="min-w-0 flex-1">
              <div class="flex flex-wrap items-center gap-x-2 text-sm">
                <a [routerLink]="['/usuarios', c.autor?.id]" class="font-semibold hover:underline">{{ c.autor?.nombre }}</a>
                @if (c.autor?.id === propietarioId()) {
                  <span class="insignia-trueke">Dueño</span>
                }
                <span class="text-xs text-tenue">{{ c.fechaUtc | hace }}</span>
              </div>
              @if (c.oculto) {
                <p class="mt-1 text-sm text-tenue italic">Comentario oculto por moderación{{ c.motivoOcultamiento ? ': ' + c.motivoOcultamiento : '' }}</p>
              }
              @if (!c.oculto || sesion.esModerador()) {
                <p class="mt-1 text-sm break-words whitespace-pre-line">{{ c.texto }}</p>
              }
              <div class="mt-1.5 flex gap-3 text-xs">
                @if (sesion.autenticado() && !c.esMio) {
                  <button type="button" class="text-tenue hover:text-tierra-600" (click)="denunciar(c)">Reportar</button>
                }
                @if (sesion.esModerador()) {
                  <button type="button" class="font-semibold text-agua-700 hover:underline dark:text-agua-100" (click)="moderar(c)">
                    {{ c.oculto ? 'Mostrar' : 'Ocultar' }}
                  </button>
                }
              </div>
            </div>
          </li>
        } @empty {
          @if (!recurso.isLoading()) {
            <li class="py-6 text-center text-sm text-tenue">Aún no hay comentarios. ¡Haz la primera pregunta!</li>
          }
        }
      </ul>

      @if (comentarios().length < total()) {
        <button type="button" class="btn btn-secundario btn-sm mt-6 w-full" (click)="pagina.set(pagina() + 1)" [disabled]="recurso.isLoading()">
          Ver más comentarios
        </button>
      }
    </section>

    @if (aDenunciar(); as id) {
      <app-denunciar [(abierto)]="denunciaAbierta" tipo="Comentario" [objetivoId]="id" />
    }

    <app-modal [(abierto)]="ocultarAbierto" titulo="Ocultar comentario" subtitulo="Su autor verá el motivo.">
      <form id="form-ocultar-comentario" (ngSubmit)="confirmarOcultar()" class="campo">
        <label for="motivo-ocultar" class="etiqueta">Motivo</label>
        <textarea id="motivo-ocultar" name="motivo" class="entrada" minlength="3" maxlength="300" [(ngModel)]="motivoOcultar" required></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="ocultarAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-ocultar-comentario" class="btn btn-peligro" [disabled]="motivoOcultar().trim().length < 3">Ocultar</button>
      </div>
    </app-modal>
  `,
})
export class Comentarios {
  private readonly api = inject(CatalogoApi);
  private readonly adminApi = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);

  readonly publicacionId = input.required<string>();
  readonly propietarioId = input<string | undefined>();

  protected readonly texto = signal('');
  protected readonly enviando = signal(false);
  protected readonly pagina = signal(1);
  protected readonly comentarios = signal<ComentarioDto[]>([]);
  protected readonly total = signal(0);
  protected readonly aDenunciar = signal<string | null>(null);
  protected readonly denunciaAbierta = signal(false);
  protected readonly ocultarAbierto = signal(false);
  protected readonly motivoOcultar = signal('');
  private aOcultar: ComentarioDto | null = null;

  protected readonly recurso = rxResource({
    params: () => ({ id: this.publicacionId(), pagina: this.pagina() }),
    stream: ({ params }) => this.api.comentarios(params.id, params.pagina, TAMANO),
  });

  constructor() {
    // "Ver más" acumula páginas en lugar de reemplazarlas.
    effect(() => {
      const valor = this.recurso.value();
      if (!valor) return;
      untracked(() => {
        const items = valor.items ?? [];
        this.comentarios.update((l) => ((valor.pagina ?? 1) <= 1 ? items : [...l, ...items]));
        this.total.set(valor.total ?? 0);
      });
    });
  }

  protected async publicar(): Promise<void> {
    const texto = this.texto().trim();
    if (!texto) return;
    this.enviando.set(true);
    try {
      const nuevo = await firstValueFrom(this.api.comentar(this.publicacionId(), texto));
      this.comentarios.update((l) => [nuevo, ...l]);
      this.total.update((t) => t + 1);
      this.texto.set('');
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }

  protected denunciar(c: ComentarioDto): void {
    this.aDenunciar.set(c.id ?? null);
    this.denunciaAbierta.set(true);
  }

  protected async moderar(c: ComentarioDto): Promise<void> {
    if (!c.id) return;
    if (!c.oculto) {
      this.aOcultar = c;
      this.motivoOcultar.set('');
      this.ocultarAbierto.set(true);
      return;
    }
    try {
      await firstValueFrom(this.adminApi.mostrarComentario(c.id));
      this.actualizarLocal(c.id, false, null);
      this.avisos.exito('Comentario visible de nuevo');
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected async confirmarOcultar(): Promise<void> {
    const c = this.aOcultar;
    const motivo = this.motivoOcultar().trim();
    if (!c?.id || motivo.length < 3) return;
    try {
      await firstValueFrom(this.adminApi.ocultarComentario(c.id, motivo));
      this.actualizarLocal(c.id, true, motivo);
      this.ocultarAbierto.set(false);
      this.avisos.exito('Comentario ocultado');
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  private actualizarLocal(id: string, oculto: boolean, motivo: string | null): void {
    this.comentarios.update((l) => l.map((x) => (x.id === id ? { ...x, oculto, motivoOcultamiento: motivo } : x)));
  }
}
