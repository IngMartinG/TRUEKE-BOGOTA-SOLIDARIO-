import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MOTIVOS_DENUNCIA, type DenunciaRecibidaDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { FechaPipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';

const MINIMO = 20;

/** Reportes confirmados por moderación sobre el contenido o la cuenta del usuario, con su derecho a contar su versión. */
@Component({
  imports: [FormsModule, Icono, EstadoVacio, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Reportes sobre ti</h1>
    <p class="mt-1 text-tenue">
      Cuando un moderador confirma un reporte sobre tu contenido o tu cuenta, te lo contamos aquí. Nunca revelamos quién lo hizo.
      Si crees que es un error, cuéntanos tu versión: otra persona del equipo la revisará.
    </p>

    <ul class="mt-6 space-y-4">
      @if (recurso.isLoading() && !recurso.value()) {
        @for (i of [1, 2]; track i) {
          <li class="esqueleto h-40 rounded-tarjeta"></li>
        }
      }
      @for (d of recurso.value() ?? []; track d.resolucionId) {
        <li class="tarjeta p-5" [class.ring-2]="d.resolucionId === destacada" [class.ring-sol-400]="d.resolucionId === destacada">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <div class="flex flex-wrap items-center gap-2">
              <span class="insignia-neutra">{{ tipos[d.tipo ?? ''] ?? d.tipo }}</span>
              <span [class]="d.accion === 'OcultarContenido' ? 'insignia bg-tierra-100 text-tierra-700' : 'insignia-sol'">
                {{ d.accion === 'OcultarContenido' ? 'Contenido ocultado' : 'Reporte confirmado' }}
              </span>
            </div>
            <span class="text-xs text-tenue">{{ d.fechaResolucionUtc | fecha }}</span>
          </div>

          <p class="mt-3 text-sm">
            Motivo:
            @for (m of d.motivos ?? []; track m; let ultimo = $last) {
              <strong>{{ motivo(m) }}</strong>{{ ultimo ? '' : ' · ' }}
            }
          </p>
          @if (d.notaModerador) {
            <p class="mt-1 text-sm"><span class="text-tenue">Nota del moderador:</span> {{ d.notaModerador }}</p>
          }
          @if (d.vistaPrevia) {
            <blockquote class="mt-3 rounded-xl border-l-4 border-borde bg-superficie-2 p-3 text-sm break-words whitespace-pre-line">{{ d.vistaPrevia }}</blockquote>
          }

          <div class="mt-4 border-t border-borde pt-4">
            @if (d.apelacion; as a) {
              <div class="flex flex-wrap items-center gap-2">
                <app-icono nombre="balanza" [tamano]="16" />
                <p class="text-sm font-semibold">Tu versión</p>
                <span [class]="claseEstado(a.estado)">{{ etiquetaEstado(a.estado) }}</span>
              </div>
              <p class="mt-2 text-sm break-words whitespace-pre-line text-tenue">{{ a.texto }}</p>
              @if (a.respuesta) {
                <div class="mt-3 rounded-xl p-3 text-sm whitespace-pre-line"
                  [class]="a.estado === 'Aceptada' ? 'bg-bosque-50 dark:bg-bosque-950/50' : 'bg-superficie-2'">
                  <p class="mb-1 text-xs font-semibold text-tenue">Respuesta del equipo · {{ a.fechaRespuestaUtc | fecha }}</p>{{ a.respuesta }}
                </div>
              }
            } @else if (d.apelableHastaUtc) {
              @if (abierta() === d.resolucionId) {
                <form class="campo animate-aparecer" (ngSubmit)="apelar(d)">
                  <label [for]="'apelacion-' + d.resolucionId" class="etiqueta">Tu versión de lo que pasó</label>
                  <textarea [id]="'apelacion-' + d.resolucionId" name="texto" class="entrada min-h-28" maxlength="1000"
                    [(ngModel)]="texto" placeholder="Explica por qué crees que el reporte no es correcto. Sé claro y respetuoso."></textarea>
                  <div class="flex flex-wrap items-center justify-between gap-2">
                    <span class="ayuda">{{ texto().trim().length }}/1000 · mínimo {{ minimo }} caracteres</span>
                    <div class="flex gap-2">
                      <button type="button" class="btn btn-fantasma btn-sm" (click)="abierta.set(null)">Cancelar</button>
                      <button type="submit" class="btn btn-primario btn-sm" [disabled]="texto().trim().length < minimo || enviando()">
                        <app-icono nombre="enviar" [tamano]="14" />{{ enviando() ? 'Enviando…' : 'Enviar mi versión' }}
                      </button>
                    </div>
                  </div>
                </form>
              } @else {
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <p class="text-sm text-tenue">¿No estás de acuerdo? Puedes contar tu versión hasta el {{ d.apelableHastaUtc | fecha }}.</p>
                  <button type="button" class="btn btn-secundario btn-sm" (click)="abrir(d)"><app-icono nombre="balanza" [tamano]="14" />Contar mi versión</button>
                </div>
              }
            } @else {
              <p class="text-sm text-tenue">El plazo para contar tu versión ya venció.</p>
            }
          </div>
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="checkCirculo" titulo="Todo en orden" descripcion="No tienes reportes confirmados. ¡Gracias por cuidar la comunidad!" />
    }
  `,
})
export default class Reportes {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  /** La notificación trae ?r=<resolucionId> para resaltar el reporte. */
  protected readonly destacada = inject(ActivatedRoute).snapshot.queryParamMap.get('r');
  protected readonly minimo = MINIMO;
  protected readonly recurso = rxResource({ stream: () => this.api.denunciasRecibidas() });
  protected readonly abierta = signal<string | null>(null);
  protected readonly texto = signal('');
  protected readonly enviando = signal(false);
  protected readonly tipos: Record<string, string> = {
    Publicacion: 'Publicación',
    Comentario: 'Comentario',
    Mensaje: 'Mensaje del chat',
    Usuario: 'Tu cuenta',
    Calificacion: 'Calificación',
  };

  protected motivo(m: string): string {
    return MOTIVOS_DENUNCIA.find((x) => x.valor === m)?.etiqueta ?? m;
  }

  protected etiquetaEstado(e: string | undefined): string {
    return e === 'Aceptada' ? 'Te dimos la razón' : e === 'Rechazada' ? 'Se mantuvo la decisión' : 'En revisión';
  }

  protected claseEstado(e: string | undefined): string {
    return e === 'Aceptada' ? 'insignia-trueke' : e === 'Rechazada' ? 'insignia-neutra' : 'insignia-sol';
  }

  protected abrir(d: DenunciaRecibidaDto): void {
    this.texto.set('');
    this.abierta.set(d.resolucionId ?? null);
  }

  protected async apelar(d: DenunciaRecibidaDto): Promise<void> {
    const texto = this.texto().trim();
    if (!d.resolucionId || texto.length < MINIMO) return;
    this.enviando.set(true);
    try {
      await firstValueFrom(this.api.apelar(d.resolucionId, texto));
      this.avisos.exito('Recibimos tu versión', 'Otra persona del equipo la revisará y te avisaremos la decisión.');
      this.abierta.set(null);
      this.recurso.reload();
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
