import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeEsCo from '@angular/common/locales/es-CO';
import {
  ApplicationConfig,
  inject,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  provideRouter,
  TitleStrategy,
  withComponentInputBinding,
  withInMemoryScrolling,
  withViewTransitions,
} from '@angular/router';
import { routes } from './app.routes';
import { ConfigService } from './core/config.service';
import { authInterceptor } from './core/http/auth.interceptor';
import { erroresInterceptor } from './core/http/errores.interceptor';
import { SesionService } from './core/sesion.service';
import { TituloEstrategia } from './core/titulo.estrategia';

registerLocaleData(localeEsCo);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: LOCALE_ID, useValue: 'es-CO' },
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top', anchorScrolling: 'enabled' }),
      withViewTransitions({ skipInitialTransition: true }),
    ),
    { provide: TitleStrategy, useClass: TituloEstrategia },
    // El primero es el más externo: el de errores ve el resultado final tras el reintento por refresco.
    provideHttpClient(withInterceptors([erroresInterceptor, authInterceptor])),
    provideAppInitializer(async () => {
      const config = inject(ConfigService);
      const sesion = inject(SesionService);
      await Promise.all([config.cargar(), sesion.restaurar()]);
    }),
  ],
};
