import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DocumentoLegal, type SeccionLegal } from './documento-legal';

/** Declaración de accesibilidad: lo que se cumple, lo que falta y cómo avisar. Debe reflejar el estado real. */
@Component({
  imports: [DocumentoLegal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-documento-legal titulo="Declaración de accesibilidad" subtitulo="Queremos que cualquier persona pueda intercambiar, comprar y donar en Trueke Bogotá Solidario" [secciones]="secciones" />`,
})
export default class Accesibilidad {
  protected readonly secciones: SeccionLegal[] = [
    {
      titulo: 'Nuestro compromiso',
      parrafos: [
        'Diseñamos la plataforma para que funcione con teclado, lectores de pantalla, aumento de texto, modo oscuro y la preferencia de reducir el movimiento. Tomamos como referencia las Pautas de Accesibilidad para el Contenido Web (WCAG) 2.2 en su nivel AA.',
        'Esta declaración describe el estado actual: la revisamos cada vez que hacemos cambios importantes en la interfaz.',
      ],
    },
    {
      titulo: 'Lo que ya puedes hacer',
      parrafos: [
        'Recorrer todo con el teclado: hay un enlace para saltar al contenido, el foco siempre se ve y las ventanas emergentes se cierran con Esc y devuelven el foco a donde estabas.',
        'Usar un lector de pantalla: los campos de formulario tienen su etiqueta y anuncian sus errores, los contadores de mensajes y notificaciones dicen cuántos hay sin leer, al cambiar de página se anuncia su título y en el chat se anuncian los mensajes que llegan.',
        'Leer con buen contraste: los textos, botones y bordes de los campos cumplen la relación de contraste mínima de WCAG AA, en modo claro y oscuro.',
        'Usar la app con el dedo: en pantallas táctiles los botones miden al menos 44 píxeles, y las acciones que se hacen deslizando (como responder un mensaje) también se pueden hacer con un toque.',
        'Pedir menos movimiento: si tu sistema tiene activada la opción de reducir el movimiento, quitamos los desplazamientos y animaciones de posición, las transiciones entre páginas y el zoom animado del mapa.',
      ],
    },
    {
      titulo: 'Limitaciones conocidas',
      parrafos: [
        'El mapa del catálogo y de las publicaciones es una herramienta visual. Toda su información (publicaciones, distancia y zona) también está en la lista y en el detalle de cada publicación.',
        'Las fotos las suben las personas que publican y no siempre tienen una descripción alternativa detallada: el lector de pantalla anuncia el título de la publicación.',
        'Algunas pantallas de administración y moderación, que usa solo el equipo de la plataforma, tienen tablas que en celular se recorren deslizando de lado.',
        'El inicio de sesión con Google y la pasarela de pagos son servicios de terceros: su accesibilidad depende de esos proveedores.',
      ],
    },
    {
      titulo: '¿Encontraste una barrera?',
      parrafos: [
        'Cuéntanos qué intentabas hacer, en qué pantalla y con qué dispositivo o ayuda técnica (por ejemplo, el lector de pantalla que usas). Puedes escribirnos desde Cuenta → Soporte y PQR eligiendo "Queja"; respondemos dentro de los plazos de ley para las PQR (15 días hábiles).',
        'Si no puedes ingresar a tu cuenta por una barrera de accesibilidad, usa la opción "¿La olvidaste?" de la pantalla de ingreso o pídele a alguien de confianza que nos escriba desde su cuenta mencionando la tuya.',
      ],
    },
  ];
}
