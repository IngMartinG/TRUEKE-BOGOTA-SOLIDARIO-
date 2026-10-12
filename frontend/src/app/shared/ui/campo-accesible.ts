import { computed, DestroyRef, Directive, ElementRef, inject, OnInit, signal } from '@angular/core';
import { NgControl } from '@angular/forms';

/**
 * Conecta un campo reactivo con su mensaje de error para lectores de pantalla: marca `aria-invalid` cuando el
 * control es inválido y ya se tocó, y suma a `aria-describedby` el id `<id del campo>-error` (el que usa
 * `<app-error-campo idError="...">`). Así ningún formulario depende de cablearlo a mano.
 */
@Directive({
  // Se aplica sola a todo campo .entrada reactivo (por eso no lleva prefijo app-): nadie tiene que acordarse de ponerla.
  // eslint-disable-next-line @angular-eslint/directive-selector
  selector: 'input.entrada[formControlName], select.entrada[formControlName], textarea.entrada[formControlName]',
  host: {
    '[attr.aria-invalid]': 'invalido() || null',
    '[attr.aria-describedby]': 'descritoPor()',
  },
})
export class CampoAccesible implements OnInit {
  private readonly control = inject(NgControl, { self: true });
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly destroy = inject(DestroyRef);
  private readonly cambio = signal(0);
  /** Lo que el campo ya declaraba (p. ej. su ayuda), sin el id del error: ese se agrega solo cuando hay error. */
  private readonly base = (this.el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter((t) => t && !t.endsWith('-error'));

  protected readonly invalido = computed(() => {
    this.cambio();
    const c = this.control.control;
    return !!c && c.invalid && c.touched;
  });

  protected readonly descritoPor = computed(() => {
    const ids = this.invalido() && this.el.id ? [...this.base, `${this.el.id}-error`] : this.base;
    return ids.length ? ids.join(' ') : null;
  });

  ngOnInit(): void {
    const sub = this.control.control?.events.subscribe(() => this.cambio.update((v) => v + 1));
    this.destroy.onDestroy(() => sub?.unsubscribe());
  }
}
