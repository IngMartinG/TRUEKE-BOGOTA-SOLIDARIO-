import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { Icono } from '../../shared/ui/icono';

/** "Invita y ganan los dos": enlace personal para compartir y cuántas invitaciones lleva la persona. */
@Component({
  selector: 'app-invitar',
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="tarjeta p-5 sm:p-6" aria-labelledby="titulo-invitar">
      <div class="flex items-start gap-4">
        <span class="grid size-12 shrink-0 place-items-center rounded-2xl bg-bosque-100 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200">
          <app-icono nombre="usuarios" [tamano]="24" />
        </span>
        <div class="min-w-0 flex-1">
          <h3 id="titulo-invitar" class="font-display text-xl font-bold">Invita y ganan los dos</h3>
          <p class="mt-1 text-sm text-tenue">
            Cuando la persona que invitas complete su primer intercambio, cada una gana <strong class="text-tinta">{{ puntos() }} Eco-Puntos</strong>.
            No cuenta si ese primer intercambio es contigo. Puedes ganar por hasta {{ max() }} invitaciones cada {{ dias() }} días.
          </p>
        </div>
      </div>

      @if (invitacion.isLoading() && !invitacion.value()) {
        <div class="mt-4 space-y-2" aria-hidden="true"><div class="esqueleto h-10 w-full"></div><div class="esqueleto h-3 w-1/3"></div></div>
      } @else if (invitacion.error()) {
        <div class="mt-4 flex flex-wrap items-center gap-3 text-sm text-tenue">
          <p>No pudimos cargar tu enlace de invitación.</p>
          <button type="button" class="btn btn-secundario btn-sm" (click)="invitacion.reload()">Reintentar</button>
        </div>
      } @else if (enlace(); as url) {
        <div class="mt-4 flex flex-col gap-2 sm:flex-row">
          <label class="sr-only" for="enlace-invitacion">Tu enlace de invitación</label>
          <input id="enlace-invitacion" class="entrada min-w-0 flex-1 font-mono text-sm" readonly [value]="url" (focus)="seleccionar($event)" />
          <button type="button" class="btn btn-primario shrink-0" (click)="compartir()">
            <app-icono [nombre]="puedeCompartir ? 'enviar' : 'copiar'" [tamano]="16" />{{ puedeCompartir ? 'Compartir' : 'Copiar enlace' }}
          </button>
          @if (puedeCompartir) {
            <button type="button" class="btn btn-secundario shrink-0" (click)="copiar()"><app-icono nombre="copiar" [tamano]="16" />Copiar enlace</button>
          }
        </div>
        <p class="mt-2 min-h-5 text-sm font-semibold text-bosque-700 dark:text-bosque-300" aria-live="polite">{{ copiado() ? '¡Enlace copiado!' : '' }}</p>
        <p class="text-sm">Invitaciones logradas: <strong class="tabular-nums">{{ invitacion.value()?.logradas ?? 0 }}</strong></p>
        @if (alTope()) {
          <p class="mt-2 text-xs text-tenue">
            Llegaste al tope de {{ max() }} en {{ dias() }} días: tus invitados siguen ganando, tú vuelves a ganar cuando pase el ciclo.
          </p>
        }
      }
    </section>
  `,
})
export class Invitar {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly politica = toSignal(this.api.politica$.pipe(catchError(() => of(null))));

  protected readonly invitacion = rxResource({ stream: () => this.api.invitacion() });
  protected readonly copiado = signal(false);
  protected readonly puedeCompartir = typeof navigator !== 'undefined' && typeof navigator.share === 'function';

  protected readonly puntos = computed(() => this.invitacion.value()?.puntosPorInvitacion ?? this.politica()?.puntosInvitacion ?? 20);
  protected readonly max = computed(() => this.invitacion.value()?.maxPorCiclo ?? this.politica()?.maxInvitacionesRecompensadasPorCiclo ?? 10);
  protected readonly dias = computed(() => this.invitacion.value()?.diasCiclo ?? this.politica()?.diasCicloInvitaciones ?? 30);
  /** Contra el `<base href>` (no el origen): en GitHub Pages la app vive en una subcarpeta del dominio. */
  protected readonly enlace = computed(() => {
    const c = this.invitacion.value()?.codigo;
    return c ? new URL(`registro?invita=${encodeURIComponent(c)}`, document.baseURI).href : null;
  });
  protected readonly alTope = computed(() => {
    const i = this.invitacion.value();
    return !!i && (i.recompensadasEnCiclo ?? 0) >= (i.maxPorCiclo ?? Number.MAX_SAFE_INTEGER);
  });

  protected seleccionar(e: Event): void {
    (e.target as HTMLInputElement).select();
  }

  /** Hoja de compartir del sistema; si no existe o falla (no por cancelar), se copia el enlace. */
  protected async compartir(): Promise<void> {
    const url = this.enlace();
    if (!url) return;
    if (this.puedeCompartir) {
      try {
        await navigator.share({ title: 'Trueke Bogotá Solidario', text: 'Te invito a Trueke Bogotá Solidario: intercambia, compra o dona y gana Eco-Puntos.', url });
        return;
      } catch (e) {
        if (e instanceof DOMException && e.name === 'AbortError') return;
      }
    }
    await this.copiar();
  }

  protected async copiar(): Promise<void> {
    const url = this.enlace();
    if (!url) return;
    try {
      await navigator.clipboard.writeText(url);
      this.copiado.set(true);
      this.avisos.exito('¡Enlace copiado!', 'Pégalo en un chat o una red social.');
      setTimeout(() => this.copiado.set(false), 4000);
    } catch {
      this.avisos.info('No pudimos copiarlo solo', 'Selecciona el enlace y cópialo manualmente.');
    }
  }
}
