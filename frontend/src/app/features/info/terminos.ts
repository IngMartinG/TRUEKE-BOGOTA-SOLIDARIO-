import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ConfigService } from '../../core/config.service';
import { DocumentoLegal, type SeccionLegal } from './documento-legal';
import { datosResponsable } from './responsable';

/** Términos de uso. Los datos del prestador llegan de la configuración de la API (el repositorio es público). */
@Component({
  imports: [DocumentoLegal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-documento-legal
      titulo="Términos de uso"
      subtitulo="Reglas para usar Trueke Bogotá Solidario y convivir en la comunidad"
      [version]="legal()?.versionTerminos"
      [vigenteDesde]="legal()?.fechaVigencia"
      [secciones]="secciones()"
    />
  `,
})
export default class Terminos {
  private readonly config = inject(ConfigService);
  protected readonly legal = this.config.legal;

  protected readonly secciones = computed<SeccionLegal[]>(() => {
    const r = datosResponsable(this.legal());
    return [
      {
        titulo: 'Quién presta el servicio',
        parrafos: [
          `Trueke Bogotá Solidario es un servicio prestado por ${r.identificacion}, con domicilio en ${r.domicilio}. Contacto: ${r.correo}${r.telefono ? ` · ${r.telefono}` : ''} y el formulario de PQRS de la plataforma.`,
          'Al crear tu cuenta aceptas estos términos. La autorización para tratar tus datos personales se da por separado, en la política de tratamiento de datos.',
        ],
      },
      {
        titulo: 'Qué es la plataforma',
        parrafos: [
          'Es un portal de contacto (Ley 1480 de 2011, art. 53) que conecta a personas en Colombia para intercambiar (trueke), vender de segunda mano o donar objetos. La plataforma no es vendedora, compradora ni intermediaria de los acuerdos entre usuarios: no es dueña de los objetos, no los revisa físicamente ni garantiza su estado.',
          'Cada persona declara el estado del objeto (nuevo, usado, reparado…) y responde por la veracidad de lo que publica y por cumplir lo que acuerda. Para vender se exige, una sola vez, el registro de vendedor: nombre, documento, dirección de notificaciones y teléfono (Ley 1480, art. 53). No se muestra a la comunidad; solo se entrega a quien te compró y reclama, o a la autoridad. Quien vende de forma habitual debe además identificarse: para tener más de 5 artículos a la venta al mismo tiempo se exige la identidad verificada o el plan Empresa (con NIT).',
        ],
      },
      {
        titulo: 'Solo para mayores de 18 años',
        parrafos: [
          'Solo pueden usar la plataforma personas mayores de 18 años. Al crear la cuenta registras tu fecha de nacimiento y declaras que es verdadera. Las cuentas de menores de edad se suprimen.',
        ],
      },
      {
        titulo: 'Tu cuenta y su seguridad',
        parrafos: [
          'Debes dar información veraz (nombre y apellido reales) y mantenerla actualizada. Cada persona puede tener una sola cuenta. Cuida tu contraseña, activa la verificación en dos pasos y avísanos si sospechas que alguien entró a tu cuenta: eres responsable de lo que se haga desde ella.',
          'Para publicar, solicitar, chatear y pagar se exige el correo verificado y una foto de perfil donde se vea tu cara.',
        ],
      },
      {
        titulo: 'Fundaciones verificadas',
        parrafos: [
          'Una cuenta que representa a una entidad sin ánimo de lucro puede pedir, sin costo, la insignia «Fundación verificada». Para eso declara que es su representante legal o que este la autorizó, y envía la razón social, el NIT y el certificado de existencia y representación legal de la Cámara de Comercio. El equipo lo revisa y puede aprobar o rechazar la solicitud explicando el motivo.',
          'La insignia solo indica que revisamos esos documentos: no es un aval de la gestión de la entidad ni de cómo usa lo que recibe. La cuenta sigue las mismas reglas que las demás (también puede truekear y vender) y donarle a una fundación da los mismos Eco-Puntos que cualquier donación.',
          'Retiramos la insignia, con aviso del motivo, si la entidad deja de existir, si los datos no eran ciertos o si se usa para engañar a la comunidad; en los casos graves, además, se aplica la moderación de la cuenta.',
        ],
      },
      {
        titulo: 'Empresas, campañas e informes de impacto',
        parrafos: [
          'Una cuenta puede registrar a una empresa (persona jurídica) declarando que es su representante legal o que este la autorizó, con la razón social, el NIT y el certificado de existencia y representación legal de la Cámara de Comercio. El equipo lo revisa y puede aprobar o rechazar el registro explicando el motivo. El servicio para empresas se acuerda aparte, por cotización; la plataforma no cobra nada por registrarla.',
          'Una vez verificada, sus Admin invitan a otras cuentas con un enlace de un solo uso y crean campañas. Cada miembro sigue siendo responsable de sus publicaciones e intercambios y las reglas de la comunidad no cambian por pertenecer a una empresa.',
          'Los informes de impacto los emite el equipo de Trueke con la metodología publicada y solo cuentan las entregas que confirmaron las dos partes. Son estimaciones, no una certificación ni una verificación de tercera parte, y no identifican a las personas que recibieron los objetos.',
          'Retiramos la verificación, con aviso del motivo, si la empresa deja de existir, si los datos no eran ciertos o si usa la plataforma para engañar; los informes ya emitidos se conservan.',
        ],
      },
      {
        titulo: 'Publicaciones inactivas',
        parrafos: [
          'Para que el catálogo muestre solo lo que de verdad está disponible, si una publicación pasa 30 días sin actividad (sin editarla, impulsarla, confirmarla ni recibir solicitudes) te preguntamos si sigue disponible, con una notificación y, si tienes activos los avisos de intercambios, un correo.',
          'Si no respondes en los 7 días siguientes, la pausamos: sale del catálogo, pero no se borra y puedes reactivarla desde Mis publicaciones cuando quieras. Las publicaciones pausadas siguen contando para el límite de tu plan.',
        ],
      },
      {
        titulo: 'Publicaciones prohibidas',
        parrafos: ['Solo puedes publicar objetos propios, legales y en el estado que describes. Está prohibido publicar:'],
        lista: [
          'Armas, municiones, explosivos y sus partes.',
          'Medicamentos, sustancias controladas, drogas y productos de uso médico que requieran registro sanitario o fórmula.',
          'Animales vivos.',
          'Alimentos perecederos o preparados.',
          'Productos ilegales, robados, de contrabando, falsificados o que infrinjan derechos de autor o marcas.',
          'Datos personales de terceros (documentos, fotos o información de otras personas sin su autorización).',
          'Contenido sexual, violento, discriminatorio u ofensivo, y publicaciones engañosas o de estafa.',
          'Servicios, inmuebles o cualquier cosa que no sea un objeto que puedas entregar.',
        ],
      },
      {
        titulo: 'Eco-Puntos',
        parrafos: [
          'Los Eco-Puntos son un crédito de servicios dentro de la plataforma, sin valor monetario: no se pueden retirar, transferir, vender ni convertir en dinero, y no se reembolsan en dinero. Si cierras tu cuenta o es suspendida por fraude, se pierden.',
          'Se ganan cuando ambas partes confirman la entrega de un intercambio (máximo 5 al día y una vez cada 30 días con la misma persona), al confirmar el correo de una cuenta nueva y al invitar a alguien que completa su primer intercambio (20 Eco-Puntos para cada persona; no cuenta si ese primer intercambio es con quien invitó, y quien invita recibe puntos por máximo 10 invitaciones cada 30 días). Las recargas compran Eco-Puntos con dinero (100 pesos = 1 Eco-Punto).',
          'Se usan para impulsar publicaciones y para obtener descuentos en los servicios pagos. Crear varias cuentas o simular intercambios para obtener puntos o reputación es causa de suspensión y de la pérdida de los puntos.',
        ],
      },
      {
        titulo: 'Pagos, facturación y planes',
        parrafos: [
          'Los servicios opcionales (destacar, verificar la identidad, planes Premium y Empresa y recargas de Eco-Puntos) se pagan con Wompi. Destacar y verificar también se pueden pagar, en parte o del todo, con Eco-Puntos (1 punto = 100 pesos), solo si la persona lo elige. Las recargas parten de 5.000 pesos, y los paquetes de 10.000, 20.000 y 50.000 pesos suman Eco-Puntos de bono. Lo pagado del todo con puntos no genera cobro ni factura.',
          'Los precios están en pesos colombianos e incluyen el IVA. Cada pago aprobado genera una factura electrónica: puedes registrar tus datos de facturación en tu cuenta; si no lo haces, se emite a "consumidor final".',
          'La plataforma nunca procesa pagos entre usuarios: los acuerdos de compra se pagan directamente entre las partes, que acuerdan el medio de pago y la entrega.',
          'Los planes Premium y Empresa se pagan por 30 días o, con el plan anual, por 365 días en un solo pago. No se renuevan automáticamente: te avisamos antes del vencimiento. Si renuevas antes, los días se suman al periodo actual.',
        ],
      },
      {
        titulo: 'Retracto y reversión del pago',
        parrafos: [
          'Derecho de retracto (Ley 1480, art. 47): puedes desistir de un servicio pagado dentro de los 5 días hábiles siguientes a la compra. No aplica a servicios cuya prestación ya comenzó con tu consentimiento (por ejemplo, un destacado ya activo o una verificación ya resuelta). Si procede, devolvemos el dinero y se retira el beneficio.',
          'Reversión del pago (Ley 1480, art. 51): si el pago fue por fraude, no lo autorizaste o el servicio no se prestó, puedes pedir la reversión dentro de los 5 días hábiles siguientes a la fecha en que conociste el problema. También puedes pedirla al emisor de tu medio de pago.',
          'El retracto y la reversión se piden en Cuenta → Soporte y PQR, indicando el pago. Cualquier petición, queja o reclamo también se puede radicar, sin cuenta, en la página de PQRS. Respondemos por escrito en máximo 15 días hábiles.',
        ],
      },
      {
        titulo: 'Tu contenido y nuestra marca',
        parrafos: [
          'Lo que publicas (fotos, textos, comentarios) sigue siendo tuyo. Al publicarlo nos das una licencia gratuita y no exclusiva para mostrarlo, adaptarlo al formato de la plataforma (por ejemplo, reducir el tamaño de las fotos) y compartir su vista previa en redes, mientras esté publicado. Declaras que tienes derecho a publicarlo.',
          'El nombre, el logo, el diseño y el código de Trueke Bogotá Solidario pertenecen a su titular y no pueden usarse sin autorización.',
        ],
      },
      {
        titulo: 'Conducta, moderación y debido proceso',
        parrafos: [
          'Trata a los demás con respeto, cumple lo que acuerdas y usa el chat interno para coordinar: no compartas datos personales ni pidas pagos por fuera de lo acordado. Te recomendamos hacer las entregas en lugares públicos y seguros.',
          'Cualquier persona puede denunciar una publicación, un comentario, un mensaje o una cuenta. El equipo de moderación revisa la denuncia y puede ocultar el contenido o suspender la cuenta. A la persona denunciada se le avisa la decisión sin revelar quién la denunció, y puede apelar dentro de los 15 días siguientes; la apelación la resuelve un moderador distinto.',
          'Las infracciones graves (estafa, suplantación, contenido ilegal) o repetidas pueden llevar a la suspensión definitiva de la cuenta.',
        ],
      },
      {
        titulo: 'Responsabilidad',
        parrafos: [
          'Respondemos por los servicios que prestamos y cobramos directamente (destacados, verificación, planes y recargas) y por el funcionamiento de la plataforma conforme a la ley.',
          'Como portal de contacto, no respondemos por el estado, la calidad, la legalidad o la entrega de los objetos acordados entre usuarios, ni por el incumplimiento de esos acuerdos. Sí colaboramos con las autoridades y con las personas afectadas, entregando la información que la ley permita, y retiramos el contenido que incumpla estos términos.',
          'Nada de lo anterior limita los derechos que la ley te reconoce como consumidor.',
        ],
      },
      {
        titulo: 'Comercio electrónico',
        parrafos: [
          'Los mensajes de datos, aceptaciones electrónicas (como marcar una casilla) y registros de la plataforma tienen validez y fuerza probatoria (Ley 527 de 1999). Guardamos la versión de estos términos y de la política que aceptaste y la fecha en que lo hiciste.',
        ],
      },
      {
        titulo: 'Cambios a estos términos',
        parrafos: [
          'Podemos actualizar estos términos para reflejar cambios en el servicio o en la ley. Publicaremos la nueva versión con su fecha de vigencia y te pediremos aceptarla antes de seguir usando la plataforma. Si no estás de acuerdo, puedes descargar tus datos y eliminar tu cuenta.',
        ],
      },
      {
        titulo: 'Ley aplicable y jurisdicción',
        parrafos: [
          'Estos términos se rigen por la ley colombiana. Las controversias se resolverán ante los jueces de Bogotá D.C., sin perjuicio de tu derecho como consumidor a acudir a la Superintendencia de Industria y Comercio o a los jueces de tu domicilio.',
          `Versión ${r.versionTerminos}, vigente desde el ${r.vigencia}.`,
        ],
      },
    ];
  });
}
