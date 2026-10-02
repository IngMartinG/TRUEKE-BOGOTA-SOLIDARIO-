import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MOTIVOS_DENUNCIA, type AccionDenunciaDto, type DenunciaAgrupadaDto, type EstadoDenunciaDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { HacePipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, EstadoVacio, HacePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-bold">Denuncias</h2>
      <div class="pestanas">
        @for (e of estados; track e) {
          <button type="button" class="pestana" [class.pestana-activa]="estado() === e" (click)="estado.set(e)">{{ e }}</button>
        }
      </div>
    </div>

    <ul class="mt-6 space-y-3">
      @if (recurso.isLoading()) {
        @for (i of [1, 2]; track i) {
          <li class="esqueleto h-36 rounded-tarjeta"></li>
        }
      }
      @for (d of recurso.value() ?? []; track d.denunciaId) {
        <li class="tarjeta p-5">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div class="flex flex-wrap items-center gap-2">
                <span class="insignia-neutra">{{ tipos[d.tipo ?? ''] ?? d.tipo }}</span>
                <span class="insignia bg-tierra-100 text-tierra-700">{{ d.total }} {{ d.total === 1 ? 'reporte' : 'reportes' }}</span>
                @if (!d.objetivoExiste) {
                  <span class="insignia-neutra">El contenido ya no existe</span>
                }
              </div>
              <p class="mt-2 text-sm">
                @for (m of d.motivos ?? []; track m; let ultimo = $last) {
                  <strong>{{ motivo(m) }}</strong>{{ ultimo ? '' : ' · ' }}
                }
              </p>
              <p class="text-xs text-tenue">Primer reporte {{ d.primeraUtc | hace }} · último {{ d.ultimaUtc | hace }}</p>
            </div>
            @if (enlace(d); as ruta) {
              <a [routerLink]="ruta" target="_blank" class="btn btn-secundario btn-sm"><app-icono nombre="externo" [tamano]="14" />Ver contenido</a>
            }
          </div>
          @if (d.vistaPrevia) {
            <blockquote class="mt-3 rounded-xl border-l-4 border-tierra-500 bg-superficie-2 p-3 text-sm break-words whitespace-pre-line">{{ d.vistaPrevia }}</blockquote>
          }
          @if (d.detalles?.length) {
            <details class="mt-3 text-sm">
              <summary class="cursor-pointer font-semibold text-tenue">Comentarios de quienes reportaron ({{ d.detalles!.length }})</summary>
              <ul class="mt-2 list-disc space-y-1 pl-5 text-tenue">
                @for (det of d.detalles; track $index) {
                  <li class="break-words">{{ det }}</li>
                }
              </ul>
            </details>
          }
          @if (estado() === 'Pendiente') {
            <div class="mt-4 flex flex-wrap justify-end gap-2 border-t border-borde pt-4">
              <button type="button" class="btn btn-fantasma btn-sm" (click)="abrir(d, 'Descartar')">Descartar</button>
              <button type="button" class="btn btn-secundario btn-sm" (click)="abrir(d, 'MarcarRevisada')">Marcar revisada</button>
              @if (d.objetivoExiste && d.tipo !== 'Usuario') {
                <button type="button" class="btn btn-peligro btn-sm" (click)="abrir(d, 'OcultarContenido')"><app-icono nombre="ojoNo" [tamano]="14" />Ocultar contenido</button>
              }
            </div>
          }
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="checkCirculo" [titulo]="estado() === 'Pendiente' ? '¡Todo en orden!' : 'Sin denuncias en este estado'" descripcion="No hay denuncias para revisar." />
    }

    <app-modal [(abierto)]="abierto" [titulo]="titulos[accion()]" subtitulo="Quienes reportaron recibirán una notificación.">
      <form id="form-resolver" (ngSubmit)="resolver()" class="campo">
        <label for="nota" class="etiqueta">Nota {{ accion() === 'OcultarContenido' ? '(motivo que verá el autor)' : '(opcional)' }}</label>
        <textarea id="nota" name="nota" class="entrada" maxlength="300" [(ngModel)]="nota"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-resolver" class="btn" [class]="accion() === 'OcultarContenido' ? 'btn-peligro' : 'btn-primario'"
          [disabled]="(accion() === 'OcultarContenido' && nota().trim().length < 3) || trabajando()">Confirmar</button>
      </div>
    </app-modal>
  `,
})
export default class Denuncias {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly estados: EstadoDenunciaDto[] = ['Pendiente', 'Resuelta', 'Descartada'];
  protected readonly estado = signal<EstadoDenunciaDto>('Pendiente');
  protected readonly tipos: Record<string, string> = {
    Publicacion: 'Publicación',
    Comentario: 'Comentario',
    Mensaje: 'Mensaje',
    Usuario: 'Usuario',
    Calificacion: 'Calificación',
  };
  protected readonly recurso = rxResource({ params: () => this.estado(), stream: ({ params }) => this.api.denuncias(params) });
  protected readonly titulos: Record<AccionDenunciaDto, string> = {
    Descartar: 'Descartar denuncia',
    MarcarRevisada: 'Marcar como revisada',
    OcultarContenido: 'Ocultar el contenido',
  };
  protected readonly abierto = signal(false);
  protected readonly accion = signal<AccionDenunciaDto>('Descartar');
  protected readonly nota = signal('');
  protected readonly trabajando = signal(false);
  private seleccionada: DenunciaAgrupadaDto | null = null;

  protected motivo(m: string): string {
    return MOTIVOS_DENUNCIA.find((x) => x.valor === m)?.etiqueta ?? m;
  }

  protected enlace(d: DenunciaAgrupadaDto): unknown[] | null {
    if (!d.objetivoExiste || !d.objetivoId) return null;
    if (d.tipo === 'Publicacion') return ['/publicacion', d.objetivoId];
    if (d.tipo === 'Usuario') return ['/usuarios', d.objetivoId];
    return null;
  }

  protected abrir(d: DenunciaAgrupadaDto, accion: AccionDenunciaDto): void {
    this.seleccionada = d;
    this.accion.set(accion);
    this.nota.set('');
    this.abierto.set(true);
  }

  protected async resolver(): Promise<void> {
    const d = this.seleccionada;
    if (!d?.denunciaId) return;
    this.trabajando.set(true);
    try {
      await firstValueFrom(this.api.resolverDenuncia(d.denunciaId, this.accion(), this.nota().trim() || null));
      this.abierto.set(false);
      this.avisos.exito('Denuncia resuelta');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.trabajando.set(false);
    }
  }
}
