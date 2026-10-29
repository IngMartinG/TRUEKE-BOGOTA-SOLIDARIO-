import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import type { FotoEnRevisionDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { AvisosService } from '../../core/avisos.service';
import { HacePipe, NumeroPipe } from '../../shared/pipes';
import { EstadoVacio } from '../../shared/ui/estado-vacio';
import { Icono } from '../../shared/ui/icono';
import { Modal } from '../../shared/ui/modal';

/** Motivos frecuentes: un clic y listo (la persona los lee tal cual). */
const MOTIVOS = [
  'No se ve una cara en la foto.',
  'Es un logo, un dibujo o una imagen de internet.',
  'La cara está muy lejos, tapada o de espaldas.',
] as const;

/**
 * Fotos de perfil que el detector dejó en duda. Se revisan de a una, en grande, y se decide con el teclado (A aprueba,
 * R rechaza, flechas para moverse): con volumen, decenas se resuelven en pocos minutos.
 */
@Component({
  imports: [FormsModule, Icono, Modal, EstadoVacio, HacePipe, NumeroPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown)': 'tecla($event)' },
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-xl font-bold">Fotos de perfil en revisión</h2>
        <p class="mt-1 text-sm text-tenue">Aprueba si se ve la cara de una persona real. Si no, recházala: tendrá {{ dias }} días para cambiarla.</p>
      </div>
      @if (recurso.value(); as r) {
        <p class="text-sm text-tenue" aria-live="polite">
          <strong class="text-tinta">{{ r.total | numero }}</strong> por revisar
          @if (r.sinRevisar) { · {{ r.sinRevisar | numero }} antiguas aún en revisión automática }
        </p>
      }
    </div>

    @if (actual(); as f) {
      <div class="mt-6 grid gap-6 md:grid-cols-[minmax(0,22rem)_1fr]">
        <figure class="tarjeta overflow-hidden">
          <img [src]="f.fotoUrl" [alt]="'Foto de perfil de ' + f.nombreCompleto" width="352" height="352"
            class="aspect-square w-full bg-superficie-2 object-contain" referrerpolicy="no-referrer" />
          <figcaption class="p-4">
            <p class="truncate font-semibold">{{ f.nombreCompleto }}</p>
            <p class="text-xs text-tenue">En revisión {{ f.desdeUtc | hace }}</p>
          </figcaption>
        </figure>

        <div class="flex flex-col gap-4">
          <div class="grid grid-cols-2 gap-2 sm:max-w-sm">
            <button type="button" class="btn btn-primario" (click)="aprobar(f)" [disabled]="ocupado()">
              <app-icono nombre="check" [tamano]="16" />Aprobar <kbd class="ml-1 rounded-md border border-current/30 px-1.5 py-0.5 font-sans text-xs font-semibold opacity-80">A</kbd>
            </button>
            <button type="button" class="btn btn-peligro" (click)="abrirRechazo()" [disabled]="ocupado()">
              Rechazar <kbd class="ml-1 rounded-md border border-current/30 px-1.5 py-0.5 font-sans text-xs font-semibold opacity-80">R</kbd>
            </button>
          </div>
          <p class="text-sm text-tenue">
            Foto {{ indice() + 1 }} de {{ items().length }}.
            Usa <kbd class="ml-1 rounded-md border border-current/30 px-1.5 py-0.5 font-sans text-xs font-semibold opacity-80">←</kbd> <kbd class="ml-1 rounded-md border border-current/30 px-1.5 py-0.5 font-sans text-xs font-semibold opacity-80">→</kbd> para moverte entre fotos.
          </p>

          @if (items().length > 1) {
            <ul class="flex flex-wrap gap-2" aria-label="Siguientes fotos">
              @for (s of items(); track s.usuarioId; let i = $index) {
                <li>
                  <button type="button" class="block size-14 overflow-hidden rounded-xl border-2 transition"
                    [class]="i === indice() ? 'border-bosque-500' : 'border-transparent opacity-70 hover:opacity-100'"
                    [attr.aria-label]="'Ver la foto de ' + s.nombreCompleto" [attr.aria-current]="i === indice() ? 'true' : null"
                    (click)="indice.set(i)">
                    <img [src]="s.fotoUrl" alt="" width="56" height="56" loading="lazy" class="size-full object-cover" referrerpolicy="no-referrer" />
                  </button>
                </li>
              }
            </ul>
          }
        </div>
      </div>
    } @else if (!recurso.isLoading()) {
      <app-estado-vacio icono="camara" titulo="No hay fotos por revisar" />
    }

    <app-modal [(abierto)]="rechazoAbierto" titulo="Rechazar foto" subtitulo="La persona leerá el motivo y tendrá un plazo para cambiarla.">
      <form id="form-rechazo-foto" (ngSubmit)="rechazar()" class="space-y-3">
        <fieldset class="space-y-2">
          <legend class="etiqueta mb-2">Motivo</legend>
          @for (m of motivos; track m) {
            <label class="flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition"
              [class]="motivo() === m ? 'border-bosque-500 bg-bosque-50 dark:bg-bosque-900/40' : 'border-borde hover:bg-superficie-2'">
              <input type="radio" name="motivo-foto" class="accent-bosque-600" [value]="m" [ngModel]="motivo()" (ngModelChange)="motivo.set($event)" />
              <span class="text-sm">{{ m }}</span>
            </label>
          }
        </fieldset>
        <div class="campo">
          <label for="motivo-foto-otro" class="etiqueta">O escribe otro motivo</label>
          <textarea id="motivo-foto-otro" name="otro" class="entrada" maxlength="300" [ngModel]="motivo()" (ngModelChange)="motivo.set($event)"></textarea>
        </div>
      </form>
      <div pie class="flex justify-end gap-2 border-t border-borde p-4">
        <button type="button" class="btn btn-fantasma" (click)="rechazoAbierto.set(false)">Cancelar</button>
        <button type="submit" form="form-rechazo-foto" class="btn btn-peligro" [disabled]="motivo().trim().length < 3 || ocupado()">Rechazar</button>
      </div>
    </app-modal>
  `,
})
export default class Fotos {
  private readonly api = inject(AdminApi);
  private readonly avisos = inject(AvisosService);
  protected readonly dias = 7;
  protected readonly motivos = MOTIVOS;
  protected readonly recurso = rxResource({ stream: () => this.api.fotosEnRevision() });
  /** Las ya resueltas salen de la lista al instante, sin esperar a recargar. */
  private readonly resueltas = signal<ReadonlySet<string>>(new Set());
  protected readonly items = computed(() =>
    (this.recurso.value()?.items ?? []).filter((f) => !this.resueltas().has(f.usuarioId ?? '')),
  );
  protected readonly indice = signal(0);
  protected readonly actual = computed<FotoEnRevisionDto | undefined>(() => {
    const lista = this.items();
    return lista[Math.min(this.indice(), lista.length - 1)];
  });
  protected readonly ocupado = signal(false);
  protected readonly rechazoAbierto = signal(false);
  protected readonly motivo = signal('');

  protected tecla(e: KeyboardEvent): void {
    if (this.rechazoAbierto() || this.ocupado() || e.ctrlKey || e.metaKey || e.altKey) return;
    // Escribiendo en un campo, las letras son texto y no atajos.
    if (e.target instanceof Element && e.target.closest('input, textarea, select, [contenteditable="true"]')) return;
    const f = this.actual();
    if (!f) return;
    switch (e.key.toLowerCase()) {
      case 'a':
        e.preventDefault();
        void this.aprobar(f);
        break;
      case 'r':
        e.preventDefault();
        this.abrirRechazo();
        break;
      case 'arrowright':
        this.indice.update((i) => Math.min(i + 1, this.items().length - 1));
        break;
      case 'arrowleft':
        this.indice.update((i) => Math.max(i - 1, 0));
        break;
    }
  }

  protected async aprobar(f: FotoEnRevisionDto): Promise<void> {
    await this.resolver(f, () => this.api.aprobarFoto(f.usuarioId!), 'Foto aprobada');
  }

  protected abrirRechazo(): void {
    this.motivo.set(MOTIVOS[0]);
    this.rechazoAbierto.set(true);
  }

  protected async rechazar(): Promise<void> {
    const f = this.actual();
    if (!f) return;
    const motivo = this.motivo().trim();
    this.rechazoAbierto.set(false);
    await this.resolver(f, () => this.api.rechazarFoto(f.usuarioId!, motivo), 'Foto rechazada');
  }

  private async resolver(f: FotoEnRevisionDto, accion: () => ReturnType<AdminApi['aprobarFoto']>, mensaje: string): Promise<void> {
    this.ocupado.set(true);
    try {
      await firstValueFrom(accion());
      this.resueltas.update((s) => new Set([...s, f.usuarioId ?? '']));
      this.avisos.exito(mensaje);
      if (!this.items().length) this.recurso.reload(); // trae las siguientes, si hay más de 100
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.ocupado.set(false);
    }
  }
}
