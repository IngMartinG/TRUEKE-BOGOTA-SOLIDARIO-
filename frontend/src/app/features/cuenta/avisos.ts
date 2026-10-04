import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import type { PreferenciasAvisosDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { AvisosService } from '../../core/avisos.service';
import { Icono } from '../../shared/ui/icono';

type Clave = keyof Required<PreferenciasAvisosDto>;

/** Qué avisos llegan por correo. Las notificaciones dentro de la app siempre se muestran. */
@Component({
  imports: [Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="text-2xl font-extrabold">Avisos por correo</h1>
    <p class="mt-1 text-tenue">
      Te escribimos solo cuando no tienes Trueke abierto. Dentro de la app siempre verás todas tus notificaciones.
    </p>

    <section class="tarjeta mt-6 divide-y divide-borde">
      @for (o of opciones; track o.clave) {
        <label class="flex cursor-pointer items-start gap-4 p-5">
          <span class="grid size-10 shrink-0 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900/40 dark:text-bosque-200">
            <app-icono [nombre]="o.icono" [tamano]="20" />
          </span>
          <span class="min-w-0 flex-1">
            <span class="block font-semibold">{{ o.titulo }}</span>
            <span class="block text-sm text-tenue">{{ o.descripcion }}</span>
          </span>
          <!-- Interruptor accesible: un checkbox con apariencia de switch -->
          <input type="checkbox" role="switch" class="peer sr-only" [checked]="valores()[o.clave]" [disabled]="guardando() || !cargado()"
            (change)="cambiar(o.clave, $any($event.target).checked)" [attr.aria-label]="o.titulo" />
          <span class="relative mt-1 h-6 w-11 shrink-0 rounded-full bg-borde transition peer-checked:bg-bosque-600 peer-focus-visible:ring-2 peer-focus-visible:ring-bosque-400 peer-disabled:opacity-60
            after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:shadow after:transition peer-checked:after:translate-x-5" aria-hidden="true"></span>
        </label>
      }
    </section>

    <section class="mt-6 rounded-2xl border border-borde bg-superficie-2/60 p-5 text-sm">
      <p class="flex items-center gap-2 font-semibold"><app-icono nombre="candado" [tamano]="16" />Siempre te escribiremos por</p>
      <ul class="mt-2 list-disc space-y-1 pl-6 text-tenue">
        <li>seguridad de tu cuenta (confirmar el correo, recuperar la contraseña);</li>
        <li>pagos y facturas;</li>
        <li>respuestas a tus PQR;</li>
        <li>decisiones de moderación sobre tu cuenta.</li>
      </ul>
    </section>
  `,
})
export default class Avisos {
  private readonly api = inject(CuentaApi);
  private readonly avisos = inject(AvisosService);
  private readonly recurso = rxResource({ stream: () => this.api.preferenciasAvisos() });
  protected readonly valores = signal<Required<PreferenciasAvisosDto>>({ intercambios: true, mensajes: true, planes: true, novedades: false });
  protected readonly cargado = signal(false);
  protected readonly guardando = signal(false);

  protected readonly opciones: { clave: Clave; titulo: string; descripcion: string; icono: string }[] = [
    { clave: 'intercambios', titulo: 'Solicitudes e intercambios', descripcion: 'Cuando alguien solicita tu publicación, te responden, se confirma una entrega o te califican.', icono: 'apreton' },
    { clave: 'mensajes', titulo: 'Mensajes nuevos', descripcion: 'Un aviso (sin el texto del mensaje) como mucho cada 2 horas por conversación.', icono: 'mensaje' },
    { clave: 'planes', titulo: 'Recordatorios de tu plan', descripcion: 'Tres días antes de que venza tu plan Premium o Empresa.', icono: 'corona' },
    { clave: 'novedades', titulo: 'Novedades y consejos', descripcion: 'Mejoras de la plataforma e ideas para darle una segunda vida a tus cosas. Solo con tu autorización.', icono: 'hoja' },
  ];

  constructor() {
    effect(() => {
      const v = this.recurso.value();
      untracked(() => {
        if (!v) return;
        this.valores.set({ intercambios: !!v.intercambios, mensajes: !!v.mensajes, planes: !!v.planes, novedades: !!v.novedades });
        this.cargado.set(true);
      });
    });
  }

  protected async cambiar(clave: Clave, valor: boolean): Promise<void> {
    const anterior = this.valores();
    this.valores.set({ ...anterior, [clave]: valor });
    this.guardando.set(true);
    try {
      await firstValueFrom(this.api.guardarPreferenciasAvisos(this.valores()));
      this.avisos.exito('Preferencias guardadas');
    } catch {
      this.valores.set(anterior); // el interceptor ya mostró el error
    } finally {
      this.guardando.set(false);
    }
  }
}
