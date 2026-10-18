import { inject, Injectable } from '@angular/core';
import { catchError, firstValueFrom, of, timeout } from 'rxjs';
import type { TipoPqrDto } from '../../api/tipos';
import { CuentaApi } from '../../core/api/cuenta.api';
import { respuestaDe, type EntradaConocimiento, type Politica } from './conocimiento';
import { buscar, tipoSugerido, UMBRAL_RESPUESTA } from './motor';

export interface RespuestaAsistente {
  /** La entrada de la base que responde, o null si nada coincidió. */
  entrada: EntradaConocimiento | null;
  texto: string;
  /** Otras entradas parecidas para ofrecer como "quizá buscabas". */
  relacionadas: EntradaConocimiento[];
  /** Tipo de PQRS que se propone si la persona dice que no le sirvió. */
  tipoPqr: TipoPqrDto;
}

/**
 * Quien contesta las preguntas del asistente. Hoy es la base local; una futura implementación con IA llamaría a un
 * endpoint propio del backend (con límite de uso y la llave guardada en el servidor, nunca en el front) y se cambiaría
 * aquí sin tocar el componente.
 */
@Injectable({ providedIn: 'root', useFactory: () => inject(ProveedorLocal) })
export abstract class ProveedorRespuestas {
  abstract responder(pregunta: string): Promise<RespuestaAsistente>;
  abstract responderEntrada(entrada: EntradaConocimiento): Promise<RespuestaAsistente>;
}

/** Responde con la base de conocimiento local (función pura, para probarla sin Angular). */
export function responderLocal(pregunta: string, p?: Politica): RespuestaAsistente {
  const resultados = buscar(pregunta, p);
  const mejor = resultados[0]?.puntaje >= UMBRAL_RESPUESTA ? resultados[0].entrada : null;
  return {
    entrada: mejor,
    texto: mejor ? respuestaDe(mejor, p) : '',
    relacionadas: resultados
      .map((r) => r.entrada)
      .filter((e) => e !== mejor)
      .slice(0, 3),
    tipoPqr: tipoSugerido(pregunta, mejor),
  };
}

@Injectable({ providedIn: 'root' })
export class ProveedorLocal extends ProveedorRespuestas {
  private readonly api = inject(CuentaApi);

  /** Las cifras salen de la política en vivo; si la API tarda o falla, se responde sin números. */
  private politica(): Promise<Politica> {
    return firstValueFrom(this.api.politica$.pipe(timeout(3000), catchError(() => of(null))));
  }

  async responder(pregunta: string): Promise<RespuestaAsistente> {
    return responderLocal(pregunta, await this.politica());
  }

  async responderEntrada(entrada: EntradaConocimiento): Promise<RespuestaAsistente> {
    const p = await this.politica();
    return { entrada, texto: respuestaDe(entrada, p), relacionadas: [], tipoPqr: tipoSugerido('', entrada) };
  }
}
