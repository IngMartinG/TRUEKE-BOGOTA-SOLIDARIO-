import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { nombreMaterial, type FactorImpactoDto, type MaterialDto, type MetaComunitariaDto } from '../../api/tipos';
import { AdminApi } from '../../core/api/admin.api';
import { CatalogoApi } from '../../core/api/catalogo.api';
import { AvisosService } from '../../core/avisos.service';
import { KilosPipe } from '../../shared/pipes';
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
  imports: [FormsModule, Icono, KilosPipe],
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

      <h2 class="mt-10 text-xl font-bold">Metas comunitarias</h2>
      <p class="mt-1 max-w-prose text-sm text-tenue">
        Kilos que una localidad quiere reutilizar cada mes; se muestran con su avance en la página de impacto. Solo se pueden
        poner en localidades con al menos 3 intercambios, y el avance del mes se oculta mientras tenga menos de 3.
      </p>
      <form class="mt-4 flex flex-wrap items-end gap-3" (submit)="$event.preventDefault(); guardarMeta()">
        <div>
          <label class="etiqueta" for="meta-localidad">Localidad</label>
          <select id="meta-localidad" class="entrada" [(ngModel)]="metaLocalidad" name="localidad">
            <option value="">Elige una</option>
            @for (l of d.localidadesDisponibles ?? []; track $index) {
              <option [value]="l.municipioCodigo + '|' + l.localidad">{{ l.localidad }} · {{ l.municipio }}</option>
            }
          </select>
        </div>
        <div>
          <label class="etiqueta" for="meta-kg">Meta (kg al mes)</label>
          <input id="meta-kg" type="number" min="10" max="100000" step="10" class="entrada w-36" [(ngModel)]="metaKg" name="kg" />
        </div>
        <button type="submit" class="btn btn-primario" [disabled]="!metaValida() || guardando()">Guardar meta</button>
      </form>
      @if (d.metas?.length) {
        <ul class="mt-5 space-y-2">
          @for (m of d.metas; track m.id) {
            <li class="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-borde px-4 py-2.5 text-sm">
              <span><strong>{{ m.localidad }}</strong> <span class="text-tenue">· {{ m.municipio }}</span>: {{ m.metaKg | kilos }}
                <span class="text-tenue">· {{ m.reservado ? 'avance reservado (menos de 3 intercambios este mes)' : (m.kgEsteMes | kilos) + ' (' + m.porcentaje + ' %)' }}</span></span>
              <button type="button" class="btn btn-fantasma btn-sm" (click)="quitarMeta(m)" [disabled]="guardando()" [attr.aria-label]="'Quitar la meta de ' + m.localidad">
                <app-icono nombre="basura" [tamano]="14" />Quitar
              </button>
            </li>
          }
        </ul>
      }
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

  protected readonly metaLocalidad = signal('');
  protected readonly metaKg = signal<number | null>(1000);
  protected readonly metaValida = computed(() => {
    const kg = this.metaKg();
    return !!this.metaLocalidad() && kg !== null && kg >= 10 && kg <= 100_000;
  });

  protected async guardarMeta(): Promise<void> {
    if (!this.metaValida()) return;
    const [municipioCodigo, ...resto] = this.metaLocalidad().split('|');
    this.guardando.set(true);
    try {
      const m = await firstValueFrom(this.admin.guardarMetaComunitaria({ municipioCodigo, localidad: resto.join('|'), metaKg: this.metaKg()! }));
      this.avisos.exito(`Meta de ${m.localidad} guardada`);
      this.metaLocalidad.set('');
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.guardando.set(false);
    }
  }

  protected async quitarMeta(m: MetaComunitariaDto): Promise<void> {
    this.guardando.set(true);
    try {
      await firstValueFrom(this.admin.quitarMetaComunitaria(m.id!));
      this.avisos.exito(`Meta de ${m.localidad} quitada`);
      this.recurso.reload();
    } catch {
      // El interceptor ya mostró el error.
    } finally {
      this.guardando.set(false);
    }
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
