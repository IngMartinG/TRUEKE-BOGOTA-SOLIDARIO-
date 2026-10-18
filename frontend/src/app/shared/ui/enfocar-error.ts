import { DestroyRef, Directive, ElementRef, inject, OnInit } from '@angular/core';
import { FormGroupDirective } from '@angular/forms';

/** Campos que pueden recibir el foco cuando su control no es válido (incluye radios y casillas). */
const CAMPO_CON_ERROR = 'input.ng-invalid:not([type="hidden"]):not([disabled]), select.ng-invalid:not([disabled]), textarea.ng-invalid:not([disabled])';

/**
 * Al enviar un formulario reactivo con errores, lleva el foco al primer campo inválido: quien usa teclado o lector de
 * pantalla llega directo al mensaje (que `CampoAccesible` asocia al campo) y en celular el campo queda a la vista.
 * Se aplica sola a todo `<form [formGroup]>` del componente que la importa.
 */
@Directive({
  // Igual que CampoAccesible: sin prefijo app-, para que ningún formulario dependa de acordarse de ponerla.
  // eslint-disable-next-line @angular-eslint/directive-selector
  selector: 'form[formGroup]',
})
export class EnfocarError implements OnInit {
  private readonly grupo = inject(FormGroupDirective, { self: true });
  private readonly el = inject<ElementRef<HTMLFormElement>>(ElementRef).nativeElement;
  private readonly destroy = inject(DestroyRef);

  ngOnInit(): void {
    const sub = this.grupo.ngSubmit.subscribe(() => {
      // Después del manejador del componente (que suele marcar todo como tocado) y de que se pinten los errores.
      requestAnimationFrame(() => {
        if (!this.grupo.invalid) return;
        const campo = this.el.querySelector<HTMLElement>(CAMPO_CON_ERROR);
        if (!campo || campo === document.activeElement) return;
        campo.focus({ preventScroll: true });
        campo.scrollIntoView({ block: 'center', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
      });
    });
    this.destroy.onDestroy(() => sub.unsubscribe());
  }
}
