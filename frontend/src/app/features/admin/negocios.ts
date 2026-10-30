import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { NegocioPorVerificarDto } from '../../api/tipos';
import { NegociosApi } from '../../core/api/negocios.api';
import { AvisosService } from '../../core/avisos.service';
import { FechaPipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

/**
 * Sello "Negocio verificado": se compara el NIT y el nombre comercial con el registro mercantil (RUES). Si no coinciden, el
 * negocio recibe el motivo y vuelve a la fila cuando corrige el NIT o el nombre.
 */
@Component({
  imports: [FormsModule, RouterLink, Icono, Modal, EstadoVacio, FechaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-xl font-bold">Negocios por verificar</h2>
    <p class="mt-1 max-w-2xl text-sm text-tenue">
      Busca el NIT en el RUES: debe estar activo y la razón social debe corresponder al nombre comercial (o ser de la misma persona).
      Verificado, el negocio muestra el sello «Negocio verificado» en su vitrina y publicaciones.
    </p>

    <ul class="mt-6 grid grid-cols-1 gap-3 md:grid-cols-2">
      @for (n of recurso.value() ?? []; track n.usuarioId) {
        <li class="tarjeta flex flex-col gap-3 p-5">
          <div>
            <p class="font-display text-lg font-bold">{{ n.nombreComercial }}</p>
            <p class="text-sm"><span class="text-tenue">NIT&nbsp;</span><span class="font-semibold tabular-nums">{{ n.nit }}</span></p>
            <p class="mt-1 text-xs text-tenue">Cuenta de {{ n.nombreCuenta }} · plan hasta el {{ n.planVigenteHasta | fecha }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <a href="https://www.rues.org.co/" target="_blank" rel="noopener noreferrer" class="btn btn-secundario btn-sm">
              <app-icono nombre="buscar" [tamano]="14" />Buscar en el RUES<span class="sr-only"> (abre otra pestaña)</span>
            </a>
            <a [routerLink]="['/negocios', n.usuarioId]" class="btn btn-fantasma btn-sm">Ver vitrina</a>
          </div>
          <div class="mt-auto grid grid-cols-2 gap-2 border-t border-borde pt-3">
            <button type="button" class="btn btn-fantasma btn-sm text-tierra-600 dark:text-tierra-500" (click)="abrirRechazo(n)">No coincide</button>
            <button type="button" class="btn btn-primario btn-sm" (click)="verificar(n)" [disabled]="ocupado()"><app-icono nombre="check" [tamano]="14" />Verificar</button>
          </div>
        </li>
      }
    </ul>
    @if (!recurso.isLoading() && !recurso.value()?.length) {
      <app-estado-vacio icono="edificio" titulo="No hay negocios por verificar" />
    }

    <app-modal [(abierto)]="rechazoAbierto" titulo="El NIT no coincide" subtitulo="El negocio leerá el motivo y podrá corregir su NIT o su nombre.">
      <form id="form-rechazo-negocio" (ngSubmit)="rechazar()" class="campo">
        <label for="motivo-negocio" class="etiqueta">Motivo</label>
        <textarea id="motivo-negocio" name="motivo" class="entrada" maxlength="300" [(ngModel)]="motivo"
          placeholder="Ej: el NIT no aparece activo en el RUES"></textarea>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="rechazoAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-rechazo-negocio" class="btn btn-peligro" [disabled]="motivo().trim().length < 3 || ocupado()">Enviar</button>
      </div>
    </app-modal>
  `,
})
export default class NegociosAdmin {
  private readonly api = inject(NegociosApi);
  private readonly avisos = inject(AvisosService);
  protected readonly recurso = rxResource({ stream: () => this.api.porVerificar() });
  protected readonly ocupado = signal(false);
  protected readonly rechazoAbierto = signal(false);
  protected readonly motivo = signal('');
  private seleccionado: NegocioPorVerificarDto | null = null;

  protected async verificar(n: NegocioPorVerificarDto): Promise<void> {
    await this.ejecutar(() => firstValueFrom(this.api.verificar(n.usuarioId!)), `${n.nombreComercial} ya tiene el sello`);
  }

  protected abrirRechazo(n: NegocioPorVerificarDto): void {
    this.seleccionado = n;
    this.motivo.set('');
    this.rechazoAbierto.set(true);
  }

  protected async rechazar(): Promise<void> {
    const n = this.seleccionado;
    if (!n?.usuarioId) return;
    this.rechazoAbierto.set(false);
    await this.ejecutar(() => firstValueFrom(this.api.rechazar(n.usuarioId!, this.motivo().trim())), 'Le avisamos al negocio');
  }

  private async ejecutar(accion: () => Promise<void>, mensaje: string): Promise<void> {
    this.ocupado.set(true);
    try {
      await accion();
      this.avisos.exito(mensaje);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.ocupado.set(false);
    }
  }
}
