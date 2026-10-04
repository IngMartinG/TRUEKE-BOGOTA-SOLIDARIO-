import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MOTIVOS_DENUNCIA, type ApelacionAdminDto, type EstadoApelacionDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { HacePipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

/** Segunda instancia: la versión de la persona denunciada frente a la decisión original. */
@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, EstadoVacio, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Apelaciones</h2>
        <p class="text-sm text-tenue">La persona denunciada cuenta su versión. Quien resolvió la denuncia no revisa su propia decisión.</p>
      </div>
      <div class="pestanas">
        @for (e of estados; track e) {
          <button type="button" class="pestana" [class.pestana-activa]="estado() === e" (click)="estado.set(e)">{{ e }}</button>
        }
      </div>
    </div>

    <ul class="mt-6 space-y-3">
      @if (recurso.isLoading()) {
        @for (i of [1, 2]; track i) {
          <li class="esqueleto h-44 rounded-tarjeta"></li>
        }
      }
      @for (a of recurso.value() ?? []; track a.id) {
        <li class="tarjeta p-5">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="flex flex-wrap items-center gap-2">
              <span class="insignia-neutra">{{ tipos[a.tipo ?? ''] ?? a.tipo }}</span>
              <span class="insignia bg-tierra-100 text-tierra-700">{{ a.accionOriginal === 'OcultarContenido' ? 'Se ocultó' : 'Marcada procedente' }}</span>
              <span class="text-xs text-tenue">Apeló {{ a.fechaUtc | hace }}</span>
            </div>
            @if (enlace(a); as ruta) {
              <a [routerLink]="ruta" target="_blank" class="btn btn-secundario btn-sm"><app-icono nombre="externo" [tamano]="14" />Ver contenido</a>
            }
          </div>

          <div class="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <p class="text-xs font-semibold tracking-wide text-tenue uppercase">La denuncia</p>
              <p class="mt-1 text-sm">
                @for (m of a.motivos ?? []; track m; let ultimo = $last) {
                  <strong>{{ motivo(m) }}</strong>{{ ultimo ? '' : ' · ' }}
                }
              </p>
              @if (a.notaOriginal) {
                <p class="mt-1 text-sm"><span class="text-tenue">Decisión:</span> {{ a.notaOriginal }}</p>
              }
              @if (a.vistaPrevia) {
                <blockquote class="mt-2 rounded-xl border-l-4 border-tierra-500 bg-superficie-2 p-3 text-sm break-words whitespace-pre-line">{{ a.vistaPrevia }}</blockquote>
              }
              @if (a.detallesDenuncias?.length) {
                <details class="mt-2 text-sm">
                  <summary class="cursor-pointer font-semibold text-tenue">Lo que dijeron quienes reportaron ({{ a.detallesDenuncias!.length }})</summary>
                  <ul class="mt-2 list-disc space-y-1 pl-5 text-tenue">
                    @for (det of a.detallesDenuncias; track $index) {
                      <li class="break-words">{{ det }}</li>
                    }
                  </ul>
                </details>
              }
            </div>
            <div>
              <p class="text-xs font-semibold tracking-wide text-tenue uppercase">La versión de la persona denunciada</p>
              <blockquote class="mt-1 rounded-xl border-l-4 border-agua-500 bg-superficie-2 p-3 text-sm break-words whitespace-pre-line">{{ a.texto }}</blockquote>
              @if (a.notaResolucion) {
                <p class="mt-2 text-sm"><span class="text-tenue">Respuesta:</span> {{ a.notaResolucion }}</p>
              }
            </div>
          </div>

          @if (estado() === 'Pendiente') {
            <div class="mt-4 flex flex-wrap items-center justify-end gap-2 border-t border-borde pt-4">
              @if (a.puedoResolver) {
                <button type="button" class="btn btn-secundario btn-sm" (click)="abrir(a, false)">Mantener la decisión</button>
                <button type="button" class="btn btn-primario btn-sm" (click)="abrir(a, true)"><app-icono nombre="refrescar" [tamano]="14" />Dar la razón y revertir</button>
              } @else {
                <p class="text-sm text-tenue">Tú resolviste la denuncia original: otra persona del equipo debe revisar esta apelación.</p>
              }
            </div>
          }
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="balanza" [titulo]="estado() === 'Pendiente' ? 'Sin apelaciones pendientes' : 'Sin apelaciones en este estado'" descripcion="Aquí llegan las versiones de las personas denunciadas." />
    }

    <app-modal [(abierto)]="abierto" [titulo]="aceptar() ? 'Dar la razón y revertir' : 'Mantener la decisión'"
      [subtitulo]="aceptar() ? 'El contenido vuelve a mostrarse y ambas partes reciben el aviso.' : 'La persona denunciada verá tu respuesta.'">
      <form id="form-apelacion" (ngSubmit)="resolver()" class="campo">
        <label for="nota-apelacion" class="etiqueta">Respuesta (la verá quien apeló)</label>
        <textarea id="nota-apelacion" name="nota" class="entrada" maxlength="300" [(ngModel)]="nota"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-apelacion" class="btn btn-primario" [disabled]="nota().trim().length < 3 || trabajando()">Confirmar</button>
      </div>
    </app-modal>
  `,
})
export default class Apelaciones {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly estados: EstadoApelacionDto[] = ['Pendiente', 'Aceptada', 'Rechazada'];
  protected readonly estado = signal<EstadoApelacionDto>('Pendiente');
  protected readonly tipos: Record<string, string> = {
    Publicacion: 'Publicación',
    Comentario: 'Comentario',
    Mensaje: 'Mensaje',
    Usuario: 'Usuario',
    Calificacion: 'Calificación',
  };
  protected readonly recurso = rxResource({ params: () => this.estado(), stream: ({ params }) => this.api.apelaciones(params) });
  protected readonly abierto = signal(false);
  protected readonly aceptar = signal(false);
  protected readonly nota = signal('');
  protected readonly trabajando = signal(false);
  private seleccionada: ApelacionAdminDto | null = null;

  protected motivo(m: string): string {
    return MOTIVOS_DENUNCIA.find((x) => x.valor === m)?.etiqueta ?? m;
  }

  protected enlace(a: ApelacionAdminDto): unknown[] | null {
    if (!a.objetivoId) return null;
    if (a.tipo === 'Publicacion' && a.accionOriginal !== 'OcultarContenido') return ['/publicacion', a.objetivoId];
    if (a.tipo === 'Usuario') return ['/usuarios', a.objetivoId];
    return null;
  }

  protected abrir(a: ApelacionAdminDto, aceptar: boolean): void {
    this.seleccionada = a;
    this.aceptar.set(aceptar);
    this.nota.set('');
    this.abierto.set(true);
  }

  protected async resolver(): Promise<void> {
    const a = this.seleccionada;
    if (!a?.id) return;
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.resolverApelacion(a.id, this.aceptar(), this.nota().trim()));
      this.abierto.set(false);
      this.avisos.exito(this.aceptar() ? 'Decisión revertida' : 'Decisión mantenida', 'Avisamos a las personas involucradas.');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }
}
