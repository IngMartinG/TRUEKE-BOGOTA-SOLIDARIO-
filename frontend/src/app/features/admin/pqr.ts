import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { estadoPqr, etiquetaPqr, type DatosOferenteDto, type EstadoPqrDto, type PqrAdminDto, type VendedorDePqrDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { FechaPipe } from '../../shared/pipes';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';
import { Paginador } from '../../shared/ui/paginador';

const TAMANO = 20;

/**
 * Bandeja de PQR: las de plazo más próximo primero. Plazo legal en días hábiles colombianos: 10 para una consulta de datos
 * personales y 15 para lo demás. "Abiertas" incluye las que están en trámite.
 */
@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, Paginador, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">PQR</h2>
        <p class="text-sm text-tenue">Responde por escrito antes de la fecha límite. Retractos y reversiones: reembolsa en Wompi y regístralo en <a routerLink="../pagos" class="enlace">Pagos</a>.</p>
      </div>
      <select class="entrada w-full sm:w-44" [ngModel]="estado()" (ngModelChange)="estado.set($event); pagina.set(1)" aria-label="Filtrar por estado">
        <option value="Abierta">Pendientes</option>
        <option value="EnTramite">En trámite</option>
        <option value="Respondida">Respondidas</option>
      </select>
    </div>

    <ul class="mt-6 space-y-3">
      @for (p of recurso.value()?.items ?? []; track p.id) {
        <li class="tarjeta p-5" [class.ring-2]="p.vencida" [class.ring-tierra-400]="p.vencida">
          <div class="flex flex-wrap items-start justify-between gap-2">
            <div>
              <p class="font-semibold">{{ p.asunto }}</p>
              <p class="text-xs text-tenue">
                {{ etiqueta(p.tipo) }} · <span class="font-mono">{{ p.radicado }}</span> · {{ p.nombreUsuario }} ({{ p.correoUsuario }}) · {{ p.fechaUtc | fecha }}
                @if (p.publica) { · <span class="insignia-neutra">Sin cuenta</span> }
              </p>
            </div>
            @if (p.estado !== 'Respondida') {
              <span class="flex flex-wrap items-center gap-1.5">
                @if (p.estado === 'EnTramite') {
                  <span [class]="estadoDe(p.estado).clase">{{ estadoDe(p.estado).texto }}</span>
                }
                <span [class]="p.vencida ? 'insignia-donacion' : 'insignia-sol'">{{ p.vencida ? 'Vencida' : 'Vence' }} {{ p.fechaLimiteUtc | fecha }}</span>
              </span>
            } @else {
              <span class="insignia-trueke">Respondida {{ p.fechaRespuestaUtc | fecha }}</span>
            }
          </div>
          @if (p.pagoReferencia) {
            <p class="mt-2 text-xs">Pago: <span class="font-mono">{{ p.pagoReferencia }}</span></p>
          }
          <p class="mt-2 text-sm whitespace-pre-line">{{ p.descripcion }}</p>
          @if (!p.publica && p.id) {
            <!-- Ley 1480, art. 53: el registro de quien le vendió, solo para resolver el reclamo de quien compró -->
            @if (vendedores()[p.id]; as lista) {
              <div class="mt-3 rounded-xl border border-borde p-3 text-sm">
                <p class="font-semibold">Vendedores de sus compras completadas</p>
                @for (v of lista; track v.usuarioId) {
                  <div class="mt-2 flex flex-wrap items-center justify-between gap-2">
                    <span>{{ v.nombre }} · {{ v.publicacion }} · {{ v.fechaUtc | fecha }}</span>
                    @if (registros()[p.id + v.usuarioId]; as d) {
                      <dl class="grid w-full grid-cols-[auto_1fr] gap-x-3 gap-y-1 rounded-lg bg-superficie-2 p-3 text-xs">
                        <dt class="text-tenue">Nombre</dt><dd>{{ d.nombre }}</dd>
                        <dt class="text-tenue">Documento</dt><dd>{{ d.tipoDocumento }} {{ d.documento }}</dd>
                        <dt class="text-tenue">Dirección</dt><dd>{{ d.direccion }}, {{ d.municipio }}</dd>
                        <dt class="text-tenue">Teléfono</dt><dd>{{ d.telefono }}</dd>
                      </dl>
                    } @else if (v.tieneRegistro) {
                      <button type="button" class="btn btn-secundario btn-sm" (click)="verRegistro(p.id, v.usuarioId!)">Ver registro (queda en la auditoría)</button>
                    } @else {
                      <span class="text-xs text-tenue">Sin registro de vendedor</span>
                    }
                  </div>
                } @empty {
                  <p class="mt-1 text-tenue">No tiene compras completadas en la plataforma.</p>
                }
              </div>
            } @else {
              <button type="button" class="btn btn-fantasma btn-sm mt-2" (click)="cargarVendedores(p.id)">
                <app-icono nombre="tienda" [tamano]="14" />Datos del vendedor (art. 53)
              </button>
            }
          }
          @if (p.respuesta) {
            <p class="mt-3 rounded-xl bg-bosque-50 p-3 text-sm whitespace-pre-line dark:bg-bosque-950/50">{{ p.respuesta }}</p>
          } @else {
            <div class="mt-3 flex flex-wrap gap-2">
              <button type="button" class="btn btn-primario btn-sm" (click)="abrir(p)"><app-icono nombre="enviar" [tamano]="14" />Responder</button>
              @if (p.estado === 'Abierta') {
                <button type="button" class="btn btn-secundario btn-sm" (click)="enTramite(p)">Marcar en trámite</button>
              }
            </div>
          }
        </li>
      } @empty {
        <li class="py-10 text-center text-tenue">{{ recurso.isLoading() ? 'Cargando…' : 'No hay solicitudes en este estado.' }}</li>
      }
    </ul>
    <app-paginador [pagina]="pagina()" [total]="recurso.value()?.total ?? 0" [tamano]="tamano" (cambiar)="pagina.set($event)" />

    <app-modal [(abierto)]="abierto" titulo="Responder solicitud" [subtitulo]="seleccionada()?.radicado ?? ''">
      <form id="form-pqr" (ngSubmit)="responder()" class="campo">
        <label for="respuesta" class="etiqueta">Respuesta (se envía por correo{{ seleccionada()?.publica ? '' : ' y queda en la cuenta del usuario' }})</label>
        <textarea id="respuesta" name="respuesta" class="entrada min-h-40" maxlength="2000" [(ngModel)]="respuesta"></textarea>
        @if (error()) {<p class="error-campo" role="alert">{{ error() }}</p>}
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="abierto.set(false)">Cancelar</button>
        <button type="submit" form="form-pqr" class="btn btn-primario" [disabled]="respuesta().trim().length < 10">Enviar respuesta</button>
      </div>
    </app-modal>
  `,
})
export default class Pqr {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly tamano = TAMANO;
  protected readonly etiqueta = etiquetaPqr;
  protected readonly estadoDe = estadoPqr;
  protected readonly estado = signal<EstadoPqrDto>('Abierta');
  protected readonly pagina = signal(1);
  protected readonly recurso = rxResource({
    params: () => ({ estado: this.estado(), pagina: this.pagina() }),
    stream: ({ params }) => this.api.pqr(params.estado, params.pagina, TAMANO),
  });
  protected readonly abierto = signal(false);
  protected readonly seleccionada = signal<PqrAdminDto | null>(null);
  protected readonly respuesta = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly vendedores = signal<Record<string, VendedorDePqrDto[]>>({});
  protected readonly registros = signal<Record<string, DatosOferenteDto>>({});

  protected async cargarVendedores(pqrId: string): Promise<void> {
    try {
      const lista = await firstValueFrom(this.api.vendedoresDePqr(pqrId));
      this.vendedores.update((v) => ({ ...v, [pqrId]: lista }));
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected async verRegistro(pqrId: string, usuarioId: string): Promise<void> {
    try {
      const d = await firstValueFrom(this.api.registroVendedorDePqr(pqrId, usuarioId));
      this.registros.update((r) => ({ ...r, [pqrId + usuarioId]: d }));
    } catch {
      // El interceptor ya mostró el error.
    }
  }

  protected abrir(p: PqrAdminDto): void {
    this.seleccionada.set(p);
    this.respuesta.set('');
    this.error.set(null);
    this.abierto.set(true);
  }

  /** Avisa a quien la radicó que ya se está revisando. */
  protected async enTramite(p: PqrAdminDto): Promise<void> {
    if (!p.id) return;
    try {
      await firstValueFrom(this.api.pqrEnTramite(p.id));
      this.avisos.exito('Marcada en trámite', 'Le avisamos a quien la radicó.');
      this.recurso.reload();
    } catch (e) {
      this.avisos.error(mensajeDe(e));
    }
  }

  protected async responder(): Promise<void> {
    const p = this.seleccionada();
    if (!p?.id) return;
    try {
      await firstValueFrom(this.api.responderPqr(p.id, this.respuesta().trim()));
      this.abierto.set(false);
      this.avisos.exito('Respuesta enviada');
      this.recurso.reload();
    } catch (e) {
      this.error.set(mensajeDe(e));
    }
  }
}
