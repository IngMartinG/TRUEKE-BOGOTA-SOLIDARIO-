import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, of } from 'rxjs';
import type { EmpresaAdminDto, EstadoEmpresaDto, InformeImpactoDto } from '../../api/tipos';
import { EmpresasApi } from '../../core/api/empresas.api';
import { AvisosService } from '../../core/avisos.service';
import { mensajeDe } from '../../core/http/problema';
import { SesionService } from '../../core/sesion.service';
import { descargar } from '../../shared/descargar';
import { dia, estadoCampana, hoyEnColombia } from '../../shared/dias';
import { FechaPipe, HacePipe, KilosPipe, NumeroPipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

type Decision = 'rechazar' | 'retirar';

/**
 * Empresas: el SuperUsuario las verifica con el certificado de la Cámara de Comercio (que se borra al decidir); cualquier
 * Administrador emite sus informes de impacto, que guardan una foto inmutable de los registros del periodo.
 */
@Component({
  imports: [FormsModule, Icono, Modal, EstadoVacio, HacePipe, FechaPipe, KilosPipe, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h2 class="text-xl font-bold">Empresas</h2>
        <p class="mt-1 max-w-2xl text-sm text-tenue">
          @if (sesion.esSuperUsuario()) {
            Compara la razón social y el NIT con el certificado: que sea una persona jurídica activa y que el certificado tenga menos
            de 30 días. El certificado se borra al decidir.
          } @else {
            Emite los informes de impacto de las empresas verificadas. La verificación la hace el SuperUsuario.
          }
        </p>
      </div>
      <div class="pestanas max-w-full overflow-x-auto" role="group" aria-label="Mostrar">
        @for (e of estados; track e.valor) {
          <button type="button" class="pestana" [class.pestana-activa]="estado() === e.valor"
            [attr.aria-pressed]="estado() === e.valor" (click)="estado.set(e.valor); abierta.set(null)">{{ e.texto }}</button>
        }
      </div>
    </div>

    <ul class="mt-6 space-y-3">
      @for (e of recurso.value() ?? []; track e.id) {
        <li class="tarjeta p-5">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0">
              <p class="font-display text-lg font-bold">{{ e.razonSocial }}</p>
              <p class="text-sm"><span class="text-tenue">NIT&nbsp;</span><span class="font-semibold tabular-nums">{{ e.nit }}</span></p>
              <p class="mt-1 text-xs text-tenue">
                Registrada por {{ e.solicitante }}@if (e.correoSolicitante) { ({{ e.correoSolicitante }}) } ·
                {{ e.miembros }} {{ e.miembros === 1 ? 'miembro' : 'miembros' }} ·
                {{ e.estado === 'Pendiente' ? 'enviada ' + (e.fechaSolicitudUtc | hace) : 'decidida el ' + (e.fechaDecisionUtc | fecha) }}
              </p>
              @if (e.motivo) {
                <p class="mt-2 text-sm"><span class="font-semibold">Motivo:</span> {{ e.motivo }}</p>
              }
            </div>
            <div class="flex flex-wrap gap-2">
              @if (e.certificadoUrl) {
                <a [href]="e.certificadoUrl" target="_blank" rel="noopener noreferrer" referrerpolicy="no-referrer" class="btn btn-secundario btn-sm">
                  <app-icono nombre="externo" [tamano]="14" />Ver certificado<span class="sr-only"> (abre otra pestaña; el enlace vence en 5 minutos)</span>
                </a>
              }
              @if (e.estado === 'Pendiente' && sesion.esSuperUsuario()) {
                <a href="https://www.rues.org.co/" target="_blank" rel="noopener noreferrer" class="btn btn-fantasma btn-sm">
                  <app-icono nombre="buscar" [tamano]="14" />Buscar en el RUES<span class="sr-only"> (abre otra pestaña)</span>
                </a>
              }
              @if (e.estado === 'Aprobada') {
                <button type="button" class="btn btn-sm" [class]="abierta() === e.id ? 'btn-secundario' : 'btn-primario'"
                  [attr.aria-expanded]="abierta() === e.id" [attr.aria-controls]="'informes-' + e.id" (click)="alternar(e)">
                  <app-icono nombre="grafica" [tamano]="14" />{{ abierta() === e.id ? 'Cerrar' : 'Campañas e informes' }}
                </button>
              }
            </div>
          </div>

          @if (e.estado === 'Pendiente' && sesion.esSuperUsuario()) {
            <div class="mt-4 grid grid-cols-2 gap-2 border-t border-borde pt-3 sm:max-w-sm">
              <button type="button" class="btn btn-fantasma btn-sm text-tierra-600 dark:text-tierra-500" (click)="abrir(e, 'rechazar')">Rechazar</button>
              <button type="button" class="btn btn-primario btn-sm" (click)="aprobar(e)"><app-icono nombre="check" [tamano]="14" />Aprobar</button>
            </div>
          }

          @if (abierta() === e.id) {
            <div [id]="'informes-' + e.id" class="mt-4 space-y-6 border-t border-borde pt-4">
              @if (detalle.isLoading()) {
                <div class="esqueleto h-24 w-full rounded-tarjeta"></div>
              } @else if (detalle.value(); as d) {
                <section aria-label="Campañas">
                  <h3 class="font-bold">Campañas</h3>
                  <ul class="mt-2 divide-y divide-borde text-sm">
                    @for (c of d.campanas ?? []; track c.id) {
                      <li class="flex flex-wrap items-center gap-x-4 gap-y-1 py-2">
                        <span class="font-semibold">{{ c.nombre }}</span>
                        <span [class]="estadoCampana(c).clase">{{ estadoCampana(c).texto }}</span>
                        <span class="text-tenue">{{ dia(c.inicio) }} – {{ dia(c.fin) }}</span>
                        <span class="ml-auto tabular-nums text-tenue">{{ c.objetos | numero }} entregados · {{ c.kg | kilos }} · ≈ {{ c.kgCo2 | kilos }} CO₂e</span>
                      </li>
                    } @empty {
                      <li class="py-2 text-tenue">La empresa aún no tiene campañas.</li>
                    }
                  </ul>
                </section>

                <form class="rounded-2xl bg-superficie-2/60 p-4" (ngSubmit)="emitir(e)" [attr.aria-labelledby]="'titulo-emitir-' + e.id">
                  <h3 [id]="'titulo-emitir-' + e.id" class="font-bold">Emitir un informe</h3>
                  <p class="mt-1 text-xs text-tenue">
                    Toma las entregas con doble confirmación del periodo y guarda sus datos tal como están hoy. Se avisa a los miembros.
                  </p>
                  <div class="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-[1fr_10rem_10rem_auto] sm:items-end">
                    <div class="campo">
                      <label [for]="'inf-camp-' + e.id" class="etiqueta">Campaña</label>
                      <select [id]="'inf-camp-' + e.id" name="campana" class="entrada" [(ngModel)]="campanaInforme">
                        <option [ngValue]="null">Todas las campañas</option>
                        @for (c of d.campanas ?? []; track c.id) {
                          <option [ngValue]="c.id">{{ c.nombre }}</option>
                        }
                      </select>
                    </div>
                    <div class="campo">
                      <label [for]="'inf-desde-' + e.id" class="etiqueta">Desde</label>
                      <input [id]="'inf-desde-' + e.id" name="desde" type="date" class="entrada" required [max]="hoy" [(ngModel)]="desde" />
                    </div>
                    <div class="campo">
                      <label [for]="'inf-hasta-' + e.id" class="etiqueta">Hasta</label>
                      <input [id]="'inf-hasta-' + e.id" name="hasta" type="date" class="entrada" required [min]="desde()" [max]="hoy" [(ngModel)]="hasta" />
                    </div>
                    <button type="submit" class="btn btn-primario" [disabled]="emitiendo() || !desde() || !hasta()">
                      {{ emitiendo() ? 'Emitiendo…' : 'Emitir informe' }}
                    </button>
                  </div>
                  @if (errorInforme()) {
                    <p class="mt-2 text-sm text-tierra-700 dark:text-tierra-400" role="alert">{{ errorInforme() }}</p>
                  }
                </form>

                <section aria-label="Informes emitidos">
                  <h3 class="font-bold">Informes emitidos</h3>
                  <ul class="mt-2 divide-y divide-borde">
                    @for (i of d.informes ?? []; track i.id) {
                      <li class="flex flex-wrap items-center gap-2 py-2 text-sm">
                        <div class="min-w-0 flex-1">
                          <p class="font-semibold">{{ dia(i.desde) }} – {{ dia(i.hasta) }} · {{ i.campana ?? 'Todas las campañas' }}</p>
                          <p class="text-xs text-tenue">
                            Emitido el {{ i.emitidoUtc | fecha: true }} · metodología {{ i.versiones }} ·
                            huella <span class="font-mono" [title]="i.huella ?? ''">{{ i.huella?.slice(0, 12) }}…</span>
                          </p>
                        </div>
                        <button type="button" class="btn btn-secundario btn-sm" (click)="bajar(i, 'pdf')"><app-icono nombre="descargar" [tamano]="14" />PDF</button>
                        <button type="button" class="btn btn-fantasma btn-sm" (click)="bajar(i, 'csv')"><app-icono nombre="descargar" [tamano]="14" />CSV</button>
                      </li>
                    } @empty {
                      <li class="py-2 text-sm text-tenue">Aún no se han emitido informes.</li>
                    }
                  </ul>
                </section>

                @if (sesion.esSuperUsuario()) {
                  <div class="border-t border-borde pt-3">
                    <button type="button" class="btn btn-fantasma btn-sm text-tierra-600 dark:text-tierra-500" (click)="abrir(e, 'retirar')">
                      Retirar la verificación
                    </button>
                  </div>
                }
              }
            </div>
          }
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="edificio" [titulo]="estado() === 'Pendiente' ? 'No hay empresas por revisar' : 'No hay empresas en este estado'" />
    }

    <app-modal [(abierto)]="modalAbierto" [titulo]="decision() === 'rechazar' ? 'Rechazar el registro' : 'Retirar la verificación'"
      subtitulo="Explica el motivo: lo verán los Admin de la empresa.">
      <form id="form-empresa" (ngSubmit)="decidir()" class="campo">
        <label for="motivo-empresa" class="etiqueta">Motivo</label>
        <textarea id="motivo-empresa" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivo"
          [placeholder]="decision() === 'rechazar' ? 'Ej: el certificado tiene más de 30 días' : 'Ej: la sociedad fue liquidada'"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="modalAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-empresa" class="btn btn-peligro" [disabled]="motivo().trim().length < 3">
          {{ decision() === 'rechazar' ? 'Rechazar' : 'Retirar la verificación' }}
        </button>
      </div>
    </app-modal>
  `,
})
export default class EmpresasAdmin {
  private readonly api = inject(EmpresasApi);
  private readonly avisos = inject(AvisosService);
  protected readonly sesion = inject(SesionService);
  protected readonly dia = dia;
  protected readonly estadoCampana = estadoCampana;
  protected readonly hoy = hoyEnColombia();

  protected readonly estados: readonly { valor: EstadoEmpresaDto; texto: string }[] = this.sesion.esSuperUsuario()
    ? [{ valor: 'Pendiente', texto: 'Por revisar' }, { valor: 'Aprobada', texto: 'Verificadas' }, { valor: 'Rechazada', texto: 'Rechazadas' },
       { valor: 'Retirada', texto: 'Retiradas' }]
    : [{ valor: 'Aprobada', texto: 'Verificadas' }];
  protected readonly estado = signal<EstadoEmpresaDto>(this.estados[0]!.valor);
  protected readonly recurso = rxResource({ params: () => this.estado(), stream: ({ params }) => this.api.listarAdmin(params) });

  protected readonly abierta = signal<string | null>(null);
  protected readonly detalle = rxResource({
    params: () => this.abierta(),
    stream: ({ params }) => (params ? this.api.detalleAdmin(params) : of(null)),
  });
  protected readonly campanaInforme = signal<string | null>(null);
  protected readonly desde = signal(this.primerDiaDelMes());
  protected readonly hasta = signal(hoyEnColombia());
  protected readonly emitiendo = signal(false);
  protected readonly errorInforme = signal<string | null>(null);

  protected readonly modalAbierto = signal(false);
  protected readonly decision = signal<Decision>('rechazar');
  protected readonly motivo = signal('');
  private seleccionada: EmpresaAdminDto | null = null;

  private primerDiaDelMes(): string {
    return `${hoyEnColombia().slice(0, 8)}01`;
  }

  protected alternar(e: EmpresaAdminDto): void {
    this.abierta.set(this.abierta() === e.id ? null : (e.id ?? null));
    this.campanaInforme.set(null);
    this.errorInforme.set(null);
  }

  protected async aprobar(e: EmpresaAdminDto): Promise<void> {
    try {
      await firstValueFrom(this.api.aprobar(e.id!));
      this.avisos.exito('Empresa verificada', `${e.razonSocial} ya puede invitar a su equipo y crear campañas.`);
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }

  protected abrir(e: EmpresaAdminDto, decision: Decision): void {
    this.seleccionada = e;
    this.decision.set(decision);
    this.motivo.set('');
    this.modalAbierto.set(true);
  }

  protected async decidir(): Promise<void> {
    const e = this.seleccionada;
    if (!e?.id) return;
    const motivo = this.motivo().trim();
    try {
      await firstValueFrom(this.decision() === 'rechazar' ? this.api.rechazar(e.id, motivo) : this.api.retirar(e.id, motivo));
      this.avisos.exito(this.decision() === 'rechazar' ? 'Registro rechazado' : 'Verificación retirada');
      this.modalAbierto.set(false);
      this.abierta.set(null);
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }

  protected async emitir(e: EmpresaAdminDto): Promise<void> {
    if (!e.id) return;
    this.emitiendo.set(true);
    this.errorInforme.set(null);
    try {
      const i = await firstValueFrom(this.api.emitirInforme(e.id, { campanaId: this.campanaInforme(), desde: this.desde(), hasta: this.hasta() }));
      this.avisos.exito('Informe emitido', `Del ${dia(i.desde)} al ${dia(i.hasta)}. Ya lo pueden descargar los miembros.`);
      this.detalle.reload();
    } catch (err) {
      this.errorInforme.set(mensajeDe(err));
    } finally {
      this.emitiendo.set(false);
    }
  }

  protected async bajar(i: InformeImpactoDto, tipo: 'pdf' | 'csv'): Promise<void> {
    if (!i.id) return;
    try {
      const blob = await firstValueFrom(tipo === 'pdf' ? this.api.informePdfAdmin(i.id) : this.api.informeCsvAdmin(i.id));
      descargar(blob, `informe-impacto-${i.desde}-a-${i.hasta}.${tipo}`);
    } catch {
      // El interceptor ya mostró el error.
    }
  }
}
