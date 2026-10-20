import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { iconoLogro, type LogroDto } from '../../api/tipos';
import { Icono } from './icono';

/**
 * Medallas de logros. Son solo un reconocimiento: no dan puntos ni beneficios.
 * - `progreso = false` (perfil público): solo las obtenidas, en una fila compacta.
 * - `progreso = true` (tus logros): todas; las pendientes en gris con lo que falta.
 * El grado (bronce, plata, oro) se nota en el color del aro y se dice en texto: el color nunca es la única señal.
 */
@Component({
  selector: 'app-medallas-logros',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (lista().length) {
      <ul class="grid gap-3" [class]="progreso() ? 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3' : 'grid-cols-1 sm:grid-cols-2'">
        @for (l of lista(); track l.clave) {
          <li class="flex items-start gap-3 rounded-2xl p-3" [class]="l.obtenido ? 'bg-superficie' : 'bg-superficie-2/60'">
            <span class="grid size-11 shrink-0 place-items-center rounded-full border-2" [class]="aro(l)" aria-hidden="true">
              <app-icono [nombre]="icono(l.clave)" [tamano]="20" />
            </span>
            <div class="min-w-0 flex-1">
              <p class="font-semibold leading-tight" [class.text-tenue]="!l.obtenido">
                {{ l.nombre }}{{ l.grado ? ' de ' + etiquetaGrado(l.grado) : '' }}
              </p>
              <p class="mt-0.5 text-sm text-tenue">{{ l.descripcion }}</p>
              @if (progreso() && pendiente(l)) {
                <div class="mt-2 flex items-center gap-2 text-xs text-tenue">
                  <span
                    class="h-1.5 flex-1 overflow-hidden rounded-full bg-superficie-2"
                    role="progressbar"
                    [attr.aria-valuenow]="l.progreso"
                    [attr.aria-valuemin]="0"
                    [attr.aria-valuemax]="l.meta"
                    [attr.aria-label]="'Avance en ' + l.nombre"
                  >
                    <span class="block h-full rounded-full bg-bosque-500" [style.width.%]="porcentaje(l)"></span>
                  </span>
                  <span class="shrink-0 tabular-nums">{{ textoAvance(l) }}</span>
                </div>
              }
            </div>
          </li>
        }
      </ul>
    }
  `,
})
export class MedallasLogros {
  readonly logros = input<LogroDto[] | null | undefined>();
  readonly progreso = input(false);

  protected readonly lista = computed(() => {
    const todos = this.logros() ?? [];
    if (!this.progreso()) return todos.filter((l) => l.obtenido);
    // Primero lo obtenido; después lo que está más cerca de lograrse
    return [...todos].sort((a, b) => Number(!!b.obtenido) - Number(!!a.obtenido) || this.porcentaje(b) - this.porcentaje(a));
  });

  protected readonly icono = iconoLogro;

  protected etiquetaGrado(g: string): string {
    return g === 'Oro' ? 'oro' : g === 'Plata' ? 'plata' : 'bronce';
  }

  protected aro(l: LogroDto): string {
    if (!l.obtenido) return 'border-dashed border-borde text-tenue';
    switch (l.grado) {
      case 'Oro':
        return 'border-sol-400 bg-sol-50 text-sol-600 dark:bg-sol-500/15 dark:text-sol-300';
      case 'Plata':
        return 'border-borde bg-superficie-2 text-tenue';
      case 'Bronce':
        return 'border-tierra-500 bg-tierra-50 text-tierra-700 dark:bg-tierra-700/25 dark:text-tierra-100';
      default:
        return 'border-bosque-500 bg-bosque-50 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200';
    }
  }

  /** Obtenido sin siguiente grado (o sin grados) = completo; lo demás aún muestra avance. */
  protected pendiente(l: LogroDto): boolean {
    return (l.progreso ?? 0) < (l.meta ?? 0);
  }

  protected porcentaje(l: LogroDto): number {
    const meta = l.meta ?? 0;
    return meta > 0 ? Math.min(100, Math.round((100 * (l.progreso ?? 0)) / meta)) : 100;
  }

  protected textoAvance(l: LogroDto): string {
    const base = `${l.progreso ?? 0} de ${l.meta ?? 0}`;
    if (l.clave === 'un-anio') return `${l.progreso ?? 0} de ${l.meta ?? 0} días`;
    return l.obtenido && l.grado ? `${base} para ${l.grado === 'Bronce' ? 'plata' : 'oro'}` : base;
  }
}
