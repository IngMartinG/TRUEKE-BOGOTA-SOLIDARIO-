import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { nombreMaterial, type FactorImpactoDto, type MaterialDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { Icono } from '../../shared/ui/icono';

/** Siguiente versión menor: 2.0.0 → 2.1.0 (se puede cambiar antes de guardar). */
const siguienteVersion = (v: string) => {
  const [a = 2, b = 0] = v.split('.').map(Number);
  return `${a}.${b + 1}.0`;
};

/**
 * Factores de CO₂ por material (solo SuperUsuario). Cambiar uno exige su fuente y una versión nueva de la metodología; los
 * intercambios ya contados conservan el factor con que se calcularon.
 */
@Component({
  imports: [FormsModule, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-xl font-bold">Factores de impacto</h2>
    <p class="mt-1 max-w-prose text-sm text-tenue">
      kg de CO₂ que se evitan por cada kg reutilizado. Al cambiar un factor, anota la fuente y sube la versión
      (documéntala en la metodología): lo ya contado no se recalcula. Versión vigente: <strong class="text-tinta">{{ version() }}</strong>.
    </p>

    @if (recurso.value(); as d) {
      <div class="mt-6 overflow-x-auto rounded-tarjeta border border-borde">
        <table class="w-full text-left text-sm">
          <caption class="sr-only">Factores por material</caption>
          <thead class="bg-superficie-2">
            <tr>
              <th scope="col" class="px-4 py-3 font-semibold">Material</th>
              <th scope="col" class="px-4 py-3 text-right font-semibold">kg CO₂ / kg</th>
              <th scope="col" class="px-4 py-3 font-semibold">Fuente</th>
              <th scope="col" class="px-4 py-3 font-semibold">Versión</th>
              <th scope="col" class="px-4 py-3"><span class="sr-only">Acciones</span></th>
            </tr>
          </thead>
          <tbody>
            @for (f of d.factores; track f.material) {
              @if (editando() === f.material) {
                <tr class="border-t border-borde bg-bosque-50/60 dark:bg-bosque-900/30">
                  <th scope="row" class="px-4 py-2 font-semibold">{{ nombre(f.material) }}</th>
                  <td class="px-4 py-2">
                    <label class="sr-only" for="factor-valor">Factor</label>
                    <input id="factor-valor" type="number" min="0" max="100" step="0.1" class="entrada w-24 py-1.5 text-right" [(ngModel)]="valor" />
                  </td>
                  <td class="px-4 py-2">
                    <label class="sr-only" for="factor-fuente">Fuente</label>
                    <input id="factor-fuente" type="text" maxlength="300" class="entrada py-1.5" [(ngModel)]="fuente" />
                  </td>
                  <td class="px-4 py-2">
                    <label class="sr-only" for="factor-version">Versión nueva</label>
                    <input id="factor-version" type="text" maxlength="20" class="entrada w-24 py-1.5" [(ngModel)]="versionNueva" pattern="\\d+\\.\\d+\\.\\d+" />
                  </td>
                  <td class="px-4 py-2">
                    <div class="flex justify-end gap-1">
                      <button type="button" class="btn btn-fantasma btn-sm" (click)="editando.set(null)">Cancelar</button>
                      <button type="button" class="btn btn-primario btn-sm" (click)="guardar(f)" [disabled]="!valido() || guardando()">Guardar</button>
                    </div>
                  </td>
                </tr>
              } @else {
                <tr class="border-t border-borde">
                  <th scope="row" class="px-4 py-2.5 font-normal">{{ nombre(f.material) }}</th>
                  <td class="px-4 py-2.5 text-right font-semibold tabular-nums">{{ f.kgCo2PorKg }}</td>
                  <td class="max-w-xs px-4 py-2.5 text-tenue">{{ f.fuente }}</td>
                  <td class="px-4 py-2.5 tabular-nums">{{ f.version }}</td>
                  <td class="px-4 py-2.5 text-right">
                    <button type="button" class="btn btn-fantasma btn-sm" (click)="editar(f)" [attr.aria-label]="'Cambiar el factor de ' + nombre(f.material)">
                      <app-icono nombre="editar" [tamano]="14" />Cambiar
                    </button>
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>
      </div>
    } @else if (recurso.isLoading()) {
      <div class="esqueleto mt-6 h-64 rounded-tarjeta" aria-hidden="true"></div>
    }
  `,
})
export default class FactoresImpacto {
  private readonly admin = inject(AdminApi);
  private readonly catalogo = inject(CatalogoApi);
  private readonly avisos = inject(AvisosService);
  protected readonly nombre = nombreMaterial;
  // Sin la caché de la sesión: tras guardar se recarga con los factores nuevos.
  protected readonly recurso = rxResource({ stream: () => this.catalogo.impactoFiltrado({}) });
  protected readonly version = computed(() => this.recurso.value()?.versionMetodologia ?? '');
  protected readonly editando = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly valor = signal<number | null>(null);
  protected readonly fuente = signal('');
  protected readonly versionNueva = signal('');
  protected readonly valido = computed(() => {
    const v = this.valor();
    return v !== null && v >= 0 && v <= 100 && this.fuente().trim().length >= 3 && /^\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(this.versionNueva().trim());
  });

  protected editar(f: FactorImpactoDto): void {
    this.valor.set(f.kgCo2PorKg ?? 0);
    this.fuente.set(f.fuente ?? '');
    this.versionNueva.set(siguienteVersion(this.version() || f.version || '2.0.0'));
    this.editando.set(f.material ?? null);
  }

  protected async guardar(f: FactorImpactoDto): Promise<void> {
    this.guardando.set(true);
    try {
      await firstValueFrom(this.admin.actualizarFactorImpacto(f.material as MaterialDto,
        { kgCo2PorKg: this.valor()!, fuente: this.fuente().trim(), version: this.versionNueva().trim() }));
      this.avisos.exito(`Factor de ${nombreMaterial(f.material).toLowerCase()} actualizado`);
      this.editando.set(null);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.guardando.set(false);
    }
  }
}
