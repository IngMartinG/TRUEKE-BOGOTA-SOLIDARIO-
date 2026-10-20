import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { ConfiguracionPublicaDto } from '../api/tipos';
import { configurarCdn } from '../shared/imagenes';
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
  /** Pagos sin dinero real (sandbox de Wompi o pasarela simulada): la interfaz lo avisa. */
  readonly pagosDePrueba = computed(() => this._config().pagosDePrueba === true);
  /**
   * false = lanzamiento sin cobro: no se paga con dinero (planes, recargas). Solo se puede usar lo que cubren los
   * Eco-Puntos. Si la API no respondió se asume true: el backend decide igual.
   */
  readonly ventasHabilitadas = computed(() => this._config().ventasHabilitadas !== false);
  /** Enlace externo para aportes voluntarios ("Apoya el proyecto"); null = no se muestra. */
  readonly donacionUrl = computed(() => this._config().donacionUrl ?? null);
  /** Llave pública VAPID para las notificaciones push (null = apagadas en el servidor). */
  readonly pushClavePublica = computed(() => this._config().pushClavePublica ?? null);
  /** Versiones vigentes de la política y los términos, y datos del responsable del tratamiento (null si la API no respondió). */
  readonly legal = computed(() => this._config().legal ?? null);

  async cargar(): Promise<void> {
    try {
      const config = await firstValueFrom(this.api.configuracion());
      this._config.set(config);
      configurarCdn(config.imagenesOrigenUrl, config.imagenesCdnUrl);
      this._disponible.set(true);
    } catch {
      this._disponible.set(false);
    }
  }
}
