import { ChangeDetectionStrategy, Component, computed, effect, forwardRef, inject, input, output, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { catchError, of } from 'rxjs';
import type { MunicipioDto } from '../../api/tipos';
import { UbicacionesApi } from '../../core/api/ubicaciones.api';

/**
 * Departamento → municipio de Colombia (DANE - DIVIPOLA). Valor del control: código del municipio (5 dígitos).
 * Con `filtro`, también acepta solo el departamento (2 dígitos) o vacío ("Toda Colombia").
 */
@Component({
  selector: 'app-selector-municipio',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SelectorMunicipio), multi: true }],
  template: `
    <div [class]="apilado() ? 'grid gap-3' : 'grid gap-3 sm:grid-cols-2'">
      <div class="campo">
        <label class="etiqueta" [for]="id() + '-dep'">Departamento</label>
        <select class="entrada" [id]="id() + '-dep'" [disabled]="deshabilitado()" (change)="elegirDepartamento($any($event.target).value)" (blur)="tocar()">
          @if (filtro()) {
            <option value="" [selected]="!departamento()">Toda Colombia</option>
          } @else {
            <option value="" disabled [selected]="!departamento()">Elige el departamento</option>
          }
          @for (d of departamentos(); track d.codigo) {
            <option [value]="d.codigo" [selected]="d.codigo === departamento()">{{ d.nombre }}</option>
          }
        </select>
      </div>
      <div class="campo">
        <label class="etiqueta" [for]="id() + '-mpio'">Municipio</label>
        <select class="entrada" [id]="id() + '-mpio'" [disabled]="deshabilitado() || !departamento() || municipios.isLoading()"
          (change)="elegirMunicipio($any($event.target).value)" (blur)="tocar()" [attr.aria-invalid]="invalido() || null">
          @if (filtro()) {
            <option value="" [selected]="!municipio()">Todo el departamento</option>
          } @else {
            <option value="" disabled [selected]="!municipio()">{{ municipios.isLoading() ? 'Cargando…' : 'Elige el municipio' }}</option>
          }
          @for (m of municipios.value() ?? []; track m.codigo) {
            <option [value]="m.codigo" [selected]="m.codigo === municipio()">{{ m.nombre }}</option>
          }
        </select>
      </div>
    </div>
  `,
})
export class SelectorMunicipio implements ControlValueAccessor {
  private readonly api = inject(UbicacionesApi);

  readonly id = input('ubicacion');
  /** Modo filtro: permite "Toda Colombia" y "Todo el departamento". */
  readonly filtro = input(false);
  readonly apilado = input(false);
  readonly invalido = input(false);
  /** Municipio elegido (con coordenadas para centrar mapas); null si no hay. */
  readonly cambio = output<MunicipioDto | null>();

  protected readonly departamentos = toSignal(this.api.departamentos$.pipe(catchError(() => of([]))), { initialValue: [] });
  protected readonly departamento = signal('');
  protected readonly municipio = signal('');
  protected readonly deshabilitado = signal(false);

  protected readonly municipios = rxResource({
    params: () => this.departamento() || undefined,
    stream: ({ params }) => this.api.municipios(params),
  });

  private readonly elegido = computed(() => (this.municipios.value() ?? []).find((m) => m.codigo === this.municipio()) ?? null);

  private alCambiar: (v: string) => void = () => {};
  private alTocar: () => void = () => {};

  constructor() {
    // Departamentos con un solo municipio (Bogotá D.C., San Andrés…): se elige automáticamente.
    effect(() => {
      const lista = this.municipios.value() ?? [];
      untracked(() => {
        const unico = lista.length === 1 ? lista[0]!.codigo : undefined;
        if (!this.filtro() && unico && this.municipio() !== unico) this.elegirMunicipio(unico);
      });
    });
    effect(() => {
      const m = this.elegido();
      untracked(() => this.cambio.emit(m));
    });
  }

  writeValue(valor: string | null | undefined): void {
    const v = (valor ?? '').trim();
    if (/^\d{5}$/.test(v)) {
      this.departamento.set(v.slice(0, 2));
      this.municipio.set(v);
    } else if (/^\d{2}$/.test(v)) {
      this.departamento.set(v);
      this.municipio.set('');
    } else {
      this.departamento.set('');
      this.municipio.set('');
    }
  }

  registerOnChange(fn: (v: string) => void): void {
    this.alCambiar = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.alTocar = fn;
  }

  setDisabledState(deshabilitado: boolean): void {
    this.deshabilitado.set(deshabilitado);
  }

  protected tocar(): void {
    this.alTocar();
  }

  protected elegirDepartamento(codigo: string): void {
    this.departamento.set(codigo);
    this.municipio.set('');
    this.emitir();
  }

  protected elegirMunicipio(codigo: string): void {
    this.municipio.set(codigo);
    this.emitir();
  }

  private emitir(): void {
    this.alCambiar(this.municipio() || (this.filtro() ? this.departamento() : ''));
  }
}
