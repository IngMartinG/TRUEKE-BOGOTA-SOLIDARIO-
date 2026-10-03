import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DocumentoLegal, type SeccionLegal } from './documento-legal';

@Component({
  imports: [DocumentoLegal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-documento-legal titulo="Términos de uso" subtitulo="Reglas de convivencia de la comunidad Trueke Bogotá Solidario" [secciones]="secciones" />`,
})
export default class Terminos {
  protected readonly secciones: SeccionLegal[] = [
    {
      titulo: 'Objeto',
      parrafos: [
        'Trueke Bogotá Solidario es un portal de contacto que conecta a personas de Colombia para intercambiar, vender de segunda mano o donar objetos. La plataforma no es parte de los acuerdos entre usuarios ni garantiza el estado de los objetos: cada persona declara el estado (nuevo, usado, reparado…) y responde por la veracidad de lo que publica.',
      ],
    },
    {
      titulo: 'Cuentas',
      parrafos: [
        'Debes ser mayor de edad, dar información veraz y mantener la confidencialidad de tu contraseña. Eres responsable de la actividad realizada desde tu cuenta.',
      ],
    },
    {
      titulo: 'Publicaciones permitidas',
      parrafos: [
        'Solo puedes publicar objetos propios, legales y en el estado que describes. Si el objeto tiene detalles, fue reparado o solo sirve para repuestos, debes indicarlo. Están prohibidos los artículos ilegales, armas, medicamentos, animales, contenido ofensivo y cualquier publicación engañosa.',
        'Quien venda de forma habitual debe identificarse (Ley 1480 de 2011, art. 53): para tener más de 5 artículos a la venta al mismo tiempo se exige identidad verificada o el plan Empresa (con NIT).',
        'El equipo de moderación puede ocultar contenido que incumpla estas reglas y suspender cuentas que reincidan.',
      ],
    },
    {
      titulo: 'Eco-Puntos',
      parrafos: [
        'Los Eco-Puntos son un crédito de servicios dentro de la plataforma: no se pueden retirar, transferir ni convertir en dinero. Se ganan cuando ambas partes confirman la entrega de un intercambio (máximo 5 al día y una vez cada 30 días con la misma persona) y al confirmar el correo de una cuenta nueva.',
        'Se usan para impulsar publicaciones y para obtener descuentos en los servicios pagos. Crear varias cuentas o simular intercambios para obtener puntos o reputación es causa de suspensión.',
      ],
    },
    {
      titulo: 'Pagos, facturación y planes',
      parrafos: [
        'Los servicios opcionales (destacar, verificar la identidad, planes Premium y Empresa y recargas de Eco-Puntos) se pagan con Wompi. Los precios están en pesos colombianos e incluyen el IVA. La plataforma nunca procesa pagos entre usuarios: los acuerdos de compra se pagan directamente entre las partes.',
        'Cada pago aprobado genera una factura electrónica. Puedes registrar tus datos de facturación en tu cuenta; si no lo haces, la factura se emite a "consumidor final".',
        'Los planes duran 30 días y no se renuevan automáticamente: te avisamos antes del vencimiento. Si renuevas antes, los días se suman al periodo actual.',
      ],
    },
    {
      titulo: 'Retracto, reversión del pago y garantía',
      parrafos: [
        'Derecho de retracto (Ley 1480, art. 47): puedes desistir de una compra dentro de los 5 días hábiles siguientes. No aplica a servicios cuya prestación ya comenzó con tu consentimiento (por ejemplo, un destacado ya activo o una verificación ya resuelta). Si procede, devolvemos el dinero y se retira el beneficio.',
        'Reversión del pago (Ley 1480, art. 51): si el pago fue por fraude, no autorizado, o el servicio no se prestó, puedes solicitar la reversión. También puedes pedirla al emisor de tu medio de pago.',
        'Estas solicitudes, y cualquier petición, queja o reclamo, se radican en Cuenta → Soporte y PQR. Respondemos por escrito en máximo 15 días hábiles.',
      ],
    },
    {
      titulo: 'Conducta',
      parrafos: [
        'Trata a los demás con respeto, cumple los acuerdos y usa el chat interno para coordinar. Reporta cualquier comportamiento sospechoso. Recomendamos realizar las entregas en lugares públicos.',
      ],
    },
  ];
}
