import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { ConfiguracionPublicaDto } from '../api/tipos';
import { CuentaApi } from './api/cuenta.api';

/** Configuración pública que publica la API (`GET /configuracion`). Un valor null = función deshabilitada. */
@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly api = inject(CuentaApi);
  private readonly _config = signal<ConfiguracionPublicaDto>({});
  private readonly _disponible = signal(true);

  readonly config = this._config.asReadonly();
  /** false si la API no respondió al arrancar: la interfaz muestra un aviso. */
  readonly apiDisponible = this._disponible.asReadonly();
  readonly captchaClave = computed(() => this._config().captchaClaveSitio ?? null);
  readonly googleClientId = computed(() => this._config().googleClientId ?? null);
  readonly subidasHabilitadas = computed(() => this._config().subidaArchivosHabilitada === true);
  readonly maxImagenes = computed(() => this._config().maxImagenesPorPublicacion ?? 5);
  readonly tamanoMaximo = computed(() => this._config().tamanoMaximoArchivoBytes ?? 5 * 1024 * 1024);

  async cargar(): Promise<void> {
    try {
      this._config.set(await firstValueFrom(this.api.configuracion()));
      this._disponible.set(true);
    } catch {
      this._disponible.set(false);
    }
  }
}
