import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ConfigService } from '../../core/config.service';
import { DocumentoLegal, type SeccionLegal } from './documento-legal';
import { datosResponsable } from './responsable';

/**
 * Política de tratamiento de datos personales (Ley 1581 de 2012 y Decreto 1377 de 2013, hoy compilado en el Decreto 1074 de
 * 2015). Los datos del responsable llegan de la configuración de la API (el repositorio es público).
 */
@Component({
  imports: [DocumentoLegal, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-documento-legal
      titulo="Política de tratamiento de datos personales"
      subtitulo="Ley 1581 de 2012 y Decreto 1377 de 2013 (compilado en el Decreto 1074 de 2015)"
      [version]="legal()?.versionPoliticaDatos"
      [vigenteDesde]="legal()?.fechaVigencia"
      [secciones]="secciones()"
    >
      <div class="mt-10 rounded-tarjeta bg-bosque-50 p-6 dark:bg-bosque-950/50 print:hidden">
        <p class="font-semibold">Ejerce tus derechos en cualquier momento</p>
        <p class="mt-1 text-sm text-tenue">
          Descarga una copia de tus datos o elimina tu cuenta desde la sección de privacidad, o radica una consulta o un reclamo.
        </p>
        <div class="mt-4 flex flex-wrap gap-2">
          <a routerLink="/cuenta/privacidad" class="btn btn-primario">Ir a Privacidad y datos</a>
          <a routerLink="/cuenta/soporte" class="btn btn-secundario">Radicar una consulta o un reclamo</a>
        </div>
      </div>
    </app-documento-legal>
  `,
})
export default class Privacidad {
  private readonly config = inject(ConfigService);
  protected readonly legal = this.config.legal;

  protected readonly secciones = computed<SeccionLegal[]>(() => {
    const r = datosResponsable(this.legal());
    return [
      {
        titulo: 'Responsable del tratamiento',
        parrafos: [
          `El responsable del tratamiento de los datos personales recolectados en Trueke Bogotá Solidario es ${r.identificacion}, con domicilio en ${r.domicilio}.`,
          `Canales de atención: la página de PQRS (con o sin cuenta; con cuenta también en Cuenta → Soporte y PQR) y el correo ${r.correo}${r.telefono ? `, o el teléfono ${r.telefono}` : ''}.`,
        ],
      },
      {
        titulo: 'Datos que tratamos',
        parrafos: ['Tratamos solo los datos necesarios para cada finalidad:'],
        lista: [
          'Identificación y contacto: nombre y apellido, correo electrónico y, si entras con Google, el nombre, el correo verificado y la foto que Google comparte.',
          'Fecha de nacimiento: solo para comprobar que eres mayor de 18 años. Nunca se muestra a otras personas.',
          'Ubicación: municipio y barrio o localidad de residencia y, si decides usarla, la ubicación aproximada de tus publicaciones. La ubicación exacta solo se comparte con la contraparte de una solicitud aceptada.',
          'Contenido que publicas: publicaciones, fotos, comentarios, mensajes del chat, calificaciones y denuncias.',
          'Actividad en la plataforma: intercambios, Eco-Puntos, pagos, facturas y solicitudes de soporte.',
          'Datos de facturación, si los registras: tipo y número de documento, nombre o razón social, correo y dirección.',
          'Registro de vendedor, solo si vendes en modo Compra y venta: nombre o razón social, tipo y número de documento, dirección de notificaciones, municipio y teléfono. El documento, la dirección y el teléfono se guardan cifrados y nunca se muestran a otras personas de la comunidad.',
          'Datos técnicos: dirección IP, tipo de dispositivo y registros de seguridad (inicios de sesión, cambios de contraseña), para proteger tu cuenta y prevenir fraudes.',
        ],
      },
      {
        titulo: 'Finalidades',
        parrafos: ['Necesarias para prestar el servicio (sin ellas no es posible usar la plataforma):'],
        lista: [
          'Crear y administrar tu cuenta, verificar tu correo y comprobar tu mayoría de edad.',
          'Publicar objetos y permitir el intercambio, la compra y la donación entre personas, y la comunicación por el chat interno.',
          'Otorgar Eco-Puntos y reputación, y prevenir el fraude, la suplantación y el abuso (por ejemplo, cuentas duplicadas).',
          'Procesar los pagos de los servicios opcionales y emitir las facturas electrónicas.',
          'Atender peticiones, quejas, reclamos y solicitudes de retracto, y aplicar la moderación con debido proceso.',
          'Llevar el registro de quienes venden que exige el Estatuto del Consumidor a un portal de contacto (Ley 1480, art. 53), y entregarlo solo a quien te compró y presenta un reclamo por PQRS, o a la autoridad que lo pida. Cada consulta del equipo queda registrada.',
          'Cumplir obligaciones legales, contables y tributarias, y responder a requerimientos de autoridades competentes.',
          'Opcionales, solo si lo autorizas en tu cuenta: avisos por correo cuando no estás conectado y notificaciones push en tu dispositivo. Puedes desactivarlos cuando quieras.',
        ],
      },
      {
        titulo: 'Datos sensibles',
        parrafos: [
          'La foto de perfil donde se ve tu cara y la imagen de tu documento de identidad para verificar la cuenta pueden considerarse datos sensibles. Su entrega es facultativa: no estás obligado a autorizar su tratamiento, y sin ellos puedes explorar el catálogo.',
          'La foto de perfil se pide para publicar, solicitar, chatear y pagar, para que la comunidad sepa con quién intercambia. Antes de publicarla le quitamos la ubicación y los datos ocultos (EXIF). La imagen del documento se guarda en un almacenamiento privado y se elimina en cuanto se resuelve la verificación.',
        ],
      },
      {
        titulo: 'Menores de edad',
        parrafos: [
          'La plataforma es solo para personas mayores de 18 años y no tratamos datos de niños, niñas ni adolescentes. Si alguien indica una edad menor, no se crea la cuenta ni se guarda su fecha. Si una cuenta ya creada registra una fecha de menor de edad, queda en revisión: se ocultan sus publicaciones, no puede usar la plataforma ni cambiar la fecha, y conserva la descarga y la eliminación de sus datos y la PQRS. Si confirmamos que es la cuenta de un menor, la suprimimos junto con sus datos.',
        ],
      },
      {
        titulo: 'Encargados y transferencias internacionales',
        parrafos: [
          'No vendemos ni alquilamos tus datos, ni los compartimos con terceros con fines comerciales. Para operar usamos proveedores que tratan datos por cuenta nuestra (encargados), algunos con servidores fuera de Colombia. Al autorizar esta política autorizas esa transmisión, que se hace con proveedores que ofrecen niveles adecuados de protección:',
        ],
        lista: [
          'Supabase (Estados Unidos): base de datos y almacenamiento de fotos y documentos.',
          'Render (Estados Unidos): servidor donde funciona la aplicación.',
          'GitHub Pages (Estados Unidos): publicación de la página web.',
          'Brevo (Francia, Unión Europea): envío de correos.',
          'Sentry (Alemania, Unión Europea): registro de fallas técnicas de la aplicación para corregirlas. Recibe la descripción técnica de la falla, la página donde ocurrió (sin parámetros) y el tipo de navegador. No recibe tu nombre, tu correo, tu documento, tu ubicación ni tu dirección IP.',
          'Wompi (Colombia): procesamiento de los pagos. Los datos de tu tarjeta o cuenta los recibe directamente Wompi; nosotros no los vemos ni los guardamos.',
          'Google (Estados Unidos): inicio de sesión con Google, si lo eliges, y verificación contra robots (reCAPTCHA).',
          'Groq (Estados Unidos): asistente de ayuda con inteligencia artificial, solo si tienes sesión y le escribes. Recibe tu pregunta, lo que hablaste antes con el asistente y, cuando hace falta para responderte, tu saldo de Eco-Puntos y el estado de tus publicaciones y solicitudes. No recibe tu correo, tu documento ni tu ubicación, y nosotros no guardamos la conversación.',
        ],
      },
      {
        titulo: 'Tus derechos',
        parrafos: ['Como titular de los datos tienes derecho a (Ley 1581, art. 8):'],
        lista: [
          'Conocer, actualizar y rectificar tus datos.',
          'Solicitar prueba de la autorización que otorgaste.',
          'Ser informado del uso que se les ha dado.',
          'Presentar quejas ante la Superintendencia de Industria y Comercio (SIC), después de agotar el trámite de consulta o reclamo ante nosotros.',
          'Revocar la autorización y pedir la supresión de tus datos, cuando no exista un deber legal o contractual de conservarlos.',
          'Acceder gratis a tus datos. Desde la aplicación puedes descargarlos (formato JSON) y eliminar tu cuenta en Cuenta → Privacidad y datos.',
        ],
      },
      {
        titulo: 'Consultas y reclamos',
        parrafos: [
          'Consultas: si quieres saber qué datos tenemos y cómo los usamos, la respondemos en máximo 10 días hábiles desde que la recibimos. Si no es posible en ese plazo, te informamos el motivo y la respondemos dentro de los 5 días hábiles siguientes.',
          'Reclamos: si quieres corregir, actualizar o suprimir tus datos, o crees que incumplimos la ley, lo respondemos en máximo 15 días hábiles. Si no es posible, te informamos el motivo y lo respondemos dentro de los 8 días hábiles siguientes. Si al reclamo le falta información, te la pedimos en los 5 días siguientes; si pasan 2 meses sin que la envíes, se entiende que desististe.',
          `Para radicarlos usa la página de PQRS (no necesitas cuenta) o Cuenta → Soporte y PQR, eligiendo "Consulta de mis datos" o "Reclamo", o escribe a ${r.correo}, indicando tu nombre, tu correo, lo que pides y, si es un reclamo, los hechos y documentos que lo soportan. Recibirás un número de radicado.`,
        ],
      },
      {
        titulo: 'Conservación',
        parrafos: [
          'Conservamos tus datos mientras tengas una cuenta activa. Al eliminarla, borramos o anonimizamos tus datos personales y retiramos tu contenido público.',
          'Conservamos solo lo que la ley obliga a guardar: las facturas y los pagos se guardan por el tiempo que exigen las normas contables y tributarias (hasta 10 años), y los registros de seguridad por el tiempo necesario para investigar fraudes.',
        ],
      },
      {
        titulo: 'Cookies y almacenamiento en tu navegador',
        parrafos: [
          'No usamos cookies de publicidad ni de seguimiento. Solo usamos lo necesario para que la plataforma funcione:',
        ],
        lista: [
          'Una cookie segura (HttpOnly) que mantiene tu sesión iniciada.',
          'El almacenamiento local del navegador para recordar el modo claro u oscuro, el borrador de una publicación y el código de invitación con el que llegaste. Si el asistente de ayuda pasa tu conversación a una PQRS, ese texto queda solo en esa pestaña hasta que se abre el formulario (máximo 10 minutos).',
          'reCAPTCHA de Google, que puede usar sus propias cookies para distinguir personas de robots en el registro y el inicio de sesión.',
        ],
      },
      {
        titulo: 'Seguridad',
        parrafos: [
          'Aplicamos medidas técnicas y administrativas para proteger tus datos: cifrado en tránsito (HTTPS), contraseñas guardadas con algoritmos de derivación seguros, verificación en dos pasos, control de acceso por roles, registros de auditoría y copias de seguridad. El equipo de moderación solo accede a lo necesario para resolver denuncias y solicitudes.',
        ],
      },
      {
        titulo: 'Vigencia y cambios',
        parrafos: [
          `Esta política rige desde el ${r.vigencia} (versión ${r.versionPolitica}). Si la cambiamos, publicaremos la nueva versión y te pediremos aceptarla antes de seguir usando la plataforma.`,
          'Registro Nacional de Bases de Datos: la inscripción ante la SIC es obligatoria para sociedades y entidades sin ánimo de lucro con activos superiores a 100.000 UVT y para entidades públicas (Decreto 090 de 2018). El responsable inscribirá las bases de datos si llega a estar obligado.',
        ],
      },
    ];
  });
}
