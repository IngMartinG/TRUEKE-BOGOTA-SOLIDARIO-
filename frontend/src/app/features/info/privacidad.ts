import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ConfigService } from '../../core/config.service';
import { DocumentoLegal, type SeccionLegal } from './documento-legal';

@Component({
  imports: [DocumentoLegal, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-documento-legal
      titulo="Política de tratamiento de datos personales"
      [subtitulo]="'Versión ' + (config.config().versionPoliticaDatos ?? 'vigente') + ' · Ley 1581 de 2012 y Decreto 1377 de 2013'"
      [secciones]="secciones"
    >
      <div class="mt-10 rounded-tarjeta bg-bosque-50 p-6 dark:bg-bosque-950/50">
        <p class="font-semibold">Ejerce tus derechos en cualquier momento</p>
        <p class="mt-1 text-sm text-tenue">Descarga una copia de tus datos o elimina tu cuenta desde la sección de privacidad.</p>
        <a routerLink="/cuenta/privacidad" class="btn btn-primario mt-4">Ir a Privacidad y datos</a>
      </div>
    </app-documento-legal>
  `,
})
export default class Privacidad {
  protected readonly config = inject(ConfigService);
  protected readonly secciones: SeccionLegal[] = [
    {
      titulo: 'Responsable del tratamiento',
      parrafos: [
        'Trueke Bogotá Solidario es responsable del tratamiento de los datos personales que recolecta a través de esta plataforma, en cumplimiento de la Ley Estatutaria 1581 de 2012 y sus decretos reglamentarios.',
      ],
    },
    {
      titulo: 'Datos que recolectamos',
      parrafos: [
        'Nombre, correo electrónico, municipio y barrio o localidad de residencia, datos de facturación si los registras (documento, nombre o razón social, correo y dirección) y, si decides usarla, la ubicación aproximada de tus publicaciones. Si vinculas tu cuenta de Google, recibimos tu nombre y correo verificados por Google.',
        'También guardamos el contenido que publicas (publicaciones, fotos, comentarios y mensajes del chat), tus intercambios, calificaciones y movimientos de Eco-Puntos.',
        'Si solicitas la verificación de tu cuenta, recibimos una imagen de tu documento de identidad, que se elimina en cuanto se resuelve la solicitud.',
      ],
    },
    {
      titulo: 'Finalidades',
      parrafos: [
        'Prestar el servicio de intercambio, compra y donación; permitir la comunicación entre usuarios mediante el chat interno; otorgar Eco-Puntos y reputación; prevenir fraudes y abusos; y enviarte notificaciones relacionadas con tu actividad.',
        'No vendemos ni compartimos tus datos con terceros con fines comerciales. Tu correo nunca se muestra a otros usuarios y tu ubicación exacta solo se comparte con la contraparte de una solicitud aceptada.',
      ],
    },
    {
      titulo: 'Tus derechos',
      parrafos: [
        'Conocer, actualizar y rectificar tus datos; solicitar prueba de la autorización otorgada; ser informado del uso que se les da; presentar quejas ante la Superintendencia de Industria y Comercio; revocar la autorización y solicitar la supresión de tus datos.',
        'Puedes ejercer estos derechos directamente desde la aplicación: exportar tus datos en formato JSON o eliminar tu cuenta de forma permanente.',
      ],
    },
    {
      titulo: 'Seguridad y conservación',
      parrafos: [
        'Aplicamos medidas técnicas como cifrado en tránsito (HTTPS), contraseñas protegidas con algoritmos de derivación seguros, verificación en dos pasos y registros de auditoría. Los datos se conservan mientras tengas una cuenta activa y se eliminan o anonimizan al suprimirla, salvo obligación legal de conservarlos.',
      ],
    },
  ];
}
