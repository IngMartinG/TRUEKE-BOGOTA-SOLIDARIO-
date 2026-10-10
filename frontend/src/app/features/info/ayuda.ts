import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Icono } from '../../shared/ui/icono';
import { Volver } from '../../shared/ui/volver';

interface Pregunta {
  p: string;
  r: string;
}
interface Tema {
  titulo: string;
  icono: string;
  preguntas: Pregunta[];
}

/** Centro de ayuda: preguntas frecuentes con buscador y consejos para intercambiar con seguridad. */
@Component({
  imports: [FormsModule, RouterLink, Icono, Volver],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="contenedor max-w-3xl py-6 sm:py-10">
      <app-volver respaldo="/" class="mb-3" />
      <h1 class="text-3xl font-extrabold sm:text-4xl">Centro de ayuda</h1>
      <p class="mt-2 text-tenue">Respuestas rápidas sobre cómo funciona Trueke. ¿No encuentras lo que buscas? Escríbenos desde Soporte.</p>

      <div class="relative mt-6">
        <app-icono nombre="buscar" [tamano]="18" class="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-tenue" />
        <input type="search" class="entrada rounded-full pl-11" placeholder="Busca: puntos, factura, bloquear, entrega…" aria-label="Buscar en la ayuda"
          [ngModel]="busqueda()" (ngModelChange)="busqueda.set($event)" maxlength="60" />
      </div>

      <!-- Seguridad primero: es lo más importante en un intercambio entre desconocidos -->
      <section class="mt-8 rounded-2xl border border-sol-500/30 bg-sol-50 p-5 dark:bg-sol-600/10" aria-labelledby="titulo-seguridad">
        <h2 id="titulo-seguridad" class="flex items-center gap-2 font-bold"><app-icono nombre="escudo" [tamano]="20" />Para entregar con seguridad</h2>
        <ul class="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
          @for (c of consejos; track c) {
            <li class="flex gap-2"><app-icono nombre="check" [tamano]="16" class="mt-0.5 shrink-0 text-bosque-600" />{{ c }}</li>
          }
        </ul>
      </section>

      @for (t of temasFiltrados(); track t.titulo) {
        <section class="mt-8">
          <h2 class="flex items-center gap-2 text-lg font-bold"><app-icono [nombre]="t.icono" [tamano]="20" class="text-bosque-600" />{{ t.titulo }}</h2>
          <div class="tarjeta mt-3 divide-y divide-borde">
            @for (q of t.preguntas; track q.p) {
              <details class="group" [open]="!!busqueda().trim()">
                <summary class="flex cursor-pointer list-none items-center justify-between gap-3 p-4 font-semibold">
                  {{ q.p }}<app-icono nombre="abajo" [tamano]="18" class="shrink-0 text-tenue transition group-open:rotate-180" />
                </summary>
                <p class="px-4 pb-4 text-sm leading-relaxed text-tenue">{{ q.r }}</p>
              </details>
            }
          </div>
        </section>
      } @empty {
        <p class="mt-10 text-center text-tenue">No encontramos respuestas para "{{ busqueda() }}".</p>
      }

      <div class="tarjeta mt-10 flex flex-col items-center gap-3 p-6 text-center sm:flex-row sm:text-left">
        <app-icono nombre="soporte" [tamano]="28" class="text-agua-600" />
        <div class="flex-1">
          <p class="font-semibold">¿Sigues con dudas?</p>
          <p class="text-sm text-tenue">Radica una petición, queja o reclamo y te respondemos por escrito.</p>
        </div>
        <a routerLink="/cuenta/soporte" class="btn btn-primario">Ir a Soporte</a>
      </div>
    </div>
  `,
})
export default class Ayuda {
  protected readonly busqueda = signal('');

  protected readonly consejos = [
    'Coordina todo por el chat de Trueke: no compartas tu número ni tu correo.',
    'Entrega en un lugar público y concurrido, de día (centros comerciales, estaciones, parques).',
    'Revisa el objeto antes de confirmar la entrega.',
    'Nunca pagues por adelantado a alguien que no conoces.',
    'Si algo no te da confianza, cancela y repórtalo.',
    'Confirma la entrega en la app solo cuando de verdad la recibiste.',
  ];

  private readonly temas: Tema[] = [
    {
      titulo: 'Empezar',
      icono: 'brote',
      preguntas: [
        { p: '¿Qué puedo hacer en Trueke?', r: 'Intercambiar objetos sin dinero (trueke), venderlos o comprarlos de segunda mano, o donarlos a quien los necesita. Cada intercambio completado te da Eco-Puntos y reputación.' },
        { p: '¿Por qué debo confirmar mi correo?', r: 'Para publicar, solicitar, comentar, chatear y pagar necesitamos saber que hay una persona real detrás de la cuenta. Así evitamos cuentas falsas y estafas.' },
        { p: '¿Cómo publico algo?', r: 'Toca el botón "+" (Publicar), elige si es trueke, venta o donación, cuenta en qué estado está, sube fotos y marca la zona aproximada. Tus fotos se publican sin la ubicación GPS que guarda el celular.' },
      ],
    },
    {
      titulo: 'Intercambios y entregas',
      icono: 'apreton',
      preguntas: [
        { p: '¿Cómo funciona una solicitud?', r: 'Cuando te interesa una publicación envías una solicitud y se abre un chat con quien la publicó. Varias personas pueden estar interesadas a la vez: la publicación sigue en el catálogo hasta que el dueño elige a una. Ahí queda reservada y las demás pasan a lista de espera; si el intercambio no se concreta, vuelve al catálogo y el dueño puede elegir a otra persona.' },
        { p: '¿Cuándo se completa un intercambio?', r: 'Cuando las dos partes confirman la entrega en la app. En ese momento se suman los Eco-Puntos y la reputación, y pueden calificarse.' },
        { p: '¿Qué pasa si la otra persona no aparece?', r: 'Desde "Mis intercambios" puedes marcar el intercambio como no concretado, explicando el motivo. La publicación vuelve a estar disponible.' },
      ],
    },
    {
      titulo: 'Chat',
      icono: 'mensaje',
      preguntas: [
        { p: '¿Qué significan los chulitos?', r: 'Un ✓ es enviado, dos ✓✓ grises es que llegó al dispositivo de la otra persona, y ✓✓ de color es que ya lo leyó.' },
        { p: '¿Cómo respondo un mensaje en particular?', r: 'En el celular desliza el mensaje hacia la derecha; en el computador pasa el mouse sobre él y toca la flecha de responder.' },
        { p: '¿Cómo bloqueo a alguien?', r: 'Desde el chat o su perfil, toca "Bloquear". Ninguno de los dos podrá escribirse ni solicitar las publicaciones del otro, y no se le avisa. Puedes desbloquear en Cuenta → Privacidad y datos.' },
      ],
    },
    {
      titulo: 'Eco-Puntos, pagos y facturas',
      icono: 'moneda',
      preguntas: [
        { p: '¿Qué son los Eco-Puntos?', r: 'La moneda interna de la comunidad: los ganas al completar intercambios (más por donar) y sirven para impulsar publicaciones o pagar parte de los beneficios. No se convierten en dinero.' },
        { p: '¿Intercambiar o donar cuesta algo?', r: 'No. Usar la plataforma es gratis. Solo pagas si quieres un beneficio opcional: destacar una publicación, verificar tu cuenta o un plan Premium o Empresa.' },
        { p: '¿Por qué me llega una factura?', r: 'La ley colombiana obliga a facturar electrónicamente cada pago a la plataforma. Sin datos, la factura sale a "consumidor final"; si la necesitas a tu nombre o al de tu empresa, agrega tu cédula o NIT en Cuenta → Pagos y facturas.' },
        { p: '¿Puedo arrepentirme de una compra?', r: 'Sí: tienes derecho de retracto dentro de los 5 días hábiles siguientes. Solicítalo desde Soporte.' },
      ],
    },
    {
      titulo: 'Reportes y seguridad de tu cuenta',
      icono: 'bandera',
      preguntas: [
        { p: '¿Qué pasa cuando reporto algo?', r: 'Un moderador lo revisa. Si confirma el problema, puede ocultar el contenido y la otra persona recibe el aviso, sin saber quién la reportó.' },
        { p: 'Me reportaron y no estoy de acuerdo', r: 'En Cuenta → Reportes sobre ti puedes contar tu versión una vez, dentro de los 15 días siguientes. La revisa otra persona del equipo y, si te da la razón, se revierte la medida.' },
        { p: '¿Cómo protejo mi cuenta?', r: 'Usa una contraseña única. Si sospechas algo, cámbiala y usa "Cerrar todas las sesiones" en Cuenta → Seguridad.' },
        { p: '¿Cómo descargo o elimino mis datos?', r: 'En Cuenta → Privacidad y datos puedes descargar todo lo que tenemos sobre ti o eliminar tu cuenta (Ley 1581 de 2012).' },
      ],
    },
  ];

  /** Búsqueda sin tildes ni mayúsculas: "facturacion" encuentra "facturación". */
  private static normalizar(t: string): string {
    return t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
  }

  protected readonly temasFiltrados = computed(() => {
    const q = Ayuda.normalizar(this.busqueda().trim());
    if (!q) return this.temas;
    return this.temas
      .map((t) => ({ ...t, preguntas: t.preguntas.filter((x) => Ayuda.normalizar(`${x.p} ${x.r}`).includes(q)) }))
      .filter((t) => t.preguntas.length > 0);
  });
}
