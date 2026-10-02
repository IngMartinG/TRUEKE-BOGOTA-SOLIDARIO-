import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CatalogoApi } from './api/catalogo.api';
import { AvisosService } from './avisos.service';
import { SesionService } from './sesion.service';

/**
 * Favoritos con actualización optimista: el corazón cambia al instante y se revierte si la API falla.
 * Guarda los cambios hechos en esta sesión para que todas las tarjetas muestren el mismo estado.
 */
@Injectable({ providedIn: 'root' })
export class FavoritosService {
  private readonly api = inject(CatalogoApi);
  private readonly sesion = inject(SesionService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly cambios = signal<Record<string, boolean>>({});

  esFavorita(id: string | undefined, valorServidor: boolean | undefined): boolean {
    if (!id) return false;
    return this.cambios()[id] ?? valorServidor ?? false;
  }

  async alternar(id: string, actual: boolean): Promise<void> {
    if (!this.sesion.autenticado()) {
      this.avisos.info('Ingresa para guardar favoritos');
      await this.router.navigate(['/ingresar'], { queryParams: { volver: this.router.url } });
      return;
    }
    const nuevo = !actual;
    this.cambios.update((c) => ({ ...c, [id]: nuevo }));
    try {
      await firstValueFrom(nuevo ? this.api.marcarFavorito(id) : this.api.quitarFavorito(id));
    } catch {
      this.cambios.update((c) => ({ ...c, [id]: actual }));
    }
  }
}
