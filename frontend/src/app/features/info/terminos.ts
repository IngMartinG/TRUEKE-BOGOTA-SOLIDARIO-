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
        'Trueke Bogotá Solidario es una plataforma que conecta a personas de Bogotá para intercambiar, vender de segunda mano o donar objetos. La plataforma no es parte de los acuerdos entre usuarios ni garantiza el estado de los objetos.',
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
        'Solo puedes publicar objetos propios, legales y en el estado que describes. Están prohibidos los artículos ilegales, armas, medicamentos, animales, contenido ofensivo y cualquier publicación engañosa.',
        'El equipo de moderación puede ocultar contenido que incumpla estas reglas y suspender cuentas que reincidan.',
      ],
    },
    {
      titulo: 'Eco-Puntos y pagos',
      parrafos: [
        'Los Eco-Puntos son una moneda interna sin valor monetario: no se pueden retirar, transferir ni convertir en dinero. Se otorgan cuando ambas partes confirman la entrega de un intercambio.',
        'Los pagos de servicios opcionales (destacar, verificar, planes) se procesan con Wompi. La plataforma nunca procesa pagos entre usuarios: los acuerdos de compra se pagan directamente entre las partes.',
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
