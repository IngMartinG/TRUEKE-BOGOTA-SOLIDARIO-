import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ConfigService } from '../../core/config.service';
import { Icono } from '../../shared/ui/icono';
import { Logo } from '../../shared/ui/logo';

/** Marco de las pantallas de autenticación: panel ilustrado (escritorio) + formulario. */
@Component({
  selector: 'app-marco-auth',
  imports: [Icono, Logo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor grid grid-cols-1 min-h-[calc(100dvh-4rem)] items-center gap-10 py-10 lg:grid-cols-2">
      <aside class="relative hidden h-full max-h-[44rem] overflow-hidden rounded-[2rem] bg-bosque-900 p-10 text-white lg:flex lg:flex-col" aria-hidden="true">
        <div class="absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,#269763_0%,transparent_45%),radial-gradient(circle_at_80%_90%,#0f8a86_0%,transparent_45%)] opacity-60"></div>
        <svg class="absolute -right-20 -bottom-20 size-[28rem] text-bosque-700/50 animate-flotar" viewBox="0 0 200 200">
          <path fill="currentColor" d="M100 10c50 15 78 52 72 102-38 4-70-12-88-46-7-15-8-35 16-56Z" />
        </svg>
        <app-logo [claro]="true" class="relative" />
        <div class="relative mt-auto">
          <h2 class="font-display text-4xl leading-tight font-extrabold text-white">{{ lema() }}</h2>
          <ul class="mt-8 space-y-4">
            @for (b of beneficios; track b.texto) {
              <li class="flex items-center gap-3">
                <span class="grid size-10 place-items-center rounded-xl bg-white/10 backdrop-blur"><app-icono [nombre]="b.icono" [tamano]="20" class="text-sol-300" /></span>
                <span class="text-bosque-100">{{ b.texto }}</span>
              </li>
            }
          </ul>
        </div>
      </aside>

      <section class="mx-auto w-full max-w-md animate-aparecer">
        <h1 class="text-3xl font-extrabold">{{ titulo() }}</h1>
        @if (subtitulo()) {
          <p class="mt-2 text-tenue">{{ subtitulo() }}</p>
        }
        <div class="mt-8"><ng-content /></div>
        @if (conCaptcha() && config.captchaClave()) {
          <!-- reCAPTCHA v3 es invisible; Google permite ocultar su insignia si se muestra este aviso -->
          <p class="mt-6 flex items-start gap-1.5 text-xs text-tenue">
            <app-icono nombre="escudo" [tamano]="14" class="mt-px shrink-0 text-bosque-600" />
            <span>
              Protegido por reCAPTCHA de Google. Se aplican la
              <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer" class="enlace">Política de privacidad</a> y los
              <a href="https://policies.google.com/terms" target="_blank" rel="noopener noreferrer" class="enlace">Términos del servicio</a> de Google.
            </span>
          </p>
        }
      </section>
    </div>
  `,
})
export class MarcoAuth {
  readonly titulo = input.required<string>();
  readonly subtitulo = input('');
  readonly lema = input('Lo que tú ya no usas, alguien cerca de ti lo necesita.');
  /** El formulario usa reCAPTCHA v3: se muestra el aviso que Google exige. */
  readonly conCaptcha = input(false);
  protected readonly config = inject(ConfigService);
  protected readonly beneficios = [
    { icono: 'repeat', texto: 'Intercambia sin dinero de por medio' },
    { icono: 'escudo', texto: 'Chat interno: sin compartir tu número' },
    { icono: 'moneda', texto: 'Gana Eco-Puntos por cada intercambio' },
  ];
}
