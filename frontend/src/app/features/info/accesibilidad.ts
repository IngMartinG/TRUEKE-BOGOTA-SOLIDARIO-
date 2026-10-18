import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ConfigService } from '../../core/config.service';
import { DocumentoLegal, type SeccionLegal } from './documento-legal';
import { datosResponsable } from './responsable';

/** Fecha de la última revisión de accesibilidad. Se actualiza cada vez que se vuelve a evaluar la interfaz. */
const ULTIMA_EVALUACION = '2026-10-18';

/** Declaración de accesibilidad: lo que se cumple, lo que falta y cómo avisar. Debe reflejar el estado real. */
@Component({
  imports: [DocumentoLegal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-documento-legal
      titulo="Declaración de accesibilidad"
      subtitulo="Queremos que cualquier persona pueda intercambiar, comprar y donar en Trueke Bogotá Solidario"
      [vigenteDesde]="ultimaEvaluacion"
      [secciones]="secciones()"
    />
  `,
})
export default class Accesibilidad {
  private readonly config = inject(ConfigService);
  protected readonly ultimaEvaluacion = ULTIMA_EVALUACION;

  protected readonly secciones = computed<SeccionLegal[]>(() => {
    const r = datosResponsable(this.config.legal());
    return [
      {
        titulo: 'Nuestro compromiso y el estándar que seguimos',
        parrafos: [
          'Diseñamos la plataforma para que funcione con teclado, lectores de pantalla, aumento de texto, modo oscuro y la preferencia de reducir el movimiento. El objetivo es cumplir las Pautas de Accesibilidad para el Contenido Web (WCAG) 2.2 en su nivel AA.',
          'Tomamos como referencia la Ley 1618 de 2013, que garantiza los derechos de las personas con discapacidad, y los lineamientos de accesibilidad web de la Resolución 1519 de 2020 del Ministerio TIC (obligatoria para entidades públicas; la seguimos de forma voluntaria).',
          'Estado: cumplimiento parcial. La mayor parte de la plataforma cumple el nivel AA; las excepciones conocidas están más abajo.',
        ],
      },
      {
        titulo: 'Cómo la evaluamos',
        parrafos: [`Última evaluación: ${ULTIMA_EVALUACION}. Método: autoevaluación del equipo, que combina:`],
        lista: [
          'Revisión automática en cada cambio del código con las reglas de accesibilidad de Angular ESLint (etiquetas, roles, foco y textos alternativos).',
          'Pruebas de punta a punta en computador y celular que recorren los flujos principales.',
          'Revisión manual con teclado y con la preferencia de reducir el movimiento activada.',
          'Revisión del contraste de colores en modo claro y oscuro.',
        ],
      },
      {
        titulo: 'Lo que ya puedes hacer',
        parrafos: [],
        lista: [
          'Recorrer todo con el teclado: hay un enlace para saltar al contenido, el foco siempre se ve y las ventanas emergentes se cierran con Esc y devuelven el foco a donde estabas.',
          'Usar un lector de pantalla: los campos tienen su etiqueta y anuncian sus errores, los contadores de mensajes y notificaciones dicen cuántos hay sin leer, al cambiar de página se anuncia su título y en el chat se anuncian los mensajes que llegan.',
          'Leer con buen contraste: los textos, botones y bordes de los campos cumplen la relación de contraste mínima de WCAG AA, en modo claro y oscuro.',
          'Usar la app con el dedo: en pantallas táctiles los botones miden al menos 44 píxeles, y las acciones que se hacen deslizando (como responder un mensaje) también se pueden hacer con un toque.',
          'Pedir menos movimiento: si tu sistema tiene activada esa opción, quitamos los desplazamientos y animaciones de posición, las transiciones entre páginas y el zoom animado del mapa.',
          'Imprimir o guardar en PDF los documentos legales con un botón, y saltar a cada sección desde su índice.',
        ],
      },
      {
        titulo: 'Limitaciones conocidas',
        parrafos: [],
        lista: [
          'El mapa del catálogo y de las publicaciones es una herramienta visual. Toda su información (publicaciones, distancia y zona) también está en la lista y en el detalle de cada publicación.',
          'Las fotos las suben las personas que publican y no siempre tienen una descripción alternativa detallada: el lector de pantalla anuncia el título de la publicación.',
          'Algunas pantallas de administración y moderación, que usa solo el equipo de la plataforma, tienen tablas que en celular se recorren deslizando de lado.',
          'No hemos hecho todavía una auditoría con personas usuarias de lectores de pantalla ni una evaluación externa.',
          'El inicio de sesión con Google, la verificación reCAPTCHA y la pasarela de pagos son servicios de terceros: su accesibilidad depende de esos proveedores.',
        ],
      },
      {
        titulo: '¿Encontraste una barrera?',
        parrafos: [
          `Cuéntanos qué intentabas hacer, en qué pantalla y con qué dispositivo o ayuda técnica (por ejemplo, el lector de pantalla que usas). Escríbenos desde la página de PQRS (no necesitas cuenta) eligiendo "Queja", o al correo ${r.correo}. Respondemos en máximo 15 días hábiles y, si la barrera te impide usar un servicio, te ofrecemos otra forma de hacerlo mientras la corregimos.`,
          'Si no puedes ingresar a tu cuenta por una barrera de accesibilidad, usa la opción "¿La olvidaste?" de la pantalla de ingreso o radica una PQRS sin cuenta mencionando el correo de tu cuenta.',
        ],
      },
    ];
  });
}
