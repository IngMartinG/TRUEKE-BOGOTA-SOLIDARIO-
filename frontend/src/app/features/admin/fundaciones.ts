import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { EstadoFundacionDto, FundacionAdminDto } from '../../api/tipos';
import { FundacionesApi } from '../../core/api/fundaciones.api';
import { AvisosService } from '../../core/avisos.service';
import { FechaPipe, HacePipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

type Decision = 'rechazar' | 'retirar';

/**
 * Revisión de fundaciones (solo SuperUsuario con verificación en dos pasos). Se compara la razón social y el NIT con el
 * certificado de la Cámara de Comercio (y, si hace falta, con el RUES). El certificado se borra al aprobar o rechazar.
 */
@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, EstadoVacio, HacePipe, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h2 class="text-xl font-bold">Fundaciones</h2>
        <p class="mt-1 max-w-2xl text-sm text-tenue">
          Compara la razón social y el NIT con el certificado: que sea una entidad sin ánimo de lucro (fundación, asociación o
          corporación), que esté activa y que el certificado tenga menos de 30 días. El certificado se borra al decidir.
        </p>
      </div>
      <div class="pestanas" role="group" aria-label="Mostrar">
        @for (e of estados; track e.valor) {
          <button type="button" class="pestana" [class.pestana-activa]="estado() === e.valor"
            [attr.aria-pressed]="estado() === e.valor" (click)="estado.set(e.valor)">{{ e.texto }}</button>
        }
      </div>
    </div>

    <ul class="mt-6 grid grid-cols-1 gap-3 md:grid-cols-2">
      @for (f of recurso.value() ?? []; track f.usuarioId) {
        <li class="tarjeta flex flex-col gap-3 p-5">
          <div>
            <p class="font-display text-lg font-bold">{{ f.razonSocial }}</p>
            <p class="text-sm"><span class="text-tenue">NIT&nbsp;</span><span class="font-semibold tabular-nums">{{ f.nit }}</span></p>
            <p class="mt-1 text-xs text-tenue">
              Cuenta de {{ f.nombreCuenta }} ({{ f.correo }}) ·
              {{ f.estado === 'Pendiente' ? 'enviada ' + (f.fechaSolicitudUtc | hace) : 'decidida el ' + (f.fechaDecisionUtc | fecha) }}
            </p>
          </div>
          <p class="line-clamp-4 text-sm text-tinta/85">{{ f.descripcion }}</p>
          @if (f.motivo) {
            <p class="text-sm"><span class="font-semibold">Motivo:</span> {{ f.motivo }}</p>
          }
          <div class="mt-auto flex flex-wrap gap-2">
            @if (f.certificadoUrl) {
              <a [href]="f.certificadoUrl" target="_blank" rel="noopener noreferrer" referrerpolicy="no-referrer" class="btn btn-secundario btn-sm">
                <app-icono nombre="externo" [tamano]="14" />Ver certificado<span class="sr-only"> (abre otra pestaña; el enlace vence en 5 minutos)</span>
              </a>
            }
            <a href="https://www.rues.org.co/" target="_blank" rel="noopener noreferrer" class="btn btn-fantasma btn-sm">
              <app-icono nombre="buscar" [tamano]="14" />Buscar en el RUES<span class="sr-only"> (abre otra pestaña)</span>
            </a>
            @if (f.estado === 'Aprobada') {
              <a [routerLink]="['/fundaciones', f.usuarioId]" class="btn btn-fantasma btn-sm">Ver página</a>
            }
          </div>
          @if (f.estado === 'Pendiente') {
            <div class="grid grid-cols-2 gap-2 border-t border-borde pt-3">
              <button type="button" class="btn btn-fantasma btn-sm text-tierra-600 dark:text-tierra-500" (click)="abrir(f, 'rechazar')">Rechazar</button>
              <button type="button" class="btn btn-primario btn-sm" (click)="aprobar(f)"><app-icono nombre="check" [tamano]="14" />Aprobar</button>
            </div>
          } @else if (f.estado === 'Aprobada') {
            <button type="button" class="btn btn-fantasma btn-sm self-start text-tierra-600 dark:text-tierra-500" (click)="abrir(f, 'retirar')">
              Retirar la insignia
            </button>
          }
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="heart" [titulo]="estado() === 'Pendiente' ? 'No hay fundaciones por revisar' : 'No hay fundaciones en este estado'" />
    }

    <app-modal [(abierto)]="modalAbierto" [titulo]="decision() === 'rechazar' ? 'Rechazar la solicitud' : 'Retirar la insignia'"
      subtitulo="Explica el motivo: lo verá la cuenta.">
      <form id="form-fundacion" (ngSubmit)="decidir()" class="campo">
        <label for="motivo-fundacion" class="etiqueta">Motivo</label>
        <textarea id="motivo-fundacion" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivo"
          [placeholder]="decision() === 'rechazar' ? 'Ej: el certificado tiene más de 30 días' : 'Ej: la entidad fue liquidada'"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="modalAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-fundacion" class="btn btn-peligro" [disabled]="motivo().trim().length < 3">
          {{ decision() === 'rechazar' ? 'Rechazar' : 'Retirar la insignia' }}
        </button>
      </div>
    </app-modal>
  `,
})
export default class FundacionesAdmin {
  private readonly api = inject(FundacionesApi);
  private readonly avisos = inject(AvisosService);
  protected readonly estados: readonly { valor: EstadoFundacionDto; texto: string }[] = [
    { valor: 'Pendiente', texto: 'Por revisar' },
    { valor: 'Aprobada', texto: 'Verificadas' },
    { valor: 'Rechazada', texto: 'Rechazadas' },
  ];
  protected readonly estado = signal<EstadoFundacionDto>('Pendiente');
  protected readonly recurso = rxResource({ params: () => this.estado(), stream: ({ params }) => this.api.revision(params) });
  protected readonly modalAbierto = signal(false);
  protected readonly decision = signal<Decision>('rechazar');
  protected readonly motivo = signal('');
  private seleccionada: FundacionAdminDto | null = null;

  protected async aprobar(f: FundacionAdminDto): Promise<void> {
    try {
      await firstValueFrom(this.api.aprobar(f.usuarioId!));
      this.avisos.exito('Fundación verificada', `${f.razonSocial} ya muestra la insignia.`);
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }

  protected abrir(f: FundacionAdminDto, decision: Decision): void {
    this.seleccionada = f;
    this.decision.set(decision);
    this.motivo.set('');
    this.modalAbierto.set(true);
  }

  protected async decidir(): Promise<void> {
    const f = this.seleccionada;
    if (!f?.usuarioId) return;
    const motivo = this.motivo().trim();
    try {
      if (this.decision() === 'rechazar') {
        await firstValueFrom(this.api.rechazar(f.usuarioId, motivo));
        this.avisos.exito('Solicitud rechazada');
      } else {
        await firstValueFrom(this.api.retirar(f.usuarioId, motivo));
        this.avisos.exito('Insignia retirada');
      }
      this.modalAbierto.set(false);
    } catch {
      // El interceptor ya mostró el error.
    }
    this.recurso.reload();
  }
}
