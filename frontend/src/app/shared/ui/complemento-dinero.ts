import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Icono } from './icono';

/** Texto fijo del complemento: se muestra siempre que se pueda sumar dinero (propuesta y contraoferta). */
export const AVISO_COMPLEMENTO = 'Se paga entre ustedes; Trueke no recibe ni guarda este dinero.';

/**
 * Dinero que se suma a los objetos de un trueke para completar el valor (opcional). Quien lo propone indica el monto y
 * quién le paga a quién; la plataforma solo lo anota, no lo mueve. Se muestra solo cuando ya hay objetos elegidos.
 */
@Component({
  selector: 'app-complemento-dinero',
  imports: [FormsModule, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="space-y-3 rounded-2xl border-2 border-borde p-3">
      <legend class="etiqueta px-1">Agregar dinero para completar <span class="font-normal text-tenue">(opcional)</span></legend>
      <div class="relative">
        <span class="pointer-events-none absolute inset-y-0 left-3 grid place-items-center text-tenue" aria-hidden="true">$</span>
        <input [id]="idBase() + '-monto'" [name]="idBase() + '-monto'" type="number" inputmode="numeric" min="1" max="1000000000" step="1"
          class="entrada pl-7" placeholder="Ej.: 50000" [ngModel]="monto()" (ngModelChange)="monto.set($event || null)"
          aria-label="Monto en pesos que se suma a los objetos" [attr.aria-describedby]="idBase() + '-aviso'" />
      </div>
      @if (monto() && monto()! > 0) {
        <div role="radiogroup" [attr.aria-label]="'Quién paga el dinero'" class="grid gap-2 sm:grid-cols-2">
          @for (o of opciones(); track o.valor) {
            <label class="flex cursor-pointer items-center gap-2 rounded-xl border-2 p-2.5 text-sm font-semibold transition has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-bosque-500"
              [class]="loPagoYo() === o.valor ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/50' : 'border-borde hover:border-bosque-200'">
              <input type="radio" class="sr-only" [name]="idBase() + '-quien'" [checked]="loPagoYo() === o.valor" (change)="loPagoYo.set(o.valor)" />
              <span class="grid size-5 shrink-0 place-items-center rounded-full border-2"
                [class]="loPagoYo() === o.valor ? 'border-bosque-600 bg-bosque-600 text-white' : 'border-borde'" aria-hidden="true">
                @if (loPagoYo() === o.valor) {
                  <app-icono nombre="check" [tamano]="11" [grosor]="3" />
                }
              </span>
              {{ o.texto }}
            </label>
          }
        </div>
      }
      <p [id]="idBase() + '-aviso'" class="ayuda flex items-start gap-1.5">
        <app-icono nombre="alerta" [tamano]="14" class="mt-0.5 shrink-0" />
        <span>{{ aviso }} Ideal: pagarlo en persona al entregar.</span>
      </p>
    </fieldset>
  `,
})
export class ComplementoDinero {
  /** Pesos enteros; null = sin dinero. */
  readonly monto = model<number | null>(null);
  /** true = lo pago yo; false = lo paga la otra persona. */
  readonly loPagoYo = model(true);
  readonly otraPersona = input('la otra persona');
  readonly idBase = input('complemento');
  protected readonly aviso = AVISO_COMPLEMENTO;
  protected readonly opciones = computed(() => [
    { valor: true, texto: 'Lo pago yo' },
    { valor: false, texto: `Lo paga ${this.otraPersona()}` },
  ]);
}

/** Cuerpo de la API: el sentido se guarda respecto de quien solicita (true = lo paga quien solicita). */
export function cuerpoComplemento(monto: number | null, loPagoYo: boolean, soyDuenio: boolean) {
  const m = monto && monto > 0 ? Math.round(monto) : null;
  return m ? { complementoCop: m, complementoLoPagaSolicitante: soyDuenio ? !loPagoYo : loPagoYo } : {};
}
