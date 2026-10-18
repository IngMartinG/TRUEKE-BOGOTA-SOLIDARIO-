import { Injectable, signal } from '@angular/core';
import { TIPOS_PQR, type TipoPqrDto } from '../../api/tipos';
import { tipoParaEscalar } from './motor';

export interface TurnoConversacion {
  pregunta: string;
  /** Pregunta frecuente con la que contestó el asistente, o null si no encontró nada. */
  respondio: string | null;
  sirvio: boolean | null;
}

export interface Borrador {
  tipo: TipoPqrDto;
  asunto: string;
  descripcion: string;
}

export interface Escalado {
  ruta: string;
  tipo: TipoPqrDto;
  borrador: Borrador;
}

/** Quita caracteres de control (el formulario los rechaza) y recorta. */
const limpiar = (t: string, max: number) =>
  t
    // eslint-disable-next-line no-control-regex
    .replace(/[\u0000-\u0009\u000B-\u001F\u007F-\u009F\u180E\u200B-\u200F\u2028-\u202E\u2060-\u206F\uFEFF]/g, '')
    .trim()
    .slice(0, max);

/** Arma la PQRS a partir de lo que se habló con el asistente. Con sesión va a la cuenta; sin sesión, a la pública. */
export function escalarAPqrs(turnos: readonly TurnoConversacion[], tipo: TipoPqrDto, conSesion: boolean): Escalado {
  const final = tipoParaEscalar(tipo, conSesion);
  const etiqueta = TIPOS_PQR.find((t) => t.valor === final)?.etiqueta ?? 'Solicitud';
  const ultima = turnos.at(-1)?.pregunta.replace(/\s+/g, ' ').trim() ?? '';
  let asunto = limpiar(ultima, 120);
  if (asunto.length < 5) asunto = `${etiqueta} desde el asistente de ayuda`;

  const lineas = turnos.map((t) => {
    const r = t.respondio ? `El asistente respondió con «${t.respondio}»${t.sirvio === false ? ', pero no me sirvió' : ''}.` : 'El asistente no encontró una respuesta.';
    return `• Pregunté: «${t.pregunta.trim()}». ${r}`;
  });
  const descripcion = limpiar(`${lineas.join('\n')}\n\nMás detalles: `, 2000);
  return { ruta: conSesion ? '/cuenta/soporte' : '/pqrs', tipo: final, borrador: { tipo: final, asunto, descripcion } };
}

/**
 * Lleva el texto de la conversación hasta el formulario de PQRS en memoria, no en la URL: lo que la persona escribió
 * puede tener datos personales y no debe quedar en el historial ni en registros de servidores.
 */
@Injectable({ providedIn: 'root' })
export class BorradorPqrs {
  private readonly _pendiente = signal<Borrador | null>(null);
  /** Los formularios lo observan: así también se llena si la persona ya estaba en la página de PQRS. */
  readonly pendiente = this._pendiente.asReadonly();

  guardar(b: Borrador): void {
    this._pendiente.set(b);
  }

  /** Lo entrega una sola vez (al recargar la página ya no está). */
  tomar(): Borrador | null {
    const b = this._pendiente();
    this._pendiente.set(null);
    return b;
  }
}
