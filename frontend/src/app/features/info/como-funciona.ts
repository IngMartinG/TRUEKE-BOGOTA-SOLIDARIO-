import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { INFO_MODO, MODOS } from '../../api/tipos';
import { SesionService } from '../../core/sesion.service';
import { Icono } from '../../shared/ui/icono';

@Component({
  imports: [RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="bg-gradient-to-b from-bosque-50 to-fondo py-14 dark:from-bosque-950/60">
      <div class="contenedor max-w-3xl text-center">
        <p class="text-sm font-bold tracking-wide text-bosque-600 uppercase dark:text-bosque-400">Guía rápida</p>
        <h1 class="mt-2 text-4xl font-extrabold sm:text-5xl">Cómo funciona Trueke</h1>
        <p class="mt-4 text-lg text-tenue">
          Una comunidad donde los objetos circulan en lugar de terminar en la basura. Así participas paso a paso.
        </p>
      </div>
    </section>

    <section class="contenedor max-w-5xl">
      <ol class="relative space-y-10 border-l-2 border-dashed border-bosque-200 pl-8 sm:pl-12 dark:border-bosque-800">
        @for (p of pasos; track p.titulo; let i = $index) {
          <li class="relative">
            <span class="absolute top-0 -left-[3.05rem] grid size-11 place-items-center rounded-full bg-bosque-600 font-display text-lg font-extrabold text-white ring-8 ring-fondo sm:-left-[4.05rem]">{{ i + 1 }}</span>
            <div class="tarjeta grid grid-cols-1 gap-4 p-6 sm:grid-cols-[auto_1fr] sm:items-start">
              <span class="grid size-14 place-items-center rounded-2xl bg-bosque-50 text-bosque-700 dark:bg-bosque-900 dark:text-bosque-200">
                <app-icono [nombre]="p.icono" [tamano]="28" [grosor]="1.7" />
              </span>
              <div>
                <h2 class="text-xl font-bold">{{ p.titulo }}</h2>
                <p class="mt-2 text-tenue">{{ p.texto }}</p>
              </div>
            </div>
          </li>
        }
      </ol>
    </section>

    <section class="contenedor mt-20 max-w-5xl">
      <h2 class="titulo-seccion text-center">Tres maneras de participar</h2>
      <div class="mt-8 grid grid-cols-1 gap-4 md:grid-cols-3">
        @for (m of modos; track m) {
          <div class="tarjeta p-6">
            <span [class]="info[m].clase"><app-icono [nombre]="info[m].icono" [tamano]="12" />{{ info[m].etiqueta }}</span>
            <p class="mt-3 text-sm text-tenue">{{ info[m].descripcion }}</p>
            <p class="mt-4 text-sm font-semibold">Ganas <span class="text-sol-600 dark:text-sol-300">+{{ info[m].puntos }} Eco-Puntos</span> al completarlo.</p>
          </div>
        }
      </div>
    </section>

    <section class="contenedor mt-20 max-w-3xl">
      <h2 class="titulo-seccion text-center">Preguntas frecuentes</h2>
      <div class="mt-8 space-y-3">
        @for (f of preguntas; track f.p) {
          <details class="group tarjeta p-5 open:shadow-elevada">
            <summary class="flex cursor-pointer list-none items-center justify-between gap-4 font-semibold">
              {{ f.p }}
              <app-icono nombre="abajo" class="shrink-0 text-tenue transition group-open:rotate-180" />
            </summary>
            <p class="mt-3 text-sm text-tenue">{{ f.r }}</p>
          </details>
        }
      </div>
      <div class="mt-12 text-center">
        <a [routerLink]="sesion.autenticado() ? '/publicar' : '/registro'" class="btn btn-primario btn-lg">
          {{ sesion.autenticado() ? 'Publicar mi primer objeto' : 'Crear mi cuenta gratis' }}
        </a>
      </div>
    </section>
  `,
})
export default class ComoFunciona {
  protected readonly sesion = inject(SesionService);
  protected readonly info = INFO_MODO;
  protected readonly modos = MODOS;
  protected readonly pasos = [
    { icono: 'usuario', titulo: 'Crea tu cuenta', texto: 'Regístrate con tu correo o con Google y confirma tu correo. Recibes Eco-Puntos de bienvenida.' },
    { icono: 'camara', titulo: 'Publica lo que ya no usas', texto: 'Sube hasta 5 fotos, describe el estado del objeto, indica si es nuevo, usado o reparado, elige Trueke, Compra o Donación y marca tu ciudad y barrio. Tu ubicación exacta nunca se muestra en público.' },
    { icono: 'buscar', titulo: 'Explora y solicita', texto: 'Busca por categoría, ciudad, estado del producto o cerca de ti. Cuando algo te guste, envía una solicitud con un mensaje.' },
    { icono: 'mensaje', titulo: 'Acuerden por el chat', texto: 'Si el dueño acepta, se abre un chat privado para coordinar la entrega. No necesitas compartir tu teléfono ni tu correo.' },
    { icono: 'apreton', titulo: 'Confirmen la entrega', texto: 'Cuando el objeto cambie de manos, ambas personas confirman en la app. Si alguien olvida confirmar, el intercambio se cierra automáticamente tras unos días.' },
    { icono: 'estrella', titulo: 'Califica y gana', texto: 'Ambos reciben Eco-Puntos y reputación, y pueden calificarse. Así la comunidad sabe en quién confiar.' },
  ];
  protected readonly preguntas = [
    { p: '¿Cuesta algo usar Trueke?', r: 'No. Publicar, intercambiar, comprar y donar es gratis. Solo pagas si quieres servicios opcionales como destacar una publicación, verificar tu cuenta o un plan Premium.' },
    { p: '¿Qué son los Eco-Puntos?', r: 'Son la moneda interna de la comunidad. Los ganas al completar intercambios y sirven para obtener descuentos en los servicios opcionales. No se pueden convertir en dinero.' },
    { p: '¿Cómo protegen mis datos?', r: 'Nunca mostramos tu correo ni tu ubicación exacta a otras personas. Puedes descargar o eliminar tus datos cuando quieras desde Privacidad, conforme a la Ley 1581 de 2012.' },
    { p: '¿Qué pasa si el intercambio no se da?', r: 'Cualquiera de las dos partes puede marcarlo como "no se concretó" indicando el motivo. La publicación vuelve a estar disponible.' },
    { p: '¿Qué hago si veo algo sospechoso?', r: 'Usa el botón "Reportar" en la publicación, comentario o perfil. El equipo de moderación lo revisa y puede ocultar contenido o suspender cuentas.' },
  ];
}
